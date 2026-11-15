"""Feature 004: the TS packages ``src/web-interop`` and ``src/audio-engine`` stay independent and
``src/web-interop`` is measured by the SIG unit checks like the audio engine."""

from pathlib import Path

import pytest

import dependency_check
import sig_check
from common import Severity, is_production, is_test, owner_of
from components import COMPONENTS
from dependency_check import TS_ALLOWED, check_edges, read_ts_edges

SAMPLES = Path(__file__).parent / "samples"


def _ts(root: Path, rel: str, text: str) -> None:
    path = root / "src" / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def _packages(root: Path) -> None:
    for name in ("web-interop", "audio-engine"):
        _ts(root, f"{name}/package.json", "{}")


def _edges(root: Path) -> set[tuple[str, str]]:
    return {(e.source, e.target) for e in read_ts_edges(root / "src", root)}


@pytest.mark.parametrize(
    "statement",
    [
        "import { Engine } from '../../audio-engine/src/engine';",
        'import type { X } from "../../audio-engine/src/types";',
        "import '@stemstudio/audio-engine';",
        "import { y } from '@stemstudio/audio-engine/src/mixer';",
        "export * from '../../audio-engine/src/types';",
        "export { a } from '@stemstudio/audio-engine';",
        "const m = await import('../../audio-engine/src/engine');",
        "import {\n  a,\n  b,\n} from '@stemstudio/audio-engine';",
    ],
)
def test_web_interop_importing_audio_engine_is_an_edge(tmp_path: Path, statement: str) -> None:
    _packages(tmp_path)
    _ts(tmp_path, "web-interop/src/tour.ts", statement + "\n")
    assert _edges(tmp_path) == {("web-interop", "audio-engine")}


def test_audio_engine_importing_web_interop_is_an_edge(tmp_path: Path) -> None:
    _packages(tmp_path)
    _ts(tmp_path, "audio-engine/src/engine.ts", "import { x } from '@stemstudio/web-interop';\n")
    _ts(tmp_path, "audio-engine/tests/e.test.ts", "import '../../web-interop/src/index';\n")
    edges = read_ts_edges(tmp_path / "src", tmp_path)
    assert {(e.source, e.target) for e in edges} == {("audio-engine", "web-interop")}
    assert sorted((e.path, e.line) for e in edges) == [
        ("src/audio-engine/src/engine.ts", 1),
        ("src/audio-engine/tests/e.test.ts", 1),
    ]


@pytest.mark.parametrize(
    "statement",
    [
        "import { driver } from 'driver.js';",
        "import 'driver.js/dist/driver.css';",
        "import type { A } from './types';",
        "import { b } from '../src/focus';",
        "import { c } from '../../../../outside/repo';",
        "// import { x } from '@stemstudio/audio-engine';",
        "const s = 'import x from \"../../audio-engine/src/engine\"';",
    ],
)
def test_external_and_internal_imports_are_not_edges(tmp_path: Path, statement: str) -> None:
    _packages(tmp_path)
    _ts(tmp_path, "web-interop/src/tour.ts", statement + "\n")
    assert _edges(tmp_path) == set()


def test_generated_and_dependency_folders_contribute_no_edges(tmp_path: Path) -> None:
    _packages(tmp_path)
    _ts(tmp_path, "web-interop/node_modules/x/index.ts", "import '@stemstudio/audio-engine';\n")
    _ts(tmp_path, "web-interop/src/types.d.ts", "import '@stemstudio/audio-engine';\n")
    assert _edges(tmp_path) == set()


def test_folders_without_package_json_are_not_ts_packages(tmp_path: Path) -> None:
    _ts(tmp_path, "StemStudio.Web/x.ts", "import '@stemstudio/audio-engine';\n")
    assert _edges(tmp_path) == set()


def test_ts_rule_forbids_both_directions() -> None:
    assert {"web-interop": frozenset(), "audio-engine": frozenset()} == TS_ALLOWED


def test_run_fails_on_a_cross_package_import(tmp_path: Path) -> None:
    _packages(tmp_path)
    _ts(tmp_path, "web-interop/src/tour.ts", "import '@stemstudio/audio-engine';\n")
    findings = dependency_check.run(tmp_path)
    assert [(f.severity, f.path, f.line) for f in findings] == [
        (Severity.ERROR, "src/web-interop/src/tour.ts", 1)
    ]
    assert "web-interop -> audio-engine" in findings[0].message


def test_a_new_unmapped_ts_package_is_a_warning(tmp_path: Path) -> None:
    _packages(tmp_path)
    _ts(tmp_path, "new-pkg/package.json", "{}")
    _ts(tmp_path, "new-pkg/src/a.ts", "import '@stemstudio/audio-engine';\n")
    findings = check_edges(read_ts_edges(tmp_path / "src", tmp_path), TS_ALLOWED)
    assert [(f.severity, f.path) for f in findings] == [(Severity.WARNING, "new-pkg")]


def test_sig_units_of_web_interop_are_measured_and_breaches_fail(tmp_path: Path) -> None:
    for sample in ("long_unit.ts", "complex_unit.ts", "many_params.ts"):
        _ts(tmp_path, f"web-interop/src/{sample}", (SAMPLES / sample).read_text("utf-8"))
    result = sig_check.run(tmp_path, [])
    assert result.files == 3
    checks = {(f.check, f.path) for f in result.errors}
    assert {check for check, _ in checks} == {"unit-size", "complexity", "parameters"}
    assert all(path.startswith("src/web-interop/src/") for _, path in checks)


def test_web_interop_paths_are_classified_and_owned() -> None:
    assert is_production("src/web-interop/src/tour.ts")
    assert is_test("src/web-interop/tests/tour.test.ts")
    assert owner_of("src/web-interop/src/types.ts") == "architect"
    assert owner_of("src/web-interop/src/tour.ts") == "frontend"
    assert owner_of("src/web-interop/tests/tour.test.ts") == "frontend"


def test_web_interop_is_a_reported_component() -> None:
    (component,) = [c for c in COMPONENTS if c.name == "web-interop"]
    assert component.contains("src/web-interop/src/tour.ts", test=False)
    assert not component.contains("src/audio-engine/src/engine.ts", test=False)
