"""Feature 008: dependency rules for sheet music (plan.md dependency direction, tasks 008:T007).

Worker: ``notation`` imports only ``ports``, ``schemas`` and (third party) music21; ``engraving``
only ``ports``, ``schemas`` and verovio, cairosvg, pypdf; ``transcription`` only ``ports``,
``schemas``, ``config`` and its own ``vendor`` (onnxruntime/torch allowed); ``jobs``, ``api`` and
every other adapter import none of the three; only ``main.py`` composes them. Standard-library
imports are always allowed. ``transcription/vendor/**`` is third-party code: excluded from the
SIG unit checks, duplication and Sonar, and it contributes no edges.
"""

import json
from pathlib import Path

import pytest
from repo_tree import WORKER_PACKAGE as PACKAGE
from repo_tree import write_module as _module

import dependency_check
import duplication
import sig_check
import sonar_args
from common import REPO_ROOT, Severity, is_excluded, owner_of
from dependency_check import PYTHON_ALLOWED, PYTHON_EXTERNAL_ALLOWED, read_external_edges

VENDOR = f"{PACKAGE}/transcription/vendor"

NOTATION_BUILDERS = (
    "from __future__ import annotations\n"
    "import math\n"
    "from collections.abc import Sequence\n"
    "import music21\n"
    "from music21 import stream, meter\n"
    "from music21.note import Note\n"
    "import stemstudio_worker.ports\n"
)

ENGRAVING_PDF = (
    "import io\n"
    "from pypdf import PdfWriter\n"
    "from pypdf.generic import NameObject\n"
    "\n"
    "\n"
    "def svg_to_pdf(svg: bytes) -> bytes:\n"
    "    import cairosvg\n"
    "\n"
    "    return cairosvg.svg2pdf(bytestring=svg)\n"
)

MAIN = (
    "from stemstudio_worker.transcription import create_pitch_transcriber\n"
    "from stemstudio_worker.notation import Music21Notator\n"
    "from stemstudio_worker.engraving import VerovioEngraver\n"
    "from stemstudio_worker.analysis.drum_onsets import OnsetDrumTranscriber\n"
)

SCORE_TREE = {
    "notation/__init__.py": (
        "from stemstudio_worker.ports import Notator, NotationSettings\n"
        "from ..schemas import ScoreProfile\n"
        "from .builders import build\n"
    ),
    "notation/builders.py": NOTATION_BUILDERS,
    "notation/keys.py": (
        "from typing import TYPE_CHECKING\nif TYPE_CHECKING:\n    from music21.key import Key\n"
    ),
    "engraving/__init__.py": (
        "from stemstudio_worker.ports import ScoreEngraver, EngravedScore, TranscriptionError\n"
        "from ..schemas import MUSIC_FONT_MISSING_TEXT\n"
    ),
    "engraving/verovio_svg.py": "import verovio\nimport subprocess\nfrom . import fonts\n",
    "engraving/pdf.py": ENGRAVING_PDF,
    "engraving/fonts.py": "import shutil\nimport subprocess\n",
    "transcription/__init__.py": (
        "from stemstudio_worker.config import TranscriptionSettings\n"
        "from stemstudio_worker.ports import PitchTranscriber, PIANO_GPU_NEED\n"
        "from .. import schemas\n"
    ),
    "transcription/basic_pitch.py": (
        "import numpy as np\nimport onnxruntime\nfrom .vendor.basic_pitch import note_creation\n"
    ),
    "transcription/piano.py": (
        "import torch\nfrom stemstudio_worker.transcription.vendor.piano_transcription import m\n"
    ),
    # Vendored code is third party: neither its internal nor its external imports count.
    "transcription/vendor/basic_pitch/note_creation.py": (
        "from stemstudio_worker.jobs import queue\nimport scipy\n"
    ),
    "analysis/drum_onsets.py": "import numpy as np\nfrom ..ports import DrumTranscriber, DrumHit\n",
    "jobs/transcription_service.py": "from ..ports import Notator, ScoreEngraver\n",
    "api/transcription_routes.py": "from ..jobs import transcription_store\n",
    "main.py": MAIN,
}


def _score_tree(root: Path) -> None:
    for rel, text in SCORE_TREE.items():
        _module(root, rel, text)


def test_score_modules_are_mapped() -> None:
    assert PYTHON_ALLOWED["notation"] == frozenset({"ports", "schemas"})
    assert PYTHON_ALLOWED["engraving"] == frozenset({"ports", "schemas"})
    assert PYTHON_ALLOWED["transcription"] == frozenset({"ports", "schemas", "config"})


