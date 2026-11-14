from common import Finding, Severity
from priorities import impact, render_top, top_warnings


def _warning(check: str, path: str, excess: int, line: int = 1) -> Finding:
    return Finding(check, Severity.WARNING, path, line, f"{check} msg", excess)


def test_impact_weights_check_and_production() -> None:
    assert impact(_warning("complexity", "src/StemStudio.Web/A.cs", 4)) == 24.0
    assert impact(_warning("complexity", "tests/X.Tests/A.cs", 4)) == 12.0
    assert impact(_warning("unit-size", "src/StemStudio.Api/A.cs", 3)) == 6.0
    assert impact(_warning("razor-code", "src/StemStudio.Web/A.razor", 10)) == 10.0
    assert impact(_warning("duplication", "tests/a.cs", 7)) == 7.0
    assert impact(_warning("exemption", "src/a.cs", 0)) == 0.0


def test_top_warnings_skips_errors_and_orders_by_impact() -> None:
    error = Finding("unit-size", Severity.ERROR, "src/StemStudio.Api/E.cs", 1, "x", 99)
    findings = [
        _warning("unit-size", "src/StemStudio.Api/A.cs", 1),
        _warning("complexity", "src/StemStudio.Web/B.cs", 4),
        _warning("unit-size", "src/StemStudio.Api/C.cs", 1, line=2),
        error,
    ]
    top = top_warnings(findings, 2)
    assert [f.path for f in top] == ["src/StemStudio.Web/B.cs", "src/StemStudio.Api/A.cs"]


def test_render_top_lists_owner_and_remedy() -> None:
    text = render_top([_warning("complexity", "src/StemStudio.Web/Mixer/C.cs", 4, line=31)])
    assert "## Top 10 refactor requests" in text
    assert "| 1 | frontend | `src/StemStudio.Web/Mixer/C.cs:31` | complexity msg | 24 |" in text
    assert "guard clauses" in text


def test_render_top_without_warnings() -> None:
    assert render_top([]).endswith("None.")
