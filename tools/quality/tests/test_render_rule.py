"""Feature 007: dependency rules for export and the asset library (plan.md dependency direction).

Worker: ``render`` imports only ``ports``, ``schemas`` and ``config``; ``jobs`` and ``api`` (and
every other adapter) never import ``render``; only ``main.py`` composes it.

.NET: ``StemStudio.Domain/Rendering`` references no other StemStudio project (in particular not
``StemStudio.Api`` or ``StemStudio.Persistence``); ``StemStudio.Api/Assets`` never references
``StemStudio.Web``.
"""

from pathlib import Path

import pytest

import dependency_check
from common import Severity, owner_of
from dependency_check import DOTNET_FOLDER_FORBIDDEN, PYTHON_ALLOWED

PACKAGE = "worker/src/stemstudio_worker"

RENDER_INIT = (
    "from stemstudio_worker.config import RenderSettings\n"
    "from stemstudio_worker.ports import RenderExecutor, LibraryReader\n"
    "from stemstudio_worker import schemas\n"
    "from .mixing import mix_block\n"
)


def _write(root: Path, rel: str, text: str) -> None:
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def _module(root: Path, rel: str, text: str) -> None:
    _write(root, f"{PACKAGE}/{rel}", text)


def _render_tree(root: Path) -> None:
    _module(root, "render/__init__.py", RENDER_INIT)
    _module(root, "render/reader.py", "from ..ports import is_allowed_library_path\n")
    _module(
        root, "render/encoders.py", "from ..schemas import RenderOutput\nfrom . import dither\n"
    )
    _module(root, "jobs/render_lane.py", "from stemstudio_worker.ports import RenderExecutor\n")
    _module(root, "jobs/lanes.py", "from ..config import WorkerSettings\n")
    _module(root, "main.py", "from stemstudio_worker.render import create_render_executor\n")


def test_render_is_mapped_to_ports_schemas_config() -> None:
    assert PYTHON_ALLOWED["render"] == frozenset({"ports", "schemas", "config"})


def test_render_importing_allowed_modules_is_clean(tmp_path: Path) -> None:
    _render_tree(tmp_path)
    assert dependency_check.run(tmp_path) == []


@pytest.mark.parametrize(
    ("rel", "text", "message"),
    [
        ("render/wire.py", "import stemstudio_worker.jobs.lanes\n", "render -> jobs"),
        ("render/wire.py", "from stemstudio_worker import api\n", "render -> api"),
        ("render/wire.py", "from ..ingest import ffmpeg\n", "render -> ingest"),
        ("render/wire.py", "from ..metadata import images\n", "render -> metadata"),
        ("render/wire.py", "from ..separation import x\n", "render -> separation"),
        ("render/wire.py", "from ..devices import runner\n", "render -> devices"),
        ("jobs/render_lane.py", "from ..render import executor\n", "jobs -> render"),
        ("jobs/lanes.py", "import stemstudio_worker.render.mixing\n", "jobs -> render"),
        ("api/routes.py", "from stemstudio_worker import render\n", "api -> render"),
        ("api/render_routes.py", "from ..render.reader import x\n", "api -> render"),
        ("ingest/fetch.py", "from stemstudio_worker.render import y\n", "ingest -> render"),
        ("metadata/images.py", "from ..render import encoders\n", "metadata -> render"),
        ("detail/steps.py", "from ..render import mixing\n", "detail -> render"),
    ],
)
def test_forbidden_render_edges(tmp_path: Path, rel: str, text: str, message: str) -> None:
    _render_tree(tmp_path)
    _module(tmp_path, rel, text)
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.message == f"forbidden dependency {message}"


def test_render_importing_main_is_forbidden_and_a_cycle(tmp_path: Path) -> None:
    _render_tree(tmp_path)
    _module(tmp_path, "render/wire.py", "from stemstudio_worker import main\n")
    summary = sorted((f.severity, f.message) for f in dependency_check.run(tmp_path))
    assert summary == [
        (Severity.ERROR, "cycle: main -> render -> main"),
        (Severity.ERROR, "forbidden dependency render -> main"),
    ]


