"""Feature 010: voices and own-voice conversion rules (plan.md dependency direction, 010:T006).

.NET: ``Api.Generation -> Api.Voices`` one way, so the C# files below ``StemStudio.Api/Voices``
never reference the namespace ``StemStudio.Api.Generation`` (nor ``StemStudio.Web``). The
generator client ``StemStudio.Api/Generator`` is the lowest of the three: it references neither
``StemStudio.Api.Generation`` nor ``StemStudio.Api.Voices``. A namespace entry forbids the
namespace and every namespace below it, not a longer name (``StemStudio.Api.GenerationX``).

Generator: the SoulX-Singer adapters ``engines/soulx_svc.py`` and ``engines/rmvpe.py`` follow
the engine layer rule (only ``ports``, ``schemas``, ``config``), ``audio/remix.py`` and
``audio/normalise.py`` the audio rule (the same); all four are measured (not vendored), are owned
by ``generative`` and count towards their component rows.
"""

from pathlib import Path

import pytest
from repo_tree import GENERATOR_PACKAGE as GEN
from repo_tree import api_findings, write_generator_tree
from repo_tree import write_generator_module as _gen

import dependency_check
from common import Severity, is_excluded, owner_of
from components import COMPONENTS
from dependency_check import DOTNET_FOLDER_FORBIDDEN

# ------------------------------------------------------------------ .NET: Voices -/- Generation


def test_voices_folder_forbids_generation_and_web() -> None:
    assert DOTNET_FOLDER_FORBIDDEN["StemStudio.Api/Voices"] == frozenset(
        {"StemStudio.Web", "StemStudio.Api.Generation"}
    )


def test_generator_client_forbids_generation_voices_and_web() -> None:
    assert DOTNET_FOLDER_FORBIDDEN["StemStudio.Api/Generator"] == frozenset(
        {"StemStudio.Web", "StemStudio.Api.Generation", "StemStudio.Api.Voices"}
    )


def test_generation_still_only_forbids_web() -> None:
    assert DOTNET_FOLDER_FORBIDDEN["StemStudio.Api/Generation"] == frozenset({"StemStudio.Web"})


@pytest.mark.parametrize(
    ("rel", "text", "where"),
    [
        ("Voices/VoiceEndpoints.cs", "using StemStudio.Api.Generation;\n", (1, "Api.Generation")),
        (
            "Voices/Consent/ConsentService.cs",
            "using System;\n\nusing StemStudio.Api.Generation.Songs;\n",
            (3, "Api.Generation"),
        ),
        (
            "Voices/VoiceProfileStore.cs",
            "class S\n{\n    StemStudio.Api.Generation.SongService? _s;\n}\n",
            (3, "Api.Generation"),
        ),
        (
            "Voices/Alias.cs",
            "using G = StemStudio.Api.Generation.TakeStorage;\n",
            (1, "Api.Generation"),
        ),
        ("Voices/Ui.cs", "using StemStudio.Web.Voices;\n", (1, "Web")),
        (
            "Generator/GeneratorClient.cs",
            "using StemStudio.Api.Generation;\n",
            (1, "Api.Generation"),
        ),
        (
            "Generator/SampleCheck.cs",
            "using static StemStudio.Api.Voices.Texts;\n",
            (1, "Api.Voices"),
        ),
    ],
)
def test_voices_and_generator_client_references_are_forbidden(
    tmp_path: Path, rel: str, text: str, where: tuple[int, str]
) -> None:
    line, target = where
    folder = "StemStudio.Api/" + rel.split("/", maxsplit=1)[0]
    message = f"forbidden dependency {folder} -> StemStudio.{target}"
    assert api_findings(tmp_path, rel, text) == [(Severity.ERROR, message, line, "backend")]


ALLOWED_VOICES = """namespace StemStudio.Api.Voices;

using StemStudio.Api.GenerationTools;
using StemStudio.Api.Generator;
using StemStudio.Api.Audio;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Voices;
using StemStudio.Domain.Generation;
using StemStudio.Domain.Voices;
using StemStudio.Persistence;

/// <summary>Never calls StemStudio.Api.Generation (it calls us).</summary>
public sealed class VoiceProfileService
{
    private const string Route = "StemStudio.Api.Generation/songs";
}
"""

