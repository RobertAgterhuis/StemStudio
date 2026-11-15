"""Feature 006: dependency rules for synced lyrics and karaoke (plan.md dependency direction).

Worker: ``lyrics`` imports only ``ports``, ``schemas``, ``config`` and the shared egress modules
``metadata.http``, ``metadata.ratelimit`` and ``metadata.user_agent`` (005); ``alignment``
imports only ``ports``, ``schemas`` and ``config`` (it receives the ``DeviceRunner`` from the
composition root); ``jobs``, ``api`` and every other adapter never import ``lyrics`` or
``alignment``; only ``main.py`` composes them.

Engine: ``transport`` and ``mixer`` never import ``karaoke/`` (karaoke reads the transport
clock, never the other way round).
"""

from pathlib import Path

import pytest
from repo_tree import WORKER_PACKAGE as PACKAGE
from repo_tree import write_file as _write
from repo_tree import write_module as _module

import dependency_check
from common import Severity, owner_of
from components import COMPONENTS
from dependency_check import (
    PYTHON_ALLOWED,
    PYTHON_SUBMODULES_ALLOWED,
    TS_MODULE_FORBIDDEN,
    read_ts_module_edges,
)

LYRICS_INIT = (
    "from stemstudio_worker.config import LyricsSettings\n"
    "from stemstudio_worker.ports import LyricsLookup, LyricsQuery\n"
    "from stemstudio_worker import schemas\n"
    "from .lrclib import LrclibLookup\n"
)

LYRICS_EGRESS = (
    "from stemstudio_worker.metadata.http import HttpClient\n"
    "from ..metadata import ratelimit, user_agent\n"
    "import stemstudio_worker.metadata.user_agent\n"
    "from ..metadata.http import fetch_json, MAX_BYTES\n"
)

ALIGNMENT_INIT = (
    "from stemstudio_worker.config import WorkerSettings\n"
    "from stemstudio_worker.ports import Aligner, AlignRequest, DeviceRunner\n"
    "from ..schemas import TimedLyrics\n"
    "from .adapter import StableTsAligner\n"
)

MAIN = (
    "from stemstudio_worker.lyrics import create_lyrics_lookup\n"
    "from stemstudio_worker.alignment import create_aligner\n"
    "from stemstudio_worker.devices import DeviceRunner\n"
)


def _lyrics_tree(root: Path) -> None:
    _module(root, "lyrics/__init__.py", LYRICS_INIT)
    _module(root, "lyrics/lrclib.py", LYRICS_EGRESS)
    _module(root, "lyrics/lrc.py", "from . import normalize\nfrom ..schemas import LyricsLine\n")
    _module(root, "alignment/__init__.py", ALIGNMENT_INIT)
    _module(root, "alignment/adapter.py", "from ..ports import WHISPER_GPU_NEED\n")
    _module(
        root,
        "metadata/http.py",
        "from ..ports import ArtworkFetchError\nfrom . import user_agent\n",
    )
    _module(
        root, "jobs/lyrics_lane.py", "from stemstudio_worker.ports import LyricsLookup, Aligner\n"
    )
    _module(root, "api/lyrics_routes.py", "from ..jobs import lyrics_store\n")
    _module(root, "main.py", MAIN)


def test_lyrics_and_alignment_are_mapped_to_ports_schemas_config() -> None:
    assert PYTHON_ALLOWED["lyrics"] == frozenset({"ports", "schemas", "config"})
    assert PYTHON_ALLOWED["alignment"] == frozenset({"ports", "schemas", "config"})


def test_lyrics_may_import_only_the_shared_egress_modules_of_metadata() -> None:
    assert {
        "lyrics": frozenset({"metadata.http", "metadata.ratelimit", "metadata.user_agent"})
    } == PYTHON_SUBMODULES_ALLOWED


def test_lyrics_and_alignment_importing_allowed_modules_is_clean(tmp_path: Path) -> None:
    _lyrics_tree(tmp_path)
    assert dependency_check.run(tmp_path) == []


ALLOWED = "allowed: metadata.http, metadata.ratelimit, metadata.user_agent"


