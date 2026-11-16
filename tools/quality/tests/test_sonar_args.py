import fnmatch

import pytest

import sonar_args
from common import REPO_ROOT


def test_parse_properties_ignores_comments_and_blank_lines() -> None:
    text = "# c\n! c\n\nsonar.a = 1\nsonar.b=x=y\nnovalue\n"
    assert sonar_args.parse_properties(text) == {"sonar.a": "1", "sonar.b": "x=y"}


def test_begin_args_skip_unsupported_keys() -> None:
    props = {"sonar.sources": "src", "sonar.projectKey": "k", "sonar.x": "1"}
    assert sonar_args.to_begin_args(props) == ["/d:sonar.x=1"]


def test_repo_properties_cover_all_report_paths(capsys: pytest.CaptureFixture[str]) -> None:
    assert sonar_args.main(["--root", str(REPO_ROOT)]) == 0
    out = capsys.readouterr().out.splitlines()
    keys = {line.split("=", 1)[0] for line in out}
    assert {
        "/d:sonar.cs.opencover.reportsPaths",
        "/d:sonar.python.coverage.reportPaths",
        "/d:sonar.javascript.lcov.reportPaths",
        "/d:sonar.exclusions",
    } <= keys
    assert not any("token" in line.lower() for line in out)
    assert "/d:sonar.sources" not in keys


def test_agent_eval_is_a_source_and_its_coverage_report_is_imported() -> None:
    """Feature 011 (T080): the eval harness is analysed and its xUnit coverage reaches Sonar."""
    text = (REPO_ROOT / "sonar-project.properties").read_text(encoding="utf-8")
    props = sonar_args.parse_properties(text)
    assert "tools/agent-eval" in props["sonar.sources"].split(",")
    report = "tests/StemStudio.AgentEval.Tests/TestResults/coverage/coverage.opencover.xml"
    patterns = props["sonar.cs.opencover.reportsPaths"].split(",")
    assert any(fnmatch.fnmatch(report, pattern) for pattern in patterns)
