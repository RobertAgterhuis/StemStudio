"""Feature 012: quality rules for practice tempo and the SoundTouch module (plan.md, T008).

Engine (module level): ``transport``, ``mixer``, ``metronome/`` and ``karaoke/`` never import
``practice/`` or ``stretch/``; ``practice/`` never imports ``karaoke/``.

Worklet scope: ``stretch/processor.ts`` imports only the worklet modules
``stretch/{constants, params, fifo, abi, core}`` (``core`` added by audio-engine, see the
quality-gate 012 hand-off); the other worklet modules value-import only each other (``import
type`` is erased by the build and may point anywhere).

C++: the shim below ``third_party/soundtouch/shim/`` is our code and measured like any unit;
the downloaded upstream source and the build output are third-party / generated and ignored.
"""

import json
from pathlib import Path

import pytest
from repo_tree import write_file

import components
import dependency_check
import sig_check
import sonar_args
from common import (
    REPO_ROOT,
    Severity,
    default_paths,
    is_excluded,
    is_production,
    iter_source_files,
    owner_of,
    relative,
)
from dependency_check import TS_MODULE_FORBIDDEN, TS_WORKLET

ENGINE = "src/audio-engine"
SHIM = "third_party/soundtouch/shim/stemstudio_soundtouch.cpp"

CLEAN_TREE = {
    "package.json": "{}",
    "src/engine.ts": "import { PracticeController } from './practice/controller';\n",
    "src/transport.ts": "import { FrameLoop } from './frame-loop';\n",
    "src/mixer.ts": "export const mixer = 1;\n",
    "src/metronome/timeline.ts": "import type { TransportClock } from '../transport';\n",
    "src/karaoke/clock.ts": "import type { TransportClock } from '../transport';\n",
    "src/practice/controller.ts": (
        "import { WorkletStage } from '../stretch/worklet-stage';\n"
        "import { tempo } from './tempo';\n"
        "import type { TransportClock } from '../transport';\n"
    ),
    "src/stretch/worklet-stage.ts": "import { STRETCH_PROCESSOR_NAME } from './constants';\n",
    "src/stretch/processor.ts": (
        "import { SoundTouchStream, type AbiLoader } from './abi';\n"
        "import { RENDER_QUANTUM_FRAMES } from './constants';\n"
        "import { StretchCore } from './core';\n"
        "import { delayFrames } from './params';\n"
        "import { DelayFifo } from './fifo';\n"
    ),
    "src/stretch/abi.ts": "import { STRETCH_ABI_VERSION } from './constants';\n",
    "src/stretch/core.ts": "import { DelayFifo } from './fifo';\n",
    "src/stretch/fifo.ts": "import { STRETCH_OVERFLOW_FRAMES } from './constants';\n",
    "src/stretch/params.ts": "import type { PracticeSettings } from '../types';\n",
    "src/stretch/constants.ts": "export const STRETCH_DELAY_FRAMES = 5120;\n",
}


def _engine_tree(root: Path, overrides: dict[str, str] | None = None) -> None:
    for rel, text in {**CLEAN_TREE, **(overrides or {})}.items():
        write_file(root, f"{ENGINE}/{rel}", text)


def _errors(root: Path) -> list[tuple[str, str]]:
    return [
        (f.path, f.message) for f in dependency_check.run(root) if f.severity is Severity.ERROR
    ]


# ------------------------------------------------------------------ engine module rules


@pytest.mark.parametrize(
    ("source", "targets"),
    [
        ("audio-engine/transport", {"audio-engine/practice", "audio-engine/stretch"}),
        ("audio-engine/mixer", {"audio-engine/practice", "audio-engine/stretch"}),
        ("audio-engine/metronome", {"audio-engine/practice", "audio-engine/stretch"}),
        ("audio-engine/karaoke", {"audio-engine/practice", "audio-engine/stretch"}),
        ("audio-engine/practice", {"audio-engine/karaoke"}),
    ],
)
def test_practice_module_rules_are_mapped(source: str, targets: set[str]) -> None:
    assert targets <= TS_MODULE_FORBIDDEN[source]


def test_clean_practice_tree_has_no_errors(tmp_path: Path) -> None:
    _engine_tree(tmp_path)
    assert _errors(tmp_path) == []


