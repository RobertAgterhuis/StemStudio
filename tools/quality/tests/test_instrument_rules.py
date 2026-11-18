"""Feature 016 (T009): quality rules of the instrument views.

Worker layering (``dependency_check``): ``harmony.{postprocess,labels,chroma_hmm,key}`` import only
``ports``, ``schemas``, each other and (third party) numpy, librosa; ``harmony.btc`` may also
import torch and ``harmony.vendor``; ``notation`` imports neither ``harmony`` nor
``transcription``; ``views_fake`` (the fake analyses) imports only ports, schemas, config and the
named fake helpers; ``analysis_inputs`` is an adapter. ``harmony/vendor/**`` is third-party code:
excluded from SIG, duplication and Sonar, no edges, but it keeps LICENSE and NOTICE.

``instrument_rules``: no ``weights_only=False`` anywhere in ``worker/src`` (vendor included); the
engine's ``src/pitch/**`` uses no DOM or Web Audio globals; nothing below
``src/audio-engine/src`` imports ``engine.ts`` except ``index.ts``.
"""

from pathlib import Path

import pytest
from repo_tree import WORKER_PACKAGE as PACKAGE
from repo_tree import error_findings, write_file, write_module

import instrument_rules
import run_all
import vendor_check
from common import Severity, is_excluded
from dependency_check import PYTHON_ALLOWED
from duplication import DuplicationResult

ENGINE = "src/audio-engine/src"


def _messages(root: Path) -> list[tuple[str, int, str]]:
    return [(f.path, f.line, f.message) for f in instrument_rules.run(root)]


# ------------------------------------------------------------------ worker layering


@pytest.mark.parametrize("name", ["postprocess", "labels", "chroma_hmm", "key"])
def test_harmony_core_modules_may_import_ports_schemas_numpy_librosa_and_each_other(
    tmp_path: Path, name: str
) -> None:
    write_module(
        tmp_path,
        f"harmony/{name}.py",
        "import math\nimport numpy as np\nimport librosa\n"
        "from stemstudio_worker.ports import ChordFrames\n"
        "from stemstudio_worker.schemas import ChordChart\n"
        "from stemstudio_worker.harmony.labels import parse\n",
    )

    assert error_findings(tmp_path) == []


@pytest.mark.parametrize(
    ("source", "target"),
    [
        ("import torch\n", "torch"),
        ("import scipy.signal\n", "scipy"),
        ("from stemstudio_worker.harmony.btc import BtcRecognizer\n", "harmony.btc"),
        ("from stemstudio_worker.harmony.vendor.btc import labels\n", "harmony.vendor"),
        ("from stemstudio_worker import config\n", "config"),
        ("from stemstudio_worker.transcription import fake\n", "transcription"),
    ],
)
def test_harmony_core_modules_import_nothing_else(tmp_path: Path, source: str, target: str) -> None:
    write_module(tmp_path, "harmony/key.py", source)

    errors = error_findings(tmp_path)

    assert errors and all(path.endswith("harmony/key.py") for path, _ in errors)
    assert any(target in message for _, message in errors)


def test_harmony_btc_may_import_torch_librosa_and_its_vendor(tmp_path: Path) -> None:
    write_module(
        tmp_path,
        "harmony/btc.py",
        "import importlib\nimport numpy\nimport librosa\nimport torch\n"
        "from stemstudio_worker.ports import BTC_GPU_NEED\n"
        "from stemstudio_worker.schemas import ChordEngine\n"
        "from stemstudio_worker.harmony.vendor.btc import btc_model\n",
    )

    assert error_findings(tmp_path) == []


def test_harmony_btc_imports_no_transcription_model_store(tmp_path: Path) -> None:
    write_module(tmp_path, "harmony/btc.py",
                 "from stemstudio_worker.transcription.models import ModelStore\n")  # fmt: skip

    assert [m for _, m in error_findings(tmp_path)] != []


@pytest.mark.parametrize("target", ["harmony", "transcription"])
def test_notation_imports_neither_harmony_nor_transcription(tmp_path: Path, target: str) -> None:
    write_module(tmp_path, "notation/parts.py", f"from stemstudio_worker.{target} import x\n")

    errors = error_findings(tmp_path)

    assert len(errors) == 1 and target in errors[0][1]


