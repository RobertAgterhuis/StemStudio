"""Feature 005: dependency rules for artwork and band info (plan.md dependency direction).

Worker: ``metadata`` imports only ``ports``, ``schemas`` and ``config``; ``ingest``, ``jobs`` and
``api`` (and every other adapter) never import ``metadata``; only ``main.py`` composes it (the
``ImageNormalizer`` reaches ``ingest`` through the composition root).

.NET: the backend feature folders ``StemStudio.Api/Metadata`` and ``StemStudio.Api/Artwork`` never
reference ``StemStudio.Web`` (source-level check of using directives and qualified names).
"""

from pathlib import Path

import pytest
import repo_tree

import dependency_check
from common import Severity, owner_of
from dependency_check import DOTNET_FOLDER_FORBIDDEN, PYTHON_ALLOWED

PACKAGE = repo_tree.WORKER_PACKAGE
_write = repo_tree.write_file
_module = repo_tree.write_module

METADATA_INIT = (
    "from stemstudio_worker.config import WorkerSettings\n"
    "from stemstudio_worker.ports import MetadataLookup, ImageNormalizer\n"
    "from stemstudio_worker import schemas\n"
    "from .musicbrainz import client\n"
)


def _metadata_tree(root: Path) -> None:
    _module(root, "metadata/__init__.py", METADATA_INIT)
    _module(root, "metadata/images.py", "from ..schemas import ArtworkInfo\nfrom . import http\n")
    _module(root, "metadata/http.py", "from stemstudio_worker.ports import ArtworkFetchError\n")
    _module(root, "ingest/artwork.py", "from stemstudio_worker.ports import ImageNormalizer\n")
    _module(root, "main.py", "from stemstudio_worker.metadata import create_metadata_lookup\n")


def test_metadata_is_mapped_to_ports_schemas_config() -> None:
    assert PYTHON_ALLOWED["metadata"] == frozenset({"ports", "schemas", "config"})


def test_metadata_importing_allowed_modules_is_clean(tmp_path: Path) -> None:
    _metadata_tree(tmp_path)
    assert dependency_check.run(tmp_path) == []


@pytest.mark.parametrize(
    ("rel", "text", "message"),
    [
        ("metadata/http.py", "from ..ingest import validation\n", "metadata -> ingest"),
        ("metadata/wire.py", "import stemstudio_worker.jobs.runner\n", "metadata -> jobs"),
        ("metadata/wire.py", "from stemstudio_worker import api\n", "metadata -> api"),
        ("metadata/wire.py", "from ..separation import x\n", "metadata -> separation"),
        ("metadata/wire.py", "from ..devices import runner\n", "metadata -> devices"),
        ("ingest/artwork.py", "from ..metadata import images\n", "ingest -> metadata"),
        ("ingest/fetch.py", "import stemstudio_worker.metadata.images\n", "ingest -> metadata"),
        ("jobs/metadata_lane.py", "from stemstudio_worker.metadata import x\n", "jobs -> metadata"),
        ("api/routes.py", "from stemstudio_worker import metadata\n", "api -> metadata"),
        ("separation/demucs.py", "from ..metadata import http\n", "separation -> metadata"),
        ("detail/steps.py", "from stemstudio_worker.metadata import y\n", "detail -> metadata"),
        ("analysis/beats.py", "from ..metadata.titles import clean\n", "analysis -> metadata"),
    ],
)
def test_forbidden_metadata_edges(tmp_path: Path, rel: str, text: str, message: str) -> None:
    _metadata_tree(tmp_path)
    _module(tmp_path, rel, text)
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.message == f"forbidden dependency {message}"


def test_metadata_importing_main_is_forbidden_and_a_cycle(tmp_path: Path) -> None:
    _metadata_tree(tmp_path)
    _module(tmp_path, "metadata/wire.py", "from stemstudio_worker import main\n")
    summary = sorted((f.severity, f.message) for f in dependency_check.run(tmp_path))
    assert summary == [
        (Severity.ERROR, "cycle: main -> metadata -> main"),
        (Severity.ERROR, "forbidden dependency metadata -> main"),
    ]