@pytest.mark.parametrize(
    "path",
    [f"{PACKAGE}/render/mixing.py", "worker/tests/render/test_mixing.py"],
)
def test_render_is_owned_by_audio_ingest(path: str) -> None:
    assert owner_of(path) == "audio-ingest"


@pytest.mark.parametrize(
    "path",
    [f"{PACKAGE}/jobs/render_lane.py", f"{PACKAGE}/jobs/lanes.py", "worker/tests/jobs/test_x.py"],
)
def test_render_lane_is_owned_by_stem_separation(path: str) -> None:
    assert owner_of(path) == "stem-separation"


def test_render_finding_names_its_owner(tmp_path: Path) -> None:
    _render_tree(tmp_path)
    _module(tmp_path, "render/wire.py", "from stemstudio_worker.jobs import x\n")
    (finding,) = dependency_check.run(tmp_path)
    assert finding.owner == "audio-ingest"
    assert (finding.path, finding.line) == (f"{PACKAGE}/render/wire.py", 1)


# ------------------------------------------------------------------ .NET feature folders

DOMAIN = "src/StemStudio.Domain"
API = "src/StemStudio.Api"


def test_domain_rendering_forbids_every_other_project() -> None:
    assert DOTNET_FOLDER_FORBIDDEN["StemStudio.Domain/Rendering"] == frozenset(
        {"StemStudio.Api", "StemStudio.Persistence", "StemStudio.Web", "StemStudio.Contracts"}
    )


def test_api_assets_forbids_web() -> None:
    assert DOTNET_FOLDER_FORBIDDEN["StemStudio.Api/Assets"] == frozenset({"StemStudio.Web"})


@pytest.mark.parametrize(
    ("rel", "text", "target"),
    [
        (f"{DOMAIN}/Rendering/RenderPlanner.cs", "using StemStudio.Api.Assets;\n", "Api"),
        (f"{DOMAIN}/Rendering/Plan.cs", "using StemStudio.Persistence;\n", "Persistence"),
        (f"{DOMAIN}/Rendering/Sub/Law.cs", "var x = StemStudio.Web.Foo.Bar;\n", "Web"),
        (f"{DOMAIN}/Rendering/Dto.cs", "using StemStudio.Contracts.Assets;\n", "Contracts"),
    ],
)
def test_domain_rendering_referencing_other_projects_is_forbidden(
    tmp_path: Path, rel: str, text: str, target: str
) -> None:
    _write(tmp_path, rel, text)
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    expected = f"forbidden dependency StemStudio.Domain/Rendering -> StemStudio.{target}"
    assert finding.message == expected
    assert (finding.path, finding.line, finding.owner) == (rel, 1, "backend")


def test_api_assets_referencing_web_is_forbidden(tmp_path: Path) -> None:
    rel = f"{API}/Assets/AssetEndpoints.cs"
    _write(tmp_path, rel, "using StemStudio.Domain.Rendering;\nusing StemStudio.Web.Library;\n")
    (finding,) = dependency_check.run(tmp_path)
    assert finding.message == "forbidden dependency StemStudio.Api/Assets -> StemStudio.Web"
    assert (finding.path, finding.line) == (rel, 2)


ALLOWED_RENDERING = """namespace StemStudio.Domain.Rendering;

using StemStudio.Domain.Tracks;
using StemStudio.Domain.Mixes;

/// <summary>Plans a render; never calls StemStudio.Api or StemStudio.Persistence.</summary>
public sealed class RenderPlanner
{
    private const string Layout = "stems/{0}.flac";
}
"""

ALLOWED_ASSETS = """namespace StemStudio.Api.Assets;

using StemStudio.Contracts.Assets;
using StemStudio.Domain.Rendering;
using StemStudio.Persistence;
using StemStudio.Api.Worker;
"""


def test_allowed_rendering_and_assets_references_are_clean(tmp_path: Path) -> None:
    _write(tmp_path, f"{DOMAIN}/Rendering/RenderPlanner.cs", ALLOWED_RENDERING)
    _write(tmp_path, f"{API}/Assets/AssetService.cs", ALLOWED_ASSETS)
    _write(tmp_path, f"{DOMAIN}/Mixes/Mix.cs", "using StemStudio.Api;\n")
    assert dependency_check.run(tmp_path) == []