def test_views_fake_may_import_its_named_helpers(tmp_path: Path) -> None:
    write_module(
        tmp_path,
        "views_fake.py",
        "import numpy\nimport soundfile\n"
        "from stemstudio_worker.ports import DrumHit\n"
        "from stemstudio_worker.schemas import ChordChart\n"
        "from stemstudio_worker.jobs.instrument_analysis_ports import ChordInputs\n"
        "from stemstudio_worker.lyrics.fake import FAKE_LINES\n"
        "from stemstudio_worker.lyrics.interpolate import line_end_ms\n"
        "from stemstudio_worker.transcription.audio import AUDIO_UNREADABLE_TEXT\n"
        "from stemstudio_worker.transcription.fake import FakeDrumTranscriber\n",
    )

    assert error_findings(tmp_path) == []


@pytest.mark.parametrize(
    "source",
    [
        "from stemstudio_worker.jobs.instrument_analysis_service import X\n",
        "from stemstudio_worker.transcription.piano import X\n",
        "from stemstudio_worker.harmony.btc import X\n",
        "from stemstudio_worker.api import routes\n",
    ],
)
def test_views_fake_imports_nothing_else(tmp_path: Path, source: str) -> None:
    write_module(tmp_path, "views_fake.py", source)

    assert len(error_findings(tmp_path)) == 1


def test_jobs_and_api_import_no_harmony_and_no_views_fake(tmp_path: Path) -> None:
    write_module(tmp_path, "jobs/instrument_analysis_service.py",
                 "from stemstudio_worker.harmony import btc\n")  # fmt: skip
    write_module(tmp_path, "api/instrument_analysis_routes.py",
                 "from stemstudio_worker import views_fake\n")  # fmt: skip

    assert len(error_findings(tmp_path)) == 2


def test_the_new_worker_modules_are_in_the_layer_map() -> None:
    assert {"harmony", "views_fake", "analysis_inputs"} <= set(PYTHON_ALLOWED)
    assert PYTHON_ALLOWED["analysis_inputs"] == frozenset({"ports", "schemas", "config"})


def test_main_may_compose_harmony_and_the_fakes(tmp_path: Path) -> None:
    write_module(
        tmp_path,
        "main.py",
        "from stemstudio_worker.harmony.btc import BtcRecognizer\n"
        "from stemstudio_worker.views_fake import FakeChordAnalyzer\n",
    )

    assert error_findings(tmp_path) == []


# ------------------------------------------------------------------ the vendored BTC subset


def test_the_btc_vendor_is_excluded_and_licence_checked(tmp_path: Path) -> None:
    assert is_excluded(f"{PACKAGE}/harmony/vendor/btc/btc_model.py")
    assert not is_excluded(f"{PACKAGE}/harmony/btc.py")
    assert f"{PACKAGE}/harmony/vendor" in vendor_check.VENDOR_ROOTS
    write_module(tmp_path, "harmony/vendor/btc/btc_model.py", "import torch\n")
    write_module(tmp_path, "harmony/vendor/btc/LICENSE", "MIT\n")

    findings = vendor_check.run(tmp_path)

    assert [f.message for f in findings] == [
        "vendored package harmony/vendor/btc has no NOTICE file"
    ]


def test_the_vendor_contributes_no_edges(tmp_path: Path) -> None:
    write_module(tmp_path, "harmony/vendor/btc/btc_model.py",
                 "import pandas\nfrom stemstudio_worker.api import routes\n")  # fmt: skip

    assert error_findings(tmp_path) == []


# ------------------------------------------------------------------ weights_only=False


@pytest.mark.parametrize(
    "line",
    [
        "state = torch.load(path, weights_only=False)\n",
        "state = torch.load(path, weights_only = False)\n",
        "state = torch.load(path, map_location='cpu',\n    weights_only=False)\n",
    ],
)
def test_weights_only_false_is_an_error_anywhere_in_the_worker(tmp_path: Path, line: str) -> None:
    write_module(tmp_path, "harmony/vendor/btc/load.py", "import torch\n" + line)

    messages = _messages(tmp_path)

    assert len(messages) == 1
    path, number, message = messages[0]
    assert path == f"{PACKAGE}/harmony/vendor/btc/load.py" and number >= 2
    assert "weights_only=False" in message


def test_weights_only_true_and_mentions_in_tests_are_fine(tmp_path: Path) -> None:
    write_module(tmp_path, "harmony/btc.py", "torch.load(path, weights_only=True)\n")
    write_file(tmp_path, "worker/tests/test_x.py", "assert 'weights_only=False' not in text\n")

    assert _messages(tmp_path) == []


def test_findings_are_errors_of_the_instrument_check(tmp_path: Path) -> None:
    write_module(tmp_path, "x.py", "torch.load(p, weights_only=False)\n")

    finding = instrument_rules.run(tmp_path)[0]

    assert (finding.check, finding.severity) == ("instrument", Severity.ERROR)