@pytest.mark.parametrize(
    ("rel", "text", "target"),
    [
        ("src/transport.ts", "import { rate } from './practice/tempo';\n", "practice"),
        ("src/transport.ts", "import type { StretchState } from './stretch/params';\n", "stretch"),
        ("src/mixer.ts", "import { WorkletStage } from './stretch/worklet-stage';\n", "stretch"),
        ("src/metronome/timeline.ts", "import { x } from '../practice/count-in';\n", "practice"),
        ("src/karaoke/clock.ts", "import { x } from '../practice/settings';\n", "practice"),
        ("src/practice/passes.ts", "import { x } from '../karaoke/clock';\n", "karaoke"),
    ],
)
def test_forbidden_practice_edges_fail(tmp_path: Path, rel: str, text: str, target: str) -> None:
    _engine_tree(tmp_path, {rel: text})
    errors = _errors(tmp_path)
    assert len(errors) == 1
    assert errors[0][0] == f"{ENGINE}/{rel}"
    assert errors[0][1].endswith(f"-> audio-engine/{target}")


# ------------------------------------------------------------------ worklet scope


def test_worklet_modules_are_the_plan_list_plus_core() -> None:
    assert set(TS_WORKLET.modules) == {"processor", "constants", "params", "fifo", "abi", "core"}
    assert TS_WORKLET.folder == "stretch"
    assert TS_WORKLET.entry == "processor"


@pytest.mark.parametrize(
    ("rel", "text", "target"),
    [
        ("src/stretch/processor.ts", "import { x } from './worklet-stage';\n", "worklet-stage"),
        ("src/stretch/processor.ts", "import { x } from '../transport';\n", "transport"),
        ("src/stretch/processor.ts", "import type { PracticeSettings } from '../types';\n", "types"),
        ("src/stretch/processor.ts", "import { clamp } from 'lodash-es';\n", "lodash-es"),
        ("src/stretch/processor.ts", "const m = await import('./fake-stage');\n", "fake-stage"),
        ("src/stretch/core.ts", "import { engine } from '../engine';\n", "engine"),
        ("src/stretch/fifo.ts", "export { x } from '../practice/tempo';\n", "practice/tempo"),
        ("src/stretch/params.ts", "import { DEFAULTS } from '../types';\n", "types"),
    ],
)
def test_worklet_imports_outside_the_worklet_fail(
    tmp_path: Path, rel: str, text: str, target: str
) -> None:
    _engine_tree(tmp_path, {rel: text})
    errors = [e for e in _errors(tmp_path) if "worklet" in e[1]]
    assert errors == [
        (f"{ENGINE}/{rel}", f"worklet scope: {rel.split('/')[-1]} must not import {target}")
    ]


def test_type_only_imports_of_non_entry_worklet_modules_are_allowed(tmp_path: Path) -> None:
    _engine_tree(tmp_path, {"src/stretch/abi.ts": "import type { X } from '../types';\n"})
    assert _errors(tmp_path) == []


def test_worklet_check_ignores_a_missing_engine(tmp_path: Path) -> None:
    assert dependency_check.check_worklet(tmp_path / "src", tmp_path) == []


def test_worklet_findings_belong_to_audio_engine(tmp_path: Path) -> None:
    _engine_tree(tmp_path, {"src/stretch/processor.ts": "import { x } from '../engine';\n"})
    owners = {f.owner for f in dependency_check.run(tmp_path) if "worklet" in f.message}
    assert owners == {"audio-engine"}


# ------------------------------------------------------------------ C++ shim measurement


def _cpp_function(name: str, body_lines: int) -> str:
    body = "".join(f"    total += {i};\n" for i in range(body_lines))
    return f"int {name}(int a) {{\n    int total = a;\n{body}    return total;\n}}\n"


def test_cpp_units_are_measured() -> None:
    units = sig_check.measure_source(SHIM, _cpp_function("st_put", 3))
    assert [(u.name, u.nloc, u.is_test) for u in units] == [("st_put", 6, False)]


def test_long_shim_unit_is_a_hard_error(tmp_path: Path) -> None:
    write_file(tmp_path, SHIM, _cpp_function("st_receive", 30))
    result = sig_check.run(tmp_path, [])
    assert [(f.check, f.severity) for f in result.findings] == [("unit-size", Severity.ERROR)]
    assert result.findings[0].owner == "audio-engine"