@pytest.mark.parametrize(
    "path",
    [f"{PACKAGE}/metadata/musicbrainz.py", "worker/tests/metadata/test_musicbrainz.py"],
)
def test_metadata_is_owned_by_audio_ingest(path: str) -> None:
    assert owner_of(path) == "audio-ingest"


def test_metadata_finding_names_its_owner(tmp_path: Path) -> None:
    _metadata_tree(tmp_path)
    _module(tmp_path, "metadata/wire.py", "from stemstudio_worker.jobs import x\n")
    (finding,) = dependency_check.run(tmp_path)
    assert finding.owner == "audio-ingest"
    assert (finding.path, finding.line) == (f"{PACKAGE}/metadata/wire.py", 1)


# ------------------------------------------------------------------ .NET feature folders

API = "src/StemStudio.Api"


@pytest.mark.parametrize("folder", ["StemStudio.Api/Metadata", "StemStudio.Api/Artwork"])
def test_api_feature_folders_forbid_web(folder: str) -> None:
    assert DOTNET_FOLDER_FORBIDDEN[folder] == frozenset({"StemStudio.Web"})


@pytest.mark.parametrize(
    ("rel", "text", "line"),
    [
        ("Metadata/MetadataApplier.cs", "using System;\nusing StemStudio.Web;\n", 2),
        ("Metadata/Deep/Lookup.cs", "global using StemStudio.Web.Components;\n", 1),
        ("Artwork/ArtworkStore.cs", "using Client = StemStudio.Web.Api.Client;\n", 1),
        ("Artwork/ArtworkEndpoints.cs", "class A\n{\n    StemStudio.Web.Foo? _f;\n}\n", 3),
        ("Artwork/Static.cs", "using static StemStudio.Web.Text;\n", 1),
    ],
)
def test_api_feature_folder_referencing_web_is_forbidden(
    tmp_path: Path, rel: str, text: str, line: int
) -> None:
    _write(tmp_path, f"{API}/{rel}", text)
    (finding,) = dependency_check.run(tmp_path)
    folder = "StemStudio.Api/" + rel.split("/", maxsplit=1)[0]
    assert finding.severity is Severity.ERROR
    assert finding.message == f"forbidden dependency {folder} -> StemStudio.Web"
    assert (finding.path, finding.line) == (f"{API}/{rel}", line)
    assert finding.owner == "backend"


ALLOWED_SOURCE = '''namespace StemStudio.Api.Metadata;

using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence;
using StemStudio.Api.Worker;
using StemStudio.WebTools;

// StemStudio.Web is never referenced here
/* not even StemStudio.Web.Components
   in a block comment */
internal sealed class Applier
{
    private const string Note = "StemStudio.Web";
    private const string Verbatim = @"StemStudio.Web ""quoted""";
    private const string Raw = """
        StemStudio.Web
        """;
    private const char Quote = '"';
}
'''


def test_api_feature_folder_allowed_references_comments_and_strings_are_clean(
    tmp_path: Path,
) -> None:
    _write(tmp_path, f"{API}/Metadata/Applier.cs", ALLOWED_SOURCE)
    assert dependency_check.run(tmp_path) == []


def test_other_api_folders_are_not_folder_checked(tmp_path: Path) -> None:
    _write(tmp_path, f"{API}/Health/HealthEndpoints.cs", "using StemStudio.Web;\n")
    _write(tmp_path, f"{API}/MetadataHelpers.cs", "using StemStudio.Web;\n")
    assert dependency_check.run(tmp_path) == []


def test_generated_files_in_feature_folders_are_ignored(tmp_path: Path) -> None:
    _write(tmp_path, f"{API}/Metadata/obj/X.cs", "using StemStudio.Web;\n")
    _write(tmp_path, f"{API}/Artwork/Model.g.cs", "using StemStudio.Web;\n")
    assert dependency_check.run(tmp_path) == []


def test_each_reference_is_reported(tmp_path: Path) -> None:
    _write(tmp_path, f"{API}/Metadata/A.cs", "using StemStudio.Web;\nusing StemStudio.Web.X;\n")
    lines = [f.line for f in dependency_check.run(tmp_path)]
    assert lines == [1, 2]
