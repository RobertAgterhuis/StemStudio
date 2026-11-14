import json
from pathlib import Path

import pytest

import sig_check
from common import Severity
from sig_check import Limits, UnitMetrics, evaluate_unit, measure_source

SAMPLES = Path(__file__).parent / "samples"
LANGS = ["py", "cs", "ts"]


def _measure(name: str) -> list[UnitMetrics]:
    path = SAMPLES / name
    return measure_source(f"src/{name}", path.read_text(encoding="utf-8"))


def _checks(name: str) -> set[tuple[str, Severity]]:
    findings = [f for u in _measure(name) for f in evaluate_unit(u, Limits())]
    return {(f.check, f.severity) for f in findings}


@pytest.mark.parametrize("lang", LANGS)
def test_ok_sample_has_no_findings(lang: str) -> None:
    units = _measure(f"ok.{lang}")
    assert len(units) == 1
    assert _checks(f"ok.{lang}") == set()


@pytest.mark.parametrize("lang", LANGS)
def test_long_unit_fails_unit_size(lang: str) -> None:
    assert _checks(f"long_unit.{lang}") == {("unit-size", Severity.ERROR)}


@pytest.mark.parametrize("lang", LANGS)
def test_complex_unit_fails_complexity(lang: str) -> None:
    assert ("complexity", Severity.ERROR) in _checks(f"complex_unit.{lang}")


@pytest.mark.parametrize("lang", LANGS)
def test_many_params_fails_interfacing(lang: str) -> None:
    assert _checks(f"many_params.{lang}") == {("parameters", Severity.ERROR)}


def _unit(nloc: int = 5, ccn: int = 1, params: int = 0, *, test: bool = False) -> UnitMetrics:
    return UnitMetrics("src/a.py", "f", 3, nloc, ccn, params, test)


@pytest.mark.parametrize(
    ("unit", "expected"),
    [
        (_unit(nloc=15), []),
        (_unit(nloc=16), [("unit-size", Severity.WARNING)]),
        (_unit(nloc=30), [("unit-size", Severity.WARNING)]),
        (_unit(nloc=31), [("unit-size", Severity.ERROR)]),
        (_unit(nloc=31, test=True), [("unit-size", Severity.WARNING)]),
        (_unit(ccn=5), []),
        (_unit(ccn=6), [("complexity", Severity.WARNING)]),
        (_unit(ccn=10), [("complexity", Severity.WARNING)]),
        (_unit(ccn=11), [("complexity", Severity.ERROR)]),
        (_unit(ccn=11, test=True), [("complexity", Severity.ERROR)]),
        (_unit(params=4), []),
        (_unit(params=5), [("parameters", Severity.ERROR)]),
    ],
)
def test_limits_boundaries(unit: UnitMetrics, expected: list[tuple[str, Severity]]) -> None:
    assert [(f.check, f.severity) for f in evaluate_unit(unit, Limits())] == expected


def test_test_code_unit_size_message_says_reported() -> None:
    (finding,) = evaluate_unit(_unit(nloc=40, test=True), Limits())
    assert "test code" in finding.message
    assert finding.line == 3


RAZOR = """@page "/x"
<h1>Title</h1>
@code {
    private int count;

    private void Inc(int a, int b, int c, int d, int e)
    {
        count += a;
    }
}
"""


def test_razor_code_block_is_measured_as_csharp() -> None:
    units = measure_source("src/StemStudio.Web/X.razor", RAZOR)
    assert [(u.name, u.line, u.params) for u in units] == [("Inc", 6, 5)]


def test_long_razor_code_block_is_flagged(tmp_path: Path) -> None:
    body = "\n".join(f"    private int f{i};" for i in range(31))
    text = "<p>x</p>\n@code {\n" + body + "\n}\n"
    findings = sig_check.evaluate_razor("src/X.razor", text, Limits())
    assert [(f.check, f.severity, f.line) for f in findings] == [
        ("razor-code", Severity.WARNING, 2)
    ]
    assert sig_check.evaluate_razor("src/X.razor", RAZOR, Limits()) == []


def _tree(tmp_path: Path) -> Path:
    files = {
        "src/App/long.py": (SAMPLES / "long_unit.py").read_text(encoding="utf-8"),
        "src/App/Page.razor": RAZOR.replace(", int e", ""),
        "src/App/bin/skip.py": (SAMPLES / "many_params.py").read_text(encoding="utf-8"),
        "src/App/Gen.g.cs": (SAMPLES / "many_params.cs").read_text(encoding="utf-8"),
        "src/App/notes.txt": "not code",
    }
    for rel, text in files.items():
        (tmp_path / rel).parent.mkdir(parents=True, exist_ok=True)
        (tmp_path / rel).write_text(text, encoding="utf-8")
    return tmp_path


def test_run_skips_generated_and_build_output(tmp_path: Path) -> None:
    root = _tree(tmp_path)
    result = sig_check.run(root, [root / "src"])
    assert result.files == 2
    assert {u.path for u in result.units} == {"src/App/long.py", "src/App/Page.razor"}
    assert [(f.path, f.severity) for f in result.errors] == [("src/App/long.py", Severity.ERROR)]
    assert result.warnings == []


def test_run_uses_default_roots(tmp_path: Path) -> None:
    root = _tree(tmp_path)
    assert sig_check.run(root, []).files == 2


def test_main_exit_code_and_json(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = _tree(tmp_path)
    out = tmp_path / "sig.json"
    code = sig_check.main(["--root", str(root), "--json", str(out), str(root / "src")])
    assert code == 1
    data = json.loads(out.read_text(encoding="utf-8"))
    assert data["files"] == 2
    assert data["findings"][0]["check"] == "unit-size"
    assert "1 errors" in capsys.readouterr().out


def test_main_passes_on_clean_file(capsys: pytest.CaptureFixture[str]) -> None:
    code = sig_check.main(["--root", str(SAMPLES), str(SAMPLES / "ok.py")])
    assert code == 0
    assert capsys.readouterr().out.startswith("sig_check: 1 files, 1 units, 0 errors")
