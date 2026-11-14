from pathlib import Path

import pytest

import sig_check
from common import Severity
from sig_check import Exemption, Limits, UnitMetrics, evaluate_unit, find_marker, measure_source

SAMPLES = Path(__file__).parent / "samples"
LOGGER = "tests/StemStudio.Api.Tests/TestSupport/ListLoggerProvider.cs"


def _units() -> dict[str, UnitMetrics]:
    text = (SAMPLES / "exempt_logger.cs").read_text(encoding="utf-8")
    return {u.name.split("::")[-1]: u for u in measure_source(LOGGER, text)}


def _checks(unit: UnitMetrics) -> list[tuple[str, Severity]]:
    return [(f.check, f.severity) for f in evaluate_unit(unit, Limits())]


def test_marker_above_signature_exempts_parameters_only() -> None:
    unit = _units()["Log<TState>"]
    assert unit.params == 5
    assert unit.exempt_reason == "interface-mandated signature (ILogger.Log)"
    assert _checks(unit) == []


def test_marker_without_reason_is_an_error_and_does_not_exempt() -> None:
    unit = _units()["NoReason"]
    assert unit.exempt_reason == ""
    assert _checks(unit) == [("exemption", Severity.ERROR), ("parameters", Severity.ERROR)]


def test_unneeded_marker_is_a_warning() -> None:
    assert _checks(_units()["NotNeeded"]) == [("exemption", Severity.WARNING)]


def test_marker_two_lines_above_is_ignored() -> None:
    unit = _units()["TooFar"]
    assert unit.exempt_reason is None
    assert _checks(unit) == [("parameters", Severity.ERROR)]


def test_exemption_never_covers_size_or_complexity() -> None:
    unit = UnitMetrics("src/a.cs", "f", 3, 40, 12, 6, False, "framework")
    assert _checks(unit) == [("unit-size", Severity.ERROR), ("complexity", Severity.ERROR)]


@pytest.mark.parametrize(
    ("lines", "expected"),
    [
        (["def f(a, b, c, d, e):  # sig-exempt: same line"], "same line"),
        (["/* sig-exempt: block comment */", "void F() {}"], "block comment"),
        (["@* sig-exempt: razor *@", "void F() {}"], "razor"),
        (['var s = "sig-exempt: in a string";', "void F() {}"], None),
        (["void F() {}"], None),
    ],
)
def test_find_marker(lines: list[str], expected: str | None) -> None:
    assert find_marker(lines, len(lines)) == expected


def test_find_marker_ignores_lines_outside_the_file() -> None:
    assert find_marker(["# sig-exempt: x"], 0) is None
    assert find_marker([], 5) is None


def test_python_same_line_marker() -> None:
    text = "def handler(a, b, c, d, e):  # sig-exempt: callback signature\n    return a\n"
    (unit,) = measure_source("worker/src/x.py", text)
    assert unit.exempt_reason == "callback signature"
    assert _checks(unit) == []


def _logger_tree(tmp_path: Path) -> Path:
    target = tmp_path / "tests" / "Api.Tests" / "ListLogger.cs"
    target.parent.mkdir(parents=True)
    target.write_text((SAMPLES / "exempt_logger.cs").read_text(encoding="utf-8"), encoding="utf-8")
    return tmp_path


def test_result_lists_exemptions_with_a_reason(tmp_path: Path) -> None:
    root = _logger_tree(tmp_path)
    result = sig_check.run(root, [root / "tests"])
    logger = "tests/Api.Tests/ListLogger.cs"
    assert result.exemptions == [
        Exemption(
            logger, 7, "ListLogger::Log<TState>", "interface-mandated signature (ILogger.Log)"
        ),
        Exemption(logger, 14, "ListLogger::NotNeeded", "kept for symmetry"),
    ]


def test_exemption_str() -> None:
    exemption = Exemption("a.cs", 7, "L::Log", "interface-mandated")
    assert str(exemption) == "a.cs:7 L::Log - interface-mandated"


def test_main_prints_exemptions(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    target = tmp_path / "src" / "x.py"
    target.parent.mkdir(parents=True)
    target.write_text("def f(a, b, c, d, e):  # sig-exempt: callback\n    return a\n", "utf-8")
    assert sig_check.main(["--root", str(tmp_path), str(target)]) == 0
    out = capsys.readouterr().out
    assert "exemption: src/x.py:1 f - callback" in out
    assert "0 errors, 0 warnings, 1 exemptions" in out


SWITCH_EXPRESSION = """class C
{
    static int U(int t) => t switch
    {
        1 => 2,
        _ => 3,
    };

    void F() { }
}
"""


def test_lizard_global_pseudo_unit_is_not_measured() -> None:
    # lizard puts leftover tokens after a C# switch expression into a "*global*" pseudo-unit.
    names = [u.name for u in measure_source("src/G.cs", SWITCH_EXPRESSION)]
    assert names == ["C::U", "C::F"]
