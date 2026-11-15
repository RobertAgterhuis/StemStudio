from pathlib import Path

import pytest

import common
from common import (
    REPO_ROOT,
    Finding,
    Severity,
    build_parser,
    count_errors,
    default_paths,
    format_findings,
    is_excluded,
    is_production,
    is_test,
    iter_source_files,
    owner_of,
    relative,
)


@pytest.mark.parametrize(
    ("path", "owner"),
    [
        ("src/StemStudio.Api/Program.cs", "backend"),
        ("tests/StemStudio.Web.Tests/HomePageTests.cs", "frontend"),
        ("src/audio-engine/src/types.ts", "architect"),
        ("src/audio-engine/src/index.ts", "audio-engine"),
        ("worker/src/stemstudio_worker/ingest/youtube.py", "audio-ingest"),
        ("worker/src/stemstudio_worker/main.py", "stem-separation"),
        ("worker/src/stemstudio_worker/ports.py", "architect"),
        ("e2e/tests/main.spec.ts", "qa"),
        ("tools/quality/common.py", "quality-gate"),
        ("StemStudio.Web", "architect"),
    ],
)
def test_owner_of(path: str, owner: str) -> None:
    assert owner_of(path) == owner
    assert Finding("x", Severity.ERROR, path, 1, "m").owner == owner


@pytest.mark.parametrize(
    ("path", "expected"),
    [
        ("src/StemStudio.Api/Program.cs", False),
        ("tests/StemStudio.Api.Tests/HealthEndpointTests.cs", True),
        ("src/Foo.Tests/Bar.cs", True),
        ("src/audio-engine/src/mixer.test.ts", True),
        ("src/x/test_y.py", True),
        ("src/x/conftest.py", True),
        ("worker/src/stemstudio_worker/jobs/runner.py", False),
    ],
)
def test_is_test(path: str, expected: bool) -> None:
    assert is_test(path) is expected


def test_is_production() -> None:
    assert is_production("src/StemStudio.Api/Program.cs")
    assert is_production("worker/src/stemstudio_worker/main.py")
    assert not is_production("worker/tests/test_x.py")
    assert not is_production("tools/quality/common.py")


@pytest.mark.parametrize(
    ("path", "expected"),
    [
        ("src/StemStudio.Api/obj/Debug/X.cs", True),
        ("src/StemStudio.Persistence/Migrations/0001_Init.cs", True),
        ("src/StemStudio.Web/Foo.g.cs", True),
        ("src/StemStudio.Web/Foo.Designer.cs", True),
        ("src/StemStudio.Web/wwwroot/js/audio-engine.js", True),
        ("tools/quality/tests/samples/long_unit.py", True),
        ("src/StemStudio.Web/Program.cs", False),
    ],
)
def test_is_excluded(path: str, expected: bool) -> None:
    assert is_excluded(path) is expected


def test_relative_outside_root_is_absolute(tmp_path: Path) -> None:
    other = tmp_path / "a.py"
    assert relative(other, tmp_path / "sub") == other.resolve().as_posix()
    assert relative(other, tmp_path) == "a.py"


def test_iter_source_files_dedupes_and_accepts_files(tmp_path: Path) -> None:
    (tmp_path / "a.py").write_text("x = 1\n", encoding="utf-8")
    (tmp_path / "b.md").write_text("# no\n", encoding="utf-8")
    found = list(iter_source_files(tmp_path, [tmp_path, tmp_path / "a.py", tmp_path / "b.md"]))
    assert found == [tmp_path / "a.py"]


def test_default_paths_only_existing(tmp_path: Path) -> None:
    (tmp_path / "src").mkdir()
    assert default_paths(tmp_path) == [tmp_path / "src"]
    assert (REPO_ROOT / "tools" / "quality" / "common.py").is_file()


def test_helpers() -> None:
    findings = [
        Finding("a", Severity.ERROR, "p", 1, "m1"),
        Finding("b", Severity.WARNING, "q", 2, "m2"),
    ]
    assert count_errors(findings) == 1
    assert format_findings(findings) == "p:1: error [a] m1\nq:2: warning [b] m2"
    args = build_parser("Title line.\nMore.", with_paths=False).parse_args(["--root", "x"])
    assert args.root == Path("x")
    assert not hasattr(args, "paths")
    assert build_parser(None).parse_args([]).paths == []


def test_report_findings_prints_totals_and_returns_the_exit_code(
    capsys: pytest.CaptureFixture[str],
) -> None:
    error = common.Finding("x", common.Severity.ERROR, "a.cs", 3, "bad")
    warning = common.Finding("x", common.Severity.WARNING, "b.cs", 0, "meh")
    assert common.report_findings("demo", [error, warning]) == 1
    assert capsys.readouterr().out == (
        "a.cs:3: error [x] bad\nb.cs:0: warning [x] meh\ndemo: 1 errors, 1 warnings\n"
    )
    assert common.report_findings("demo", [warning]) == 0
    assert common.report_findings("demo", []) == 0
    assert capsys.readouterr().out.endswith("demo: 0 errors, 0 warnings\n")
