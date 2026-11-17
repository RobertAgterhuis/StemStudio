from collections.abc import Sequence
from pathlib import Path

import pytest

import run_all
from duplication import Clone, DuplicationResult, evaluate

SAMPLES = Path(__file__).parent / "samples"


def _repo(tmp_path: Path, sample: str = "ok.py") -> Path:
    target = tmp_path / "src" / "App" / "code.py"
    target.parent.mkdir(parents=True)
    target.write_text((SAMPLES / sample).read_text(encoding="utf-8"), encoding="utf-8")
    csproj = tmp_path / "src" / "StemStudio.Web" / "StemStudio.Web.csproj"
    csproj.parent.mkdir(parents=True)
    csproj.write_text("<Project />", encoding="utf-8")
    return tmp_path


def _dup(result: DuplicationResult) -> run_all.DuplicationRun:
    def fake(root: Path, paths: Sequence[Path]) -> DuplicationResult:
        return result

    return fake


CLEAN = DuplicationResult(duplicated_percentage=0.0)


def test_clean_repo_passes_and_writes_report(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    root = _repo(tmp_path)
    report = tmp_path / "docs" / "quality" / "SIG-REPORT.md"
    code = run_all.main(["--root", str(root), "--report", str(report)], _dup(CLEAN))
    assert code == 0
    text = report.read_text(encoding="utf-8")
    assert "**Verdict: PASS**" in text
    assert "1 files, 1 units measured (1 in production code)" in text
    assert "exit 0" in capsys.readouterr().out


def test_hard_breach_exits_one(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = _repo(tmp_path, "many_params.py")
    assert run_all.main(["--root", str(root)], _dup(CLEAN)) == 1
    out = capsys.readouterr().out
    assert "[parameters]" in out
    assert "1 errors" in out


def test_production_clone_exits_one(tmp_path: Path) -> None:
    clones = [Clone("src/App/a.py", 1, "src/App/b.py", 9, 6)]
    result = run_all.run(_repo(tmp_path), [], _dup(DuplicationResult(clones, evaluate(clones))))
    assert result.exit_code == 1
    assert [f.check for f in result.findings] == ["duplication"]


def test_tool_error_exits_two(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    root = _repo(tmp_path)
    broken = DuplicationResult(tool_error="npx not found")
    report = tmp_path / "r.md"
    assert run_all.main(["--root", str(root), "--report", str(report)], _dup(broken)) == 2
    assert "| Duplication | n/a |" in report.read_text(encoding="utf-8")
    assert "tool error: npx not found" in capsys.readouterr().out


def test_dependency_violation_exits_one(tmp_path: Path) -> None:
    root = _repo(tmp_path)
    csproj = root / "src" / "StemStudio.Web" / "StemStudio.Web.csproj"
    csproj.write_text(
        '<Project><ItemGroup><ProjectReference Include="..\\StemStudio.Persistence\\'
        'StemStudio.Persistence.csproj" /></ItemGroup></Project>',
        encoding="utf-8",
    )
    result = run_all.run(root, [], _dup(CLEAN))
    assert result.exit_code == 1
    assert result.findings[0].owner == "frontend"


def test_exemption_passes_and_is_reported(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    root = _repo(tmp_path)
    logger = root / "tests" / "Api.Tests" / "ListLogger.cs"
    logger.parent.mkdir(parents=True)
    logger.write_text((SAMPLES / "exempt_logger.cs").read_text(encoding="utf-8"), "utf-8")
    report = tmp_path / "r.md"
    run_all.main(["--root", str(root), "--report", str(report)], _dup(CLEAN))
    out = capsys.readouterr().out
    assert "exemption: tests/Api.Tests/ListLogger.cs:7 ListLogger::Log<TState>" in out
    assert "2 exemptions" in out
    assert "## Exemptions (2)" in report.read_text(encoding="utf-8")


def test_raw_colour_exits_one_and_is_reported(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    root = _repo(tmp_path)
    css = root / "src" / "StemStudio.Web" / "wwwroot" / "css" / "base.css"
    css.parent.mkdir(parents=True)
    css.write_text("body { color: #fff; }\n", encoding="utf-8")
    report = tmp_path / "r.md"
    assert run_all.main(["--root", str(root), "--report", str(report)], _dup(CLEAN)) == 1
    assert "[raw-colour]" in capsys.readouterr().out
    text = report.read_text(encoding="utf-8")
    assert "## Design tokens\n\n**FAIL** - 1 errors" in text
    row = "| error | raw-colour | `src/StemStudio.Web/wwwroot/css/base.css:1` | frontend |"
    assert row in text


def test_claude_key_rule_breach_exits_one_and_is_reported(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    root = _repo(tmp_path)
    store = root / "src" / "StemStudio.Api" / "Agent" / "Secrets" / "Store.cs"
    store.parent.mkdir(parents=True)
    store.write_text('class S { void M() => logger.LogInformation("{Key}", key); }\n', "utf-8")
    report = tmp_path / "r.md"
    assert run_all.main(["--root", str(root), "--report", str(report)], _dup(CLEAN)) == 1
    assert "[secret]" in capsys.readouterr().out
    text = report.read_text(encoding="utf-8")
    assert "## Claude key handling\n\n**FAIL** - 1 errors" in text
    assert "| error | secret | `src/StemStudio.Api/Agent/Secrets/Store.cs:1` | backend |" in text


def test_clean_report_has_a_passing_claude_key_section(tmp_path: Path) -> None:
    report = tmp_path / "r.md"
    run_all.main(["--root", str(_repo(tmp_path)), "--report", str(report)], _dup(CLEAN))
    text = report.read_text(encoding="utf-8")
    assert "## Claude key handling\n\n**PASS** - Data Protection only in Api/Agent/Secrets" in text


def test_library_care_breach_exits_one_and_is_reported(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    root = _repo(tmp_path)
    endpoints = root / "src" / "StemStudio.Api" / "Tracks" / "TrackEndpoints.cs"
    endpoints.parent.mkdir(parents=True)
    endpoints.write_text("using StemStudio.Api.LibraryCare.Backup;\n", "utf-8")
    report = tmp_path / "r.md"
    assert run_all.main(["--root", str(root), "--report", str(report)], _dup(CLEAN)) == 1
    assert "[library-care]" in capsys.readouterr().out
    text = report.read_text(encoding="utf-8")
    assert "## Library care\n\n**FAIL** - 1 errors" in text
    row = "| error | library-care | `src/StemStudio.Api/Tracks/TrackEndpoints.cs:1` | backend |"
    assert row in text


def test_clean_report_has_a_passing_library_care_section(tmp_path: Path) -> None:
    report = tmp_path / "r.md"
    run_all.main(["--root", str(_repo(tmp_path)), "--report", str(report)], _dup(CLEAN))
    text = report.read_text(encoding="utf-8")
    assert "## Library care\n\n**PASS** - only Api.LibraryCare.Gate outside Api/LibraryCare" in text
