import json
import shutil
import sys
from pathlib import Path

import pytest

import duplication
from common import Severity
from duplication import Clone, build_command, evaluate, parse_report

BLOCK = "\n".join(f"    value_{i} = compute({i}) + offset * {i}" for i in range(7))


def _entry(first: str, second: str, lines: int = 7) -> dict[str, object]:
    return {
        "firstFile": {"name": first, "start": 3, "end": 3 + lines},
        "secondFile": {"name": second, "start": 10, "end": 10 + lines},
        "lines": lines,
        "format": "python",
    }


def test_parse_report_normalises_windows_long_paths(tmp_path: Path) -> None:
    first = "\\\\?\\" + str(tmp_path / "src" / "a.py")
    report = {
        "duplicates": [_entry(first, str(tmp_path / "src" / "b.py"))],
        "statistics": {"total": {"percentage": 12.5}},
    }
    clones, percentage = parse_report(report, tmp_path)
    assert clones == [Clone("src/a.py", 3, "src/b.py", 10, 7)]
    assert percentage == 12.5


def test_parse_empty_report(tmp_path: Path) -> None:
    assert parse_report({}, tmp_path) == ([], 0.0)


@pytest.mark.parametrize(
    ("first", "second", "severity"),
    [
        ("src/StemStudio.Api/A.cs", "src/StemStudio.Api/B.cs", Severity.ERROR),
        ("worker/src/stemstudio_worker/a.py", "worker/tests/test_a.py", Severity.ERROR),
        ("worker/tests/test_a.py", "worker/tests/test_b.py", Severity.WARNING),
        ("tests/StemStudio.Api.Tests/A.cs", "tests/StemStudio.Web.Tests/B.cs", Severity.WARNING),
    ],
)
def test_production_clones_fail_test_clones_warn(
    first: str, second: str, severity: Severity
) -> None:
    (finding,) = evaluate([Clone(first, 3, second, 10, 7)])
    assert finding.severity is severity
    assert finding.check == "duplication"
    assert f"also in {second}:10" in finding.message


def test_build_command_pins_version_and_config(tmp_path: Path) -> None:
    command = build_command("npx", tmp_path, [Path("src")])
    assert command[:3] == ["npx", "--yes", f"jscpd@{duplication.JSCPD_VERSION}"]
    assert command[command.index("--config") + 1] == str(duplication.CONFIG_PATH)
    assert command[-1] == "src"


def test_config_requires_six_lines() -> None:
    config = json.loads(duplication.CONFIG_PATH.read_text(encoding="utf-8"))
    assert config["minLines"] == 6
    assert {"csharp", "python", "typescript", "razor"} <= set(config["format"])
    assert "**/tools/quality/tests/samples/**" in config["ignore"]


class FakeJscpd:
    """Writes a canned report where jscpd would, instead of running Node."""

    def __init__(self, report: dict[str, object] | None, exit_code: int = 0) -> None:
        self.report = report
        self.exit_code = exit_code
        self.commands: list[list[str]] = []

    def __call__(self, command: list[str], cwd: Path) -> int:
        self.commands.append(command)
        if self.report is not None:
            out = Path(command[command.index("--output") + 1])
            (out / "jscpd-report.json").write_text(json.dumps(self.report), encoding="utf-8")
        return self.exit_code


@pytest.fixture
def with_npx(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(shutil, "which", lambda name: "npx")


def _report(root: Path, first: str, second: str) -> dict[str, object]:
    return {
        "duplicates": [_entry(str(root / first), str(root / second))],
        "statistics": {"total": {"percentage": 4.0}},
    }


@pytest.mark.usefixtures("with_npx")
def test_run_detects_duplicated_six_line_block(tmp_path: Path) -> None:
    for name in ("a.py", "b.py"):
        (tmp_path / "src").mkdir(exist_ok=True)
        (tmp_path / "src" / name).write_text(f"def f():\n{BLOCK}\n", encoding="utf-8")
    fake = FakeJscpd(_report(tmp_path, "src/a.py", "src/b.py"))
    result = duplication.run(tmp_path, [], fake)
    assert result.tool_error is None
    assert result.duplicated_percentage == 4.0
    assert [(f.path, f.severity) for f in result.findings] == [("src/a.py", Severity.ERROR)]
    assert fake.commands[0][-1] == str(tmp_path / "src")


@pytest.mark.usefixtures("with_npx")
@pytest.mark.parametrize(("report", "exit_code"), [(None, 0), ({"duplicates": []}, 3)])
def test_run_reports_jscpd_failure(
    tmp_path: Path, report: dict[str, object] | None, exit_code: int
) -> None:
    result = duplication.run(tmp_path, [tmp_path], FakeJscpd(report, exit_code))
    assert result.tool_error == f"jscpd failed (exit code {exit_code})"


def test_run_without_npx(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(shutil, "which", lambda name: None)
    result = duplication.run(tmp_path, [tmp_path], FakeJscpd({}))
    assert result.tool_error is not None
    assert "npx not found" in result.tool_error


def test_subprocess_runner_returns_exit_code(tmp_path: Path) -> None:
    assert (
        duplication.subprocess_runner([sys.executable, "-c", "raise SystemExit(3)"], tmp_path) == 3
    )


def _patch_run(monkeypatch: pytest.MonkeyPatch, result: duplication.DuplicationResult) -> None:
    monkeypatch.setattr(duplication, "run", lambda root, paths: result)


@pytest.mark.parametrize(
    ("clones", "code"),
    [
        ([Clone("src/a.py", 1, "src/b.py", 9, 7)], 1),
        ([Clone("tests/a.py", 1, "tests/b.py", 9, 7)], 0),
        ([], 0),
    ],
)
def test_main_exit_codes(
    monkeypatch: pytest.MonkeyPatch,
    capsys: pytest.CaptureFixture[str],
    clones: list[Clone],
    code: int,
) -> None:
    _patch_run(monkeypatch, duplication.DuplicationResult(clones, evaluate(clones), 1.5))
    assert duplication.main([]) == code
    assert "1.50% duplicated lines" in capsys.readouterr().out


def test_main_tool_error(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    _patch_run(monkeypatch, duplication.DuplicationResult(tool_error="boom"))
    assert duplication.main([]) == 2
    assert "boom" in capsys.readouterr().err
