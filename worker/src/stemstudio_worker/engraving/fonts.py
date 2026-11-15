"""Fontconfig check for the Leipzig music font (feature 008, SC-004).

Verovio writes SMuFL text glyphs (the tempo mark's note) with ``font-family="Leipzig"``; Cairo
draws them as boxes unless fontconfig knows that family. The worker image installs it at build
time (``worker/scripts/install_music_font.py``); this module only asks ``fc-list`` whether it is
there. ``fc-list`` runs with an argument list, no shell and no stdin; a missing ``fc-list`` (as on
Windows) or a failing run means "not installed".
"""

import shutil
import subprocess
from collections.abc import Callable
from typing import Final

FONT_FAMILY: Final = "Leipzig"
"""Family name that Verovio's SVG uses and fontconfig must list."""

FC_LIST_TIMEOUT_SECONDS: Final = 10.0
"""Upper bound for one ``fc-list`` run."""

FamilyLister = Callable[[], frozenset[str]]
"""Returns the font family names fontconfig lists for the Leipzig pattern."""


def fontconfig_families() -> frozenset[str]:
    """Return the family names ``fc-list :family=Leipzig family`` prints.

    Returns:
        The names (one line may hold several, comma-separated); empty when ``fc-list`` is not
        installed, cannot run, times out or exits non-zero.
    """
    executable = shutil.which("fc-list")
    if executable is None:
        return frozenset()
    output = _run_fc_list(executable)
    return frozenset() if output is None else _parse_families(output)


def leipzig_available(list_families: FamilyLister = fontconfig_families) -> bool:
    """Return whether fontconfig lists exactly the ``Leipzig`` family.

    Args:
        list_families: Source of the family names; defaults to ``fc-list``.
    """
    return FONT_FAMILY in list_families()


def _run_fc_list(executable: str) -> str | None:
    try:
        completed = subprocess.run(  # noqa: S603 - argument list, never a shell
            [executable, f":family={FONT_FAMILY}", "family"],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=FC_LIST_TIMEOUT_SECONDS,
            check=False,
            stdin=subprocess.DEVNULL,
        )
    except (subprocess.TimeoutExpired, OSError):
        return None
    return completed.stdout if completed.returncode == 0 else None


def _parse_families(output: str) -> frozenset[str]:
    names = (name.strip() for line in output.splitlines() for name in line.split(","))
    return frozenset(name for name in names if name)