@pytest.mark.parametrize(
    ("text", "message"),
    [
        ("from ..metadata import images\n", f"lyrics -> metadata.images ({ALLOWED})"),
        ("from stemstudio_worker import metadata\n", f"lyrics -> metadata ({ALLOWED})"),
        ("from .. import metadata\n", f"lyrics -> metadata ({ALLOWED})"),
        ("import stemstudio_worker.metadata.lookup\n", f"lyrics -> metadata.lookup ({ALLOWED})"),
        ("import stemstudio_worker.metadata\n", f"lyrics -> metadata ({ALLOWED})"),
        ("from ..metadata.httpx import x\n", f"lyrics -> metadata.httpx.x ({ALLOWED})"),
        ("from ..metadata.musicbrainz import y\n", f"lyrics -> metadata.musicbrainz.y ({ALLOWED})"),
    ],
)
def test_lyrics_importing_other_metadata_modules_is_forbidden(
    tmp_path: Path, text: str, message: str
) -> None:
    _lyrics_tree(tmp_path)
    _module(tmp_path, "lyrics/wire.py", text)
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.message == f"forbidden dependency {message}"
    assert (finding.path, finding.line, finding.owner) == (
        f"{PACKAGE}/lyrics/wire.py",
        1,
        "audio-ingest",
    )


@pytest.mark.parametrize(
    ("rel", "text", "message"),
    [
        ("lyrics/wire.py", "import stemstudio_worker.jobs.lyrics_lane\n", "lyrics -> jobs"),
        ("lyrics/wire.py", "from stemstudio_worker import api\n", "lyrics -> api"),
        ("lyrics/wire.py", "from ..alignment import adapter\n", "lyrics -> alignment"),
        ("lyrics/wire.py", "from ..ingest import ffmpeg\n", "lyrics -> ingest"),
        ("lyrics/wire.py", "from ..render import x\n", "lyrics -> render"),
        ("lyrics/wire.py", "from ..devices import runner\n", "lyrics -> devices"),
        ("alignment/wire.py", "from ..lyrics import lrc\n", "alignment -> lyrics"),
        ("alignment/wire.py", "from ..devices import runner\n", "alignment -> devices"),
        ("alignment/wire.py", "from ..separation import demucs\n", "alignment -> separation"),
        ("alignment/wire.py", "from ..jobs import scheduler\n", "alignment -> jobs"),
        ("alignment/wire.py", "from ..metadata.http import c\n", "alignment -> metadata"),
        ("jobs/lyrics_lane.py", "from ..lyrics import lrclib\n", "jobs -> lyrics"),
        ("jobs/scheduler.py", "import stemstudio_worker.alignment.adapter\n", "jobs -> alignment"),
        ("api/lyrics_routes.py", "from stemstudio_worker import lyrics\n", "api -> lyrics"),
        ("api/routes.py", "from ..alignment import create_aligner\n", "api -> alignment"),
        ("ingest/fetch.py", "from ..lyrics import lrclib\n", "ingest -> lyrics"),
        ("metadata/lookup.py", "from ..lyrics import select\n", "metadata -> lyrics"),
        ("separation/demucs.py", "from ..alignment import x\n", "separation -> alignment"),
    ],
)
def test_forbidden_lyrics_and_alignment_edges(
    tmp_path: Path, rel: str, text: str, message: str
) -> None:
    _lyrics_tree(tmp_path)
    _module(tmp_path, rel, text)
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.message == f"forbidden dependency {message}"


def test_one_forbidden_statement_with_several_names_is_one_finding(tmp_path: Path) -> None:
    _lyrics_tree(tmp_path)
    _module(tmp_path, "lyrics/wire.py", "from ..jobs import lyrics_lane, lyrics_store\n")
    (finding,) = dependency_check.run(tmp_path)
    assert finding.message == "forbidden dependency lyrics -> jobs"


def test_metadata_http_importing_lyrics_is_forbidden_and_a_cycle(tmp_path: Path) -> None:
    _lyrics_tree(tmp_path)
    _module(tmp_path, "metadata/http.py", "from stemstudio_worker.lyrics import lrc\n")
    summary = sorted((f.severity, f.message) for f in dependency_check.run(tmp_path))
    assert summary == [
        (Severity.ERROR, "cycle: lyrics -> metadata -> lyrics"),
        (Severity.ERROR, "forbidden dependency metadata -> lyrics"),
    ]


@pytest.mark.parametrize(
    ("path", "owner"),
    [
        (f"{PACKAGE}/lyrics/lrclib.py", "audio-ingest"),
        ("worker/tests/lyrics/test_lrc.py", "audio-ingest"),
        (f"{PACKAGE}/alignment/adapter.py", "stem-separation"),
        ("worker/tests/alignment/test_adapter.py", "stem-separation"),
        (f"{PACKAGE}/jobs/lyrics_lane.py", "stem-separation"),
        ("worker/scripts/check_reconstruction.py", "stem-separation"),
        ("worker/scripts/install_music_font.py", "architect"),
        ("src/audio-engine/src/karaoke/clock.ts", "audio-engine"),
    ],
)
def test_lyrics_paths_are_owned(path: str, owner: str) -> None:
    assert owner_of(path) == owner