def test_upstream_and_build_output_are_not_measured(tmp_path: Path) -> None:
    write_file(tmp_path, SHIM, _cpp_function("st_put", 2))
    write_file(tmp_path, "third_party/soundtouch/upstream/source/TDStretch.cpp", "int f(){}\n")
    write_file(tmp_path, "third_party/soundtouch/build/gen.cpp", "int g(){}\n")
    write_file(tmp_path, "third_party/soundtouch/include/stemstudio_soundtouch.h", "int h();\n")
    found = {relative(p, tmp_path) for p in iter_source_files(tmp_path, default_paths(tmp_path))}
    assert found == {SHIM, "third_party/soundtouch/include/stemstudio_soundtouch.h"}


@pytest.mark.parametrize(
    "path",
    [
        "third_party/soundtouch/upstream/source/SoundTouch/TDStretch.cpp",
        "third_party/soundtouch/build/soundtouch.wasm",
        "src/StemStudio.Web/wwwroot/js/time-stretch-processor.js",
        "src/StemStudio.Web/wwwroot/js/time-stretch-processor.js.map",
        "src/StemStudio.Web/wwwroot/js/soundtouch/soundtouch.wasm",
        "src/StemStudio.Web/wwwroot/third-party/soundtouch/index.html",
    ],
)
def test_third_party_and_build_outputs_are_excluded(path: str) -> None:
    assert is_excluded(path)


def test_shim_and_header_are_measured_production_code() -> None:
    assert not is_excluded(SHIM)
    assert is_production(SHIM)
    assert is_production("third_party/soundtouch/include/stemstudio_soundtouch.h")


@pytest.mark.parametrize(
    ("path", "owner"),
    [
        (SHIM, "audio-engine"),
        ("third_party/soundtouch/include/stemstudio_soundtouch.h", "architect"),
        ("third_party/soundtouch/soundtouch.lock", "architect"),
        ("src/audio-engine/src/stretch/processor.ts", "audio-engine"),
        ("src/audio-engine/src/practice/controller.ts", "audio-engine"),
        ("src/StemStudio.Web/Components/Mixer/Practice/PracticePanel.razor", "frontend"),
        ("scripts/build-soundtouch.sh", "devops"),
    ],
)
def test_practice_paths_are_owned(path: str, owner: str) -> None:
    assert owner_of(path) == owner


def test_jscpd_scans_cpp_and_ignores_upstream() -> None:
    config = json.loads((REPO_ROOT / "tools/quality/.jscpd.json").read_text(encoding="utf-8"))
    assert "cpp" in config["format"]
    assert "**/third_party/soundtouch/upstream/**" in config["ignore"]
    assert "**/third_party/soundtouch/build/**" in config["ignore"]


# ------------------------------------------------------------------ components and Sonar


def test_practice_components_are_reported() -> None:
    names = {c.name: c.prefixes for c in components.COMPONENTS}
    assert names["audio-engine/stretch"] == ("src/audio-engine/src/stretch/",)
    assert names["audio-engine/practice"] == ("src/audio-engine/src/practice/",)
    assert names["soundtouch shim"] == ("third_party/soundtouch/shim/",)


def _sonar() -> dict[str, str]:
    text = (REPO_ROOT / "sonar-project.properties").read_text(encoding="utf-8")
    return sonar_args.parse_properties(text)


SONAR_EXCLUDED = (
    "third_party/soundtouch/upstream/**",
    "third_party/soundtouch/build/**",
    "src/StemStudio.Web/wwwroot/js/time-stretch-processor.js",
    "src/StemStudio.Web/wwwroot/js/time-stretch-processor.js.map",
    "src/StemStudio.Web/wwwroot/js/soundtouch/**",
    "src/StemStudio.Web/wwwroot/third-party/**",
)


@pytest.mark.parametrize("pattern", SONAR_EXCLUDED)
def test_sonar_excludes_third_party_and_build_output(pattern: str) -> None:
    props = _sonar()
    assert pattern in props["sonar.exclusions"].split(",")
    assert pattern in props["sonar.cpd.exclusions"].split(",")


def test_sonar_keeps_the_shim_measured() -> None:
    props = _sonar()
    assert "third_party/soundtouch" in props["sonar.sources"].split(",")
    excluded = props["sonar.exclusions"].split(",")
    assert not any(p.startswith(("third_party/soundtouch/shim", "**/*.cpp")) for p in excluded)
