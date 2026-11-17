import json
import shutil
import sys
from pathlib import Path

import pytest

import duplication
from common import Severity
from duplication import Clone, ToolRun, build_command, evaluate, failure_reason, parse_report

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
    assert command[-1] == str(Path("src").resolve())


def test_build_command_resolves_relative_paths_against_the_current_directory(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    # jscpd runs with cwd = repository root; a path given relative to another directory
    # (e.g. from tools/quality) must not turn into "path does not exist".
    monkeypatch.chdir(tmp_path)
    command = build_command("npx", tmp_path, [Path("../elsewhere/src")])
    assert command[-1] == str((tmp_path / "../elsewhere/src").resolve())


def test_config_requires_six_lines() -> None:
    config = json.loads(duplication.CONFIG_PATH.read_text(encoding="utf-8"))
    assert config["minLines"] == 6
    assert {"csharp", "python", "typescript", "razor"} <= set(config["format"])
    assert "**/tools/quality/tests/samples/**" in config["ignore"]


class FakeJscpd:
    """Writes a canned report where jscpd would, instead of running Node."""

    def __init__(
        self, report: dict[str, object] | str | None, exit_code: int = 0, output: str = ""
    ) -> None:
        self.report = report
        self.exit_code = exit_code
        self.output = output
        self.commands: list[list[str]] = []

    def __call__(self, command: list[str], cwd: Path) -> ToolRun:
        self.commands.append(command)
        if self.report is not None:
            out = Path(command[command.index("--output") + 1])
            text = self.report if isinstance(self.report, str) else json.dumps(self.report)
            (out / "jscpd-report.json").write_text(text, encoding="utf-8")
        return ToolRun(self.exit_code, self.output)


@pytest.fixture
def with_npx(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(shutil, "which", lambda name: "npx")


def _report(root: Path, first: str, second: str) -> dict[str, object]:
    return {
        "duplicates": [_entry(str(root / first), str(root / second))],
        "statistics": {"total": {"percentage": 4.0, "sources": 2}},
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


@pytest.mark.usefixtures("with_npx")
def test_run_names_the_reason_jscpd_printed(tmp_path: Path) -> None:
    output = "npm notice run jscpd\nUsing config\nError: path does not exist: src/Nope\n"
    result = duplication.run(tmp_path, [tmp_path], FakeJscpd(None, 1, output))
    assert result.tool_error == "jscpd failed (exit code 1): Error: path does not exist: src/Nope"


@pytest.mark.usefixtures("with_npx")
@pytest.mark.parametrize(
    "report",
    [
        {"duplicates": [], "statistics": {"total": {"percentage": 0.0, "sources": 0}}},
        {"duplicates": []},
    ],
)
def test_run_fails_when_jscpd_scanned_nothing(tmp_path: Path, report: dict[str, object]) -> None:
    # Exit code 0 with an empty report is not a clean tree: nothing was measured.
    result = duplication.run(tmp_path, [tmp_path], FakeJscpd(report))
    assert result.tool_error is not None
    assert "scanned no source file" in result.tool_error


@pytest.mark.usefixtures("with_npx")
def test_run_fails_on_an_unreadable_report(tmp_path: Path) -> None:
    result = duplication.run(tmp_path, [tmp_path], FakeJscpd("{not json"))
    assert result.tool_error is not None
    assert result.tool_error.startswith("jscpd report unreadable")


@pytest.mark.parametrize(
    ("output", "reason"),
    [
        ("", ""),
        ("npm notice run npx\n\n", ""),
        ("first\nnpm error code ENOTCACHED\nnpm notice done\n", "npm error code ENOTCACHED"),
    ],
)
def test_failure_reason_skips_npm_notices(output: str, reason: str) -> None:
    assert failure_reason(output) == reason


def test_run_without_npx(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(shutil, "which", lambda name: None)
    result = duplication.run(tmp_path, [tmp_path], FakeJscpd({}))
    assert result.tool_error is not None
    assert "npx not found" in result.tool_error


def test_subprocess_runner_returns_exit_code_and_output(tmp_path: Path) -> None:
    script = "import sys; sys.stderr.write('boom'); raise SystemExit(3)"
    outcome = duplication.subprocess_runner([sys.executable, "-c", script], tmp_path)
    assert outcome.exit_code == 3
    assert "boom" in outcome.output


def test_subprocess_runner_reports_a_command_that_cannot_start(tmp_path: Path) -> None:
    outcome = duplication.subprocess_runner([str(tmp_path / "no-such-npx")], tmp_path)
    assert outcome.exit_code == 127
    assert outcome.output


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
