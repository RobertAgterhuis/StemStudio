"""Vendored third-party packages keep their licence records (features 002 and 008).

Every package directory directly below ``detail/vendor/`` or ``transcription/vendor/`` must hold
a ``LICENSE`` (or ``LICENCE``/``COPYING``) file and a ``NOTICE`` file (origin, version and
modifications), because those folders are excluded from every other check.
"""

from collections.abc import Sequence
from pathlib import Path

import pytest
from repo_tree import WORKER_PACKAGE as PACKAGE
from repo_tree import write_file, write_module

import run_all
import vendor_check
from common import REPO_ROOT, Severity
from duplication import DuplicationResult

TRANSCRIPTION = "transcription/vendor"


def _package(root: Path, folder: str, *names: str) -> None:
    write_module(root, f"{folder}/__init__.py", "")
    for name in names:
        write_module(root, f"{folder}/{name}", "text\n")


def test_vendor_roots_cover_detail_and_transcription() -> None:
    assert (
        f"{PACKAGE}/detail/vendor",
        f"{PACKAGE}/transcription/vendor",
    ) == vendor_check.VENDOR_ROOTS


def test_no_vendor_folders_is_clean(tmp_path: Path) -> None:
    assert vendor_check.run(tmp_path) == []


def test_packages_with_licence_and_notice_are_clean(tmp_path: Path) -> None:
    _package(tmp_path, f"{TRANSCRIPTION}/basic_pitch", "LICENSE", "NOTICE")
    _package(tmp_path, f"{TRANSCRIPTION}/piano_transcription", "LICENSE.txt", "NOTICE.md")
    _package(tmp_path, "detail/vendor/msst", "licence", "notice")
    _package(tmp_path, "detail/vendor/other", "COPYING", "NOTICE")
    # The vendor root's own __init__.py and caches are not packages.
    write_module(tmp_path, f"{TRANSCRIPTION}/__init__.py", "")
    write_module(tmp_path, f"{TRANSCRIPTION}/__pycache__/x.pyc", "")
    assert vendor_check.run(tmp_path) == []


@pytest.mark.parametrize(
    ("names", "missing"),
    [
        (("NOTICE",), ["LICENSE"]),
        (("LICENSE",), ["NOTICE"]),
        (("README.md", "LICENSE_NOTES.py"), ["LICENSE", "NOTICE"]),
        ((), ["LICENSE", "NOTICE"]),
    ],
)
def test_missing_licence_records_are_errors(
    tmp_path: Path, names: Sequence[str], missing: list[str]
) -> None:
    _package(tmp_path, f"{TRANSCRIPTION}/basic_pitch", *names)
    findings = vendor_check.run(tmp_path)
    folder = f"{PACKAGE}/{TRANSCRIPTION}/basic_pitch"
    assert [f.message for f in findings] == [
        f"vendored package {TRANSCRIPTION}/basic_pitch has no {name} file" for name in missing
    ]
    for finding in findings:
        assert (finding.check, finding.severity, finding.path) == ("vendor", Severity.ERROR, folder)
        assert finding.owner == "stem-separation"


def test_licence_file_must_be_a_file(tmp_path: Path) -> None:
    _package(tmp_path, "detail/vendor/msst", "NOTICE")
    (tmp_path / PACKAGE / "detail/vendor/msst/LICENSE").mkdir()
    (finding,) = vendor_check.run(tmp_path)
    assert finding.message == "vendored package detail/vendor/msst has no LICENSE file"


def test_repository_vendor_packages_are_clean() -> None:
    assert vendor_check.run(REPO_ROOT) == []


def test_main_prints_summary_and_exit_code(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    assert vendor_check.main(["--root", str(tmp_path)]) == 0
    assert "vendor_check: 0 errors, 0 warnings" in capsys.readouterr().out
    _package(tmp_path, f"{TRANSCRIPTION}/basic_pitch", "LICENSE")
    assert vendor_check.main(["--root", str(tmp_path)]) == 1
    assert "[vendor]" in capsys.readouterr().out


def test_run_all_fails_on_a_missing_notice(tmp_path: Path) -> None:
    write_file(tmp_path, "src/App/code.py", "def f() -> int:\n    return 1\n")
    _package(tmp_path, f"{TRANSCRIPTION}/piano_transcription", "LICENSE")

    def no_clones(root: Path, paths: Sequence[Path]) -> DuplicationResult:
        return DuplicationResult(duplicated_percentage=0.0)

    result = run_all.run(tmp_path, [], no_clones)
    assert result.exit_code == 1
    assert [f.check for f in result.findings] == ["vendor"]
