"""Backups folder check (feature 013, T011; FR-005, research R-05).

The API writes backup archives into the host folder bind-mounted at ``/backups``
(``STEMSTUDIO_BACKUP_HOST_DIR``, default ``./backups`` next to ``docker-compose.yml``). The API
never changes host permissions, so the folder must be prepared by the user. Host-side checks,
one line each, exit 1 when any fails:

1. the folder exists and is a directory (Docker would otherwise create it owned by root);
2. it is writable for the API user: uid/gid 1654 of the .NET ``aspnet`` image on Linux hosts
   (owner, group or other write + search bits); on Windows and macOS (Docker Desktop) bind mounts
   are writable for every container user, so the current user must be able to write it;
3. it has at least ``--min-free-bytes`` free (default 256 MiB, the API's stop threshold
   ``LibraryCare:StopBelowFreeBytes``); the free space is always printed.

Used by ``scripts/check-backup-folder.{sh,ps1}``. Standard library only; reads no file contents
and starts no container.
"""

import argparse
import os
import shutil
import stat
import sys
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import NamedTuple

API_UID = 1654
"""uid and gid of the ``app`` user in the .NET ``aspnet`` image the API runs as."""

DEFAULT_MIN_FREE = 256 * 1024 * 1024
"""Bytes below which a running backup stops (data-model 1, ``StopBelowFreeBytes``)."""

ENV_VAR = "STEMSTUDIO_BACKUP_HOST_DIR"
_GIB = 1024**3


class DiskUsage(NamedTuple):
    """The members of ``shutil.disk_usage`` the check reads."""

    total: int
    used: int
    free: int


@dataclass(frozen=True, slots=True)
class Check:
    """The result of one check: a readable line and whether it passed."""

    ok: bool
    message: str

    def line(self) -> str:
        """``ok   <message>`` or ``FAIL <message>``."""
        return f"{'ok  ' if self.ok else 'FAIL'} {self.message}"


def resolve_folder(value: str | None, repo_root: Path) -> Path:
    """The host folder: ``value`` (relative to the repository root) or ``<repo>/backups``."""
    folder = Path(value) if value else Path("backups")
    return folder if folder.is_absolute() else repo_root / folder


def check_exists(folder: Path) -> Check:
    """The folder exists and is a directory."""
    if folder.is_dir():
        return Check(ok=True, message=f"{folder} exists")
    hint = f"create it first (mkdir -p {folder}); Docker would create it owned by root"
    return Check(ok=False, message=f"{folder} is not a folder: {hint}")


def posix_writable(mode: int, uid: int, gid: int, user: int = API_UID) -> bool:
    """Whether ``user`` (gid = uid) may create files in a folder of ``mode``, ``uid``, ``gid``."""
    if uid == user:
        return _bits(mode, stat.S_IWUSR | stat.S_IXUSR)
    if gid == user:
        return _bits(mode, stat.S_IWGRP | stat.S_IXGRP)
    return _bits(mode, stat.S_IWOTH | stat.S_IXOTH)


def _bits(mode: int, wanted: int) -> bool:
    return mode & wanted == wanted


def check_writable(folder: Path, posix: bool, can_write: Callable[[Path], bool]) -> Check:
    """Writable for the API user (Linux hosts) or for the current user (Docker Desktop)."""
    if posix:
        info = folder.stat()
        ok = posix_writable(info.st_mode, info.st_uid, info.st_gid)
        fix = f"run: sudo chown {API_UID}:{API_UID} {folder}"
        return Check(
            ok, f"writable by uid {API_UID}" if ok else f"not writable by uid {API_UID}; {fix}"
        )
    ok = can_write(folder)
    return Check(ok, "writable" if ok else "not writable for the current user")


def check_free(folder: Path, min_free: int, usage: Callable[[Path], DiskUsage]) -> Check:
    """At least ``min_free`` bytes free on the folder's drive."""
    free = usage(folder).free
    text = f"{free / _GIB:.1f} GiB free"
    if free >= min_free:
        return Check(ok=True, message=text)
    return Check(ok=False, message=f"{text}, below the {min_free / _GIB:.2f} GiB minimum")


@dataclass(frozen=True, slots=True)
class Host:
    """What the checks read from the host (injectable for tests)."""

    posix: bool
    can_write: Callable[[Path], bool]
    usage: Callable[[Path], DiskUsage]


def default_host() -> Host:
    """Linux hosts check uid 1654; Windows and macOS (Docker Desktop) the current user."""
    linux = sys.platform.startswith("linux")
    return Host(linux, lambda p: os.access(p, os.W_OK | os.X_OK), _disk_usage)


def _disk_usage(path: Path) -> DiskUsage:
    total, used, free = shutil.disk_usage(path)
    return DiskUsage(total, used, free)


def run_checks(folder: Path, min_free: int, host: Host) -> list[Check]:
    """Every check of the module docstring (the later ones only for an existing folder)."""
    exists = check_exists(folder)
    if not exists.ok:
        return [exists]
    writable = check_writable(folder, host.posix, host.can_write)
    return [exists, writable, check_free(folder, min_free, host.usage)]


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    """Command line: ``--folder``, ``--repo-root``, ``--min-free-bytes``."""
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--folder", default=os.environ.get(ENV_VAR), help=f"default: ${ENV_VAR}")
    parser.add_argument("--repo-root", type=Path, default=Path.cwd())
    parser.add_argument("--min-free-bytes", type=int, default=DEFAULT_MIN_FREE)
    return parser.parse_args(argv)


def main(argv: Sequence[str], host: Host | None = None) -> int:
    """Run the checks, print one line each; 0 when all pass, else 1."""
    args = parse_args(argv)
    folder = resolve_folder(args.folder, args.repo_root.resolve())
    checks = run_checks(folder, args.min_free_bytes, host or default_host())
    for check in checks:
        print(check.line())  # noqa: T201 - command line output
    return 0 if all(c.ok for c in checks) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