@pytest.mark.parametrize(
    ("name", "inside", "outside"),
    [
        ("worker/lyrics", "lyrics/lrclib.py", "metadata/http.py"),
        ("worker/alignment", "alignment/adapter.py", "jobs/lyrics_lane.py"),
    ],
)
def test_lyrics_components_measure_their_production_code(
    name: str, inside: str, outside: str
) -> None:
    (component,) = [c for c in COMPONENTS if c.name == name]
    assert component.contains(f"{PACKAGE}/{inside}", test=False)
    assert not component.contains(f"{PACKAGE}/{outside}", test=False)
    assert not component.contains(f"worker/tests/{inside}", test=True)


# ------------------------------------------------------------------ engine karaoke module

ENGINE = "src/audio-engine"


def _engine(root: Path, rel: str, text: str) -> None:
    _write(root, f"{ENGINE}/{rel}", text)


def _engine_tree(root: Path) -> None:
    _engine(root, "package.json", "{}")
    _engine(root, "src/engine.ts", "import { KaraokeController } from './karaoke/controller';\n")
    _engine(root, "src/index.ts", "export * from './karaoke';\nexport * from './transport';\n")
    _engine(root, "src/transport.ts", "import { FrameLoop } from './frame-loop';\n")
    _engine(root, "src/mixer.ts", "// karaoke is driven from engine.ts, not from './karaoke'\n")
    _engine(root, "src/karaoke/index.ts", "export * from './controller';\n")
    _engine(root, "src/karaoke/clock.ts", "import type { TransportClock } from '../transport';\n")
    _engine(root, "tests/karaoke.test.ts", "import { x } from '../src/karaoke/clock';\n")


def test_transport_and_mixer_must_not_import_karaoke() -> None:
    assert {
        "audio-engine/transport": frozenset({"audio-engine/karaoke"}),
        "audio-engine/mixer": frozenset({"audio-engine/karaoke"}),
    } == TS_MODULE_FORBIDDEN


def test_engine_tree_with_karaoke_reading_the_transport_is_clean(tmp_path: Path) -> None:
    _engine_tree(tmp_path)
    assert dependency_check.run(tmp_path) == []


def test_module_edges_name_package_and_top_module(tmp_path: Path) -> None:
    _engine_tree(tmp_path)
    edges = {(e.source, e.target) for e in read_ts_module_edges(tmp_path / "src", tmp_path)}
    assert ("audio-engine/engine", "audio-engine/karaoke") in edges
    assert ("audio-engine/karaoke", "audio-engine/transport") in edges
    assert ("audio-engine/transport", "audio-engine/frame-loop") in edges
    assert not any(source.startswith("audio-engine/tests") for source, _ in edges)


@pytest.mark.parametrize(
    ("rel", "text", "source"),
    [
        ("src/transport.ts", "import { Ball } from './karaoke/ball';\n", "transport"),
        ("src/transport.ts", "import type { K } from './karaoke';\n", "transport"),
        ("src/transport.ts", "const k = await import('./karaoke/clock.js');\n", "transport"),
        ("src/mixer.ts", "export { attach } from './karaoke/controller';\n", "mixer"),
        ("src/mixer.ts", 'import {\n  a,\n} from "./karaoke/index";\n', "mixer"),
        ("src/transport/clock.ts", "import { c } from '../karaoke/cursor';\n", "transport"),
    ],
)
def test_transport_or_mixer_importing_karaoke_is_forbidden(
    tmp_path: Path, rel: str, text: str, source: str
) -> None:
    _engine_tree(tmp_path)
    _engine(tmp_path, rel, text)
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.message == f"forbidden dependency audio-engine/{source} -> audio-engine/karaoke"
    assert (finding.path, finding.owner) == (f"{ENGINE}/{rel}", "audio-engine")


@pytest.mark.parametrize(
    "text",
    [
        "import { k } from './karaoke-text';\n",
        "import { m } from 'karaoke';\n",
        "import { t } from '../../web-interop/src/karaoke';\n",
        "/* import { k } from './karaoke'; */\n",
    ],
)
def test_lookalike_specifiers_are_not_karaoke_edges(tmp_path: Path, text: str) -> None:
    _engine_tree(tmp_path)
    _write(tmp_path, "src/web-interop/package.json", "{}")
    _engine(tmp_path, "src/transport.ts", text)
    findings = dependency_check.run(tmp_path)
    assert not [f for f in findings if "karaoke" in f.message]


def test_packages_without_module_rules_contribute_no_module_edges(tmp_path: Path) -> None:
    _write(tmp_path, "src/web-interop/package.json", "{}")
    _write(tmp_path, "src/web-interop/src/a.ts", "import { b } from './b';\n")
    assert read_ts_module_edges(tmp_path / "src", tmp_path) == []
