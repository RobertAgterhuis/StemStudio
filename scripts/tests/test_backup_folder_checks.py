"""Tests for scripts/backup_folder_checks.py (013 T011; FR-005)."""

import stat
from pathlib import Path

import pytest

import backup_folder_checks as bfc

_OK = bfc.DiskUsage(total=10 * 1024**3, used=0, free=2 * 1024**3)


def _host(*, posix: bool = False, writable: bool = True, free: int = _OK.free) -> bfc.Host:
    usage = bfc.DiskUsage(total=_OK.total, used=0, free=free)
    return bfc.Host(posix=posix, can_write=lambda _p: writable, usage=lambda _p: usage)


def test_default_folder_is_backups_in_the_repository(tmp_path: Path) -> None:
    assert bfc.resolve_folder(None, tmp_path) == tmp_path / "backups"
    assert bfc.resolve_folder("", tmp_path) == tmp_path / "backups"


def test_relative_and_absolute_folders(tmp_path: Path) -> None:
    assert bfc.resolve_folder("store/b", tmp_path) == tmp_path / "store" / "b"
    assert bfc.resolve_folder(str(tmp_path / "x"), Path("elsewhere")) == tmp_path / "x"


def test_a_missing_folder_fails_alone(tmp_path: Path) -> None:
    checks = bfc.run_checks(tmp_path / "missing", 0, _host())

    assert [c.ok for c in checks] == [False]
    assert "mkdir -p" in checks[0].message


def test_an_existing_writable_folder_with_space_passes(tmp_path: Path) -> None:
    checks = bfc.run_checks(tmp_path, bfc.DEFAULT_MIN_FREE, _host())

    assert [c.ok for c in checks] == [True, True, True]
    assert checks[2].message == "2.0 GiB free"
    assert checks[0].line().startswith("ok   ")


def test_not_writable_for_the_current_user(tmp_path: Path) -> None:
    checks = bfc.run_checks(tmp_path, 0, _host(writable=False))

    assert checks[1] == bfc.Check(ok=False, message="not writable for the current user")
    assert checks[1].line().startswith("FAIL ")


def test_too_little_free_space(tmp_path: Path) -> None:
    checks = bfc.run_checks(tmp_path, bfc.DEFAULT_MIN_FREE, _host(free=1024))

    assert not checks[2].ok
    assert "below the 0.25 GiB minimum" in checks[2].message


@pytest.mark.parametrize(
    ("mode", "uid", "gid", "expected"),
    [
        (0o700, 1654, 0, True),
        (0o500, 1654, 0, False),
        (0o770, 0, 1654, True),
        (0o750, 0, 1654, False),
        (0o777, 0, 0, True),
        (0o775, 0, 0, False),
        (0o707, 1000, 1000, True),
    ],
)
def test_posix_write_bits_for_uid_1654(mode: int, uid: int, gid: int, expected: bool) -> None:
    assert bfc.posix_writable(stat.S_IFDIR | mode, uid, gid) is expected


def test_posix_host_reports_the_chown_fix(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(bfc, "posix_writable", lambda *_args: False)

    checks = bfc.run_checks(tmp_path, 0, _host(posix=True))

    assert not checks[1].ok
    assert f"sudo chown 1654:1654 {tmp_path}" in checks[1].message


def test_posix_host_passes_a_prepared_folder(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(bfc, "posix_writable", lambda *_args: True)

    checks = bfc.run_checks(tmp_path, 0, _host(posix=True))

    assert checks[1] == bfc.Check(ok=True, message="writable by uid 1654")


def test_main_prints_one_line_per_check(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    (tmp_path / "backups").mkdir()

    code = bfc.main(["--repo-root", str(tmp_path)], _host())

    assert code == 0
    assert len(capsys.readouterr().out.splitlines()) == 3


def test_main_fails_and_reads_the_environment(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    monkeypatch.setenv(bfc.ENV_VAR, str(tmp_path / "usb"))

    code = bfc.main(["--repo-root", str(tmp_path)], _host())

    assert code == 1
    assert str(tmp_path / "usb") in capsys.readouterr().out


def test_default_host_measures_the_real_folder(tmp_path: Path) -> None:
    host = bfc.default_host()

    assert host.can_write(tmp_path)
    assert host.usage(tmp_path).free > 0