def test_notation_and_engraving_have_third_party_allow_lists() -> None:
    assert PYTHON_EXTERNAL_ALLOWED["notation"] == frozenset({"music21"})
    assert PYTHON_EXTERNAL_ALLOWED["engraving"] == frozenset({"verovio", "cairosvg", "pypdf"})
    # 014 T015 adds documents -> reportlab (test_practice_flow_rules.py).
    assert set(PYTHON_EXTERNAL_ALLOWED) == {"notation", "engraving", "documents"}


def test_score_tree_importing_allowed_modules_is_clean(tmp_path: Path) -> None:
    _score_tree(tmp_path)
    assert dependency_check.run(tmp_path) == []


@pytest.mark.parametrize(
    ("rel", "text", "message"),
    [
        ("notation/wire.py", "from ..config import WorkerSettings\n", "notation -> config"),
        ("notation/wire.py", "from ..transcription import fake\n", "notation -> transcription"),
        ("notation/wire.py", "import stemstudio_worker.engraving.pdf\n", "notation -> engraving"),
        ("notation/wire.py", "from stemstudio_worker import jobs\n", "notation -> jobs"),
        ("notation/wire.py", "from ..analysis import beats\n", "notation -> analysis"),
        ("engraving/wire.py", "from ..config import WorkerSettings\n", "engraving -> config"),
        ("engraving/wire.py", "from ..notation import writer\n", "engraving -> notation"),
        ("engraving/wire.py", "from ..transcription import x\n", "engraving -> transcription"),
        ("engraving/wire.py", "from stemstudio_worker.api import app\n", "engraving -> api"),
        ("transcription/wire.py", "from ..notation import keys\n", "transcription -> notation"),
        ("transcription/wire.py", "from ..engraving import pdf\n", "transcription -> engraving"),
        ("transcription/wire.py", "import stemstudio_worker.jobs.lanes\n", "transcription -> jobs"),
        ("transcription/wire.py", "from stemstudio_worker import api\n", "transcription -> api"),
        ("transcription/wire.py", "from ..devices import runner\n", "transcription -> devices"),
        ("transcription/wire.py", "from ..analysis import x\n", "transcription -> analysis"),
        ("jobs/transcription_lane.py", "from ..transcription import y\n", "jobs -> transcription"),
        ("jobs/transcription_service.py", "from ..notation import n\n", "jobs -> notation"),
        ("jobs/lanes.py", "import stemstudio_worker.engraving\n", "jobs -> engraving"),
        ("api/transcription_routes.py", "from .. import notation\n", "api -> notation"),
        ("api/routes.py", "from ..engraving import fonts\n", "api -> engraving"),
        ("api/health.py", "from ..transcription import x\n", "api -> transcription"),
        ("analysis/drum_onsets.py", "from ..notation import timebase\n", "analysis -> notation"),
        ("separation/demucs.py", "from ..transcription import x\n", "separation -> transcription"),
    ],
)
def test_forbidden_score_edges(tmp_path: Path, rel: str, text: str, message: str) -> None:
    _score_tree(tmp_path)
    _module(tmp_path, rel, text)
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.message == f"forbidden dependency {message}"


NOTATION = "beat-analysis"
ENGRAVING = "audio-ingest"


@pytest.mark.parametrize(
    ("rel", "text", "expected"),
    [
        ("notation/wire.py", "import numpy as np\n", ("notation -> numpy", NOTATION)),
        ("notation/wire.py", "import torch.nn\n", ("notation -> torch", NOTATION)),
        ("notation/wire.py", "from onnxruntime import S\n", ("notation -> onnxruntime", NOTATION)),
        ("notation/wire.py", "import verovio\n", ("notation -> verovio", NOTATION)),
        ("notation/wire.py", "import os, matplotlib\n", ("notation -> matplotlib", NOTATION)),
        ("engraving/wire.py", "from music21 import s\n", ("engraving -> music21", ENGRAVING)),
        ("engraving/wire.py", "import torch\n", ("engraving -> torch", ENGRAVING)),
        ("engraving/wire.py", "import httpx\n", ("engraving -> httpx", ENGRAVING)),
        (
            "engraving/wire.py",
            "def f() -> None:\n    import numpy\n",
            ("engraving -> numpy", ENGRAVING),
        ),
    ],
)
def test_forbidden_third_party_imports(
    tmp_path: Path, rel: str, text: str, expected: tuple[str, str]
) -> None:
    message, owner = expected
    _score_tree(tmp_path)
    _module(tmp_path, rel, text)
    (finding,) = dependency_check.run(tmp_path)
    assert finding.severity is Severity.ERROR
    assert finding.check == "dependency"
    assert finding.message == f"forbidden third-party dependency {message}"
    assert (finding.path, finding.owner) == (f"{PACKAGE}/{rel}", owner)


