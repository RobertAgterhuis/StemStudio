"""Running external programs (ffmpeg) from argument lists, never through a shell."""

import subprocess
from collections.abc import Sequence
from dataclasses import dataclass
from typing import Protocol


@dataclass(frozen=True, slots=True)
class CommandResult:
    """Outcome of an external command.

    Attributes:
        returncode: Exit code; non-zero (``-1`` for launch failures and timeouts) is a failure.
        stdout: Captured standard output (text).
        stderr: Captured standard error (text); for logs only, never shown to users.
    """

    returncode: int
    stdout: str
    stderr: str


class CommandRunner(Protocol):
    """Runs an external program from an argument list."""

    def run(self, args: Sequence[str], timeout_seconds: float) -> CommandResult:
        """Run ``args`` (program first) without a shell and capture its output."""
        ...


class SubprocessRunner:
    """``CommandRunner`` backed by ``subprocess.run`` (argument list, no shell, no stdin)."""

    def run(self, args: Sequence[str], timeout_seconds: float) -> CommandResult:
        """Run ``args``; a timeout or a missing program becomes a failed ``CommandResult``."""
        try:
            completed = subprocess.run(  # noqa: S603 - argument list, never a shell
                list(args),
                capture_output=True,
                text=True,
                encoding="utf-8",
                errors="replace",
                timeout=timeout_seconds,
                check=False,
                stdin=subprocess.DEVNULL,
            )
        except (subprocess.TimeoutExpired, OSError) as exc:
            return CommandResult(-1, "", f"{type(exc).__name__}: {exc}")
        return CommandResult(completed.returncode, completed.stdout, completed.stderr)