# ------------------------------------------------------------------ engine pitch scope


@pytest.mark.parametrize(
    "global_name",
    ["window", "document", "navigator", "AudioContext", "OfflineAudioContext", "AudioWorkletNode",
     "MediaStream", "requestAnimationFrame", "HTMLCanvasElement", "localStorage"],
)  # fmt: skip
def test_pitch_code_uses_no_dom_or_web_audio_globals(tmp_path: Path, global_name: str) -> None:
    write_file(tmp_path, f"{ENGINE}/pitch/yin.ts", f"export const x = {global_name};\n")

    messages = _messages(tmp_path)

    assert len(messages) == 1 and global_name in messages[0][2]
    assert messages[0][0] == f"{ENGINE}/pitch/yin.ts"


def test_pitch_code_may_mention_globals_in_comments_strings_and_members(tmp_path: Path) -> None:
    write_file(
        tmp_path,
        f"{ENGINE}/pitch/worklet.ts",
        "// window and document are not used here\n"
        "/* AudioContext lives in the engine */\n"
        "const label = 'navigator';\n"
        "const scope = globalThis as unknown as { registerProcessor: unknown };\n"
        "const n = options.window;\n"
        "const audioContextRate = 48000;\n",
    )

    assert _messages(tmp_path) == []


def test_other_engine_folders_may_use_web_audio(tmp_path: Path) -> None:
    write_file(tmp_path, f"{ENGINE}/views/lane.ts", "const c = new AudioContext(); window.x;\n")

    assert _messages(tmp_path) == []


# ------------------------------------------------------------------ engine.ts imports


def test_only_index_imports_the_engine(tmp_path: Path) -> None:
    write_file(tmp_path, "src/audio-engine/package.json", "{}")
    write_file(tmp_path, f"{ENGINE}/engine.ts", "import { x } from './recording/input';\n")
    write_file(tmp_path, f"{ENGINE}/index.ts", "import { Engine } from './engine';\n")
    write_file(tmp_path, f"{ENGINE}/views/chords.ts", "import { x } from './engine';\n")

    assert error_findings(tmp_path) == []


@pytest.mark.parametrize(
    ("rel", "source"),
    [
        ("views/chords.ts", "import { Engine } from '../engine';\n"),
        ("pitch/scorer.ts", 'import type { Engine } from "../engine.js";\n'),
        ("mixer.ts", "export { Engine } from './engine';\n"),
        ("drums/lanes.ts", "const m = await import('../engine');\n"),
    ],
)
def test_any_other_file_importing_the_engine_is_an_error(
    tmp_path: Path, rel: str, source: str
) -> None:
    write_file(tmp_path, "src/audio-engine/package.json", "{}")
    write_file(tmp_path, f"{ENGINE}/engine.ts", "export class Engine {}\n")
    write_file(tmp_path, f"{ENGINE}/{rel}", source)

    errors = error_findings(tmp_path)

    assert len(errors) == 1 and errors[0][0] == f"{ENGINE}/{rel}"
    assert "audio-engine/engine" in errors[0][1]


# ------------------------------------------------------------------ run_all


def test_run_all_reports_instrument_findings(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    write_module(tmp_path, "x.py", "torch.load(p, weights_only=False)\n")
    clean = DuplicationResult(clones=[], duplicated_percentage=0.0, findings=[], tool_error=None)
    report = tmp_path / "r.md"

    code = run_all.main(["--root", str(tmp_path), "--report", str(report)], lambda _r, _p: clean)

    assert code == 1
    assert "[instrument]" in capsys.readouterr().out
    assert "## Instrument views\n\n**FAIL** - 1 errors" in report.read_text(encoding="utf-8")


def test_the_real_repository_is_clean() -> None:
    from common import REPO_ROOT  # noqa: PLC0415

    assert instrument_rules.run(REPO_ROOT) == []


def test_generated_folders_are_not_checked(tmp_path: Path) -> None:
    write_module(tmp_path, "__pycache__/x.py", "torch.load(p, weights_only=False)\n")
    write_file(tmp_path, f"{ENGINE}/pitch/node_modules/y.ts", "window.x = 1;\n")

    assert _messages(tmp_path) == []


def test_main_returns_the_exit_code(tmp_path: Path) -> None:
    assert instrument_rules.main(["--root", str(tmp_path)]) == 0
    write_module(tmp_path, "x.py", "torch.load(p, weights_only=False)\n")
    assert instrument_rules.main(["--root", str(tmp_path)]) == 1