def test_one_third_party_statement_with_several_names_is_one_finding(tmp_path: Path) -> None:
    _score_tree(tmp_path)
    _module(tmp_path, "notation/wire.py", "from numpy import array, interp\n")
    (finding,) = dependency_check.run(tmp_path)
    assert (finding.line, finding.message) == (
        1,
        "forbidden third-party dependency notation -> numpy",
    )


def test_external_edges_skip_internal_relative_and_unrestricted_modules(tmp_path: Path) -> None:
    _score_tree(tmp_path)
    edges = read_external_edges(tmp_path / PACKAGE, tmp_path)
    targets = {(e.source, e.target) for e in edges}
    assert ("notation", "music21") in targets
    assert ("engraving", "cairosvg") in targets
    assert ("notation", "math") in targets
    assert not any(e.source in {"transcription", "analysis"} for e in edges)
    assert not any(e.target in {"stemstudio_worker", ""} for e in edges)
    assert all("/vendor/" not in e.path for e in edges)


def test_notation_importing_main_is_forbidden_and_a_cycle(tmp_path: Path) -> None:
    _score_tree(tmp_path)
    _module(tmp_path, "notation/wire.py", "from stemstudio_worker import main\n")
    summary = sorted((f.severity, f.message) for f in dependency_check.run(tmp_path))
    assert summary == [
        (Severity.ERROR, "cycle: main -> notation -> main"),
        (Severity.ERROR, "forbidden dependency notation -> main"),
    ]


# ------------------------------------------------------------------ vendored transcription code

LONG_UNIT = (
    "def infer(a, b, c, d, e, f, g):\n"
    + "".join(f"    if a == {i}:\n        b += {i}\n" for i in range(12))
    + "".join(f"    c{i} = {i}\n" for i in range(16))
    + "    return b\n"
)


@pytest.mark.parametrize(
    ("path", "expected"),
    [
        (f"{VENDOR}/basic_pitch/note_creation.py", True),
        (f"{VENDOR}/piano_transcription/models.py", True),
        (f"{VENDOR}/__init__.py", True),
        (f"{PACKAGE}/transcription/basic_pitch.py", False),
        (f"{PACKAGE}/notation/vendor_notes.py", False),
    ],
)
def test_transcription_vendor_is_excluded(path: str, expected: bool) -> None:
    assert is_excluded(path) is expected


def test_sig_check_skips_transcription_vendor(tmp_path: Path) -> None:
    _module(tmp_path, "transcription/vendor/piano_transcription/models.py", LONG_UNIT)
    _module(tmp_path, "transcription/piano.py", LONG_UNIT)
    result = sig_check.run(tmp_path, [tmp_path / "worker"])
    assert result.files == 1
    assert {f.path for f in result.errors} == {f"{PACKAGE}/transcription/piano.py"}


def test_jscpd_config_ignores_transcription_vendor() -> None:
    config = json.loads(duplication.CONFIG_PATH.read_text(encoding="utf-8"))
    assert "**/stemstudio_worker/transcription/vendor/**" in config["ignore"]


def test_sonar_excludes_both_vendor_folders() -> None:
    text = (REPO_ROOT / "sonar-project.properties").read_text(encoding="utf-8")
    exclusions = sonar_args.parse_properties(text)["sonar.exclusions"].split(",")
    assert f"{PACKAGE}/detail/vendor/**" in exclusions
    assert f"{VENDOR}/**" in exclusions


# ------------------------------------------------------------------ ownership and components


@pytest.mark.parametrize(
    ("path", "owner"),
    [
        (f"{PACKAGE}/transcription/basic_pitch.py", "stem-separation"),
        (f"{VENDOR}/basic_pitch/note_creation.py", "stem-separation"),
        ("worker/tests/transcription/test_basic_pitch.py", "stem-separation"),
        (f"{PACKAGE}/jobs/transcription_store.py", "stem-separation"),
        (f"{PACKAGE}/notation/timebase.py", "beat-analysis"),
        ("worker/tests/notation/test_timebase.py", "beat-analysis"),
        (f"{PACKAGE}/analysis/drum_onsets.py", "beat-analysis"),
        (f"{PACKAGE}/engraving/pdf.py", "audio-ingest"),
        ("worker/tests/engraving/test_engraver.py", "audio-ingest"),
    ],
)
def test_score_paths_are_owned(path: str, owner: str) -> None:
    assert owner_of(path) == owner