ALLOWED_GENERATION = """namespace StemStudio.Api.Generation;

using StemStudio.Api.Voices;
using StemStudio.Api.Generator;
"""


def test_allowed_voices_and_generation_references_are_clean(tmp_path: Path) -> None:
    api_findings(tmp_path, "Voices/VoiceProfileService.cs", ALLOWED_VOICES)
    api_findings(tmp_path, "Generation/SongService.cs", ALLOWED_GENERATION)
    assert (
        api_findings(tmp_path, "Generator/GeneratorClient.cs", "using StemStudio.Contracts;\n")
        == []
    )


# ------------------------------------------------------------------ generator: SoulX adapters

SOULX_SVC = (
    "from __future__ import annotations\n"
    "import importlib.util\n"
    "from pathlib import Path\n"
    "from stemstudio_generator.ports import VoiceConverter, SOULX_GPU_NEED\n"
    "from ..config import GeneratorSettings\n"
    "from .. import schemas\n"
    "from .rmvpe import RmvpePitchTracker\n"
    "\n"
    "\n"
    "def load() -> object:\n"
    "    from soulxsinger.models.soulxsinger_svc import SoulXSingerSVC\n"
    "\n"
    "    return SoulXSingerSVC\n"
)

RMVPE = (
    "import importlib.util\n"
    "from ..ports import PitchTracker, PitchTrackError\n"
    "from ..config import SOULX_SOURCE_DIR\n"
)

GENERATOR_TREE = {
    "engines/soulx_svc.py": SOULX_SVC,
    "engines/rmvpe.py": RMVPE,
    "audio/remix.py": "import numpy as np\nfrom ..ports import Take\nfrom . import normalise\n",
    "audio/normalise.py": "import soundfile\nfrom stemstudio_generator.schemas import Take\n",
    "main.py": (
        "from stemstudio_generator.engines.soulx_svc import SoulXVoiceConverter\n"
        "from stemstudio_generator.engines.rmvpe import RmvpePitchTracker\n"
        "from stemstudio_generator.audio import remix, normalise\n"
    ),
}


@pytest.mark.parametrize(
    ("rel", "text", "edge"),
    [
        ("engines/soulx_svc.py", "from ..devices.nvml import free_mib\n", "engines -> devices"),
        ("engines/soulx_svc.py", "from ..audio.remix import remix\n", "engines -> audio"),
        (
            "engines/rmvpe.py",
            "from stemstudio_generator.jobs import scheduler\n",
            "engines -> jobs",
        ),
        ("engines/rmvpe.py", "import stemstudio_generator.api\n", "engines -> api"),
        ("audio/remix.py", "from ..engines.soulx_svc import load\n", "audio -> engines"),
        ("audio/normalise.py", "from ..devices import lease\n", "audio -> devices"),
        ("audio/normalise.py", "from ..jobs.store import JobFiles\n", "audio -> jobs"),
    ],
)
def test_soulx_adapters_follow_the_layer_rule(
    tmp_path: Path, rel: str, text: str, edge: str
) -> None:
    write_generator_tree(tmp_path, GENERATOR_TREE)
    assert dependency_check.run(tmp_path) == [], "the SoulX tree itself is clean"
    _gen(tmp_path, rel, text)
    messages = [f.message for f in dependency_check.run(tmp_path) if f.path == f"{GEN}/{rel}"]
    assert messages == [f"forbidden dependency {edge}"]


@pytest.mark.parametrize(
    ("rel", "component"),
    [
        ("engines/soulx_svc.py", "generator/engines"),
        ("engines/rmvpe.py", "generator/engines"),
        ("audio/remix.py", "generator/audio"),
        ("audio/normalise.py", "generator/audio"),
    ],
)
def test_soulx_adapters_are_measured_owned_and_counted(rel: str, component: str) -> None:
    path = f"{GEN}/{rel}"
    assert not is_excluded(path)
    assert owner_of(path) == "generative"
    owners = [c.name for c in COMPONENTS if c.contains(path, test=False)]
    assert owners == [component]
