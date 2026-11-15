"""Reconstruction check of stored detail stems (quickstart D3, SC-002).

Works on a backend track directory (``<DataDir>/tracks/<id>``) and on a worker cache entry
(``cache/by-audio/<sha>``): both hold ``stems/<parent>.flac`` and ``detail/<parent>/<name>.flac``.
A split parent passes when its detail stems sum back to the parent within -30 dB, it has at
least two sub-stems besides ``rest``, and every sub-stem passes the audibility gate.
"""

import sys
from collections.abc import Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Final

import numpy as np

from stemstudio_worker.detail.audio_io import read_stem
from stemstudio_worker.detail.checks import MAX_RECONSTRUCTION_DB, MIN_KEPT
from stemstudio_worker.detail.gating import read_parent, relative_db, sub_stem_audible
from stemstudio_worker.detail.names import REST, STANDARD_ORDER

USAGE: Final = "Usage: check_reconstruction.py <trackDir>  (holds stems/ and detail/)"


@dataclass(frozen=True, slots=True)
class ParentCheck:
    """Result for one split parent.

    Attributes:
        parent: The parent stem.
        stems: Detail stems found (display order as stored, ``rest`` last).
        reconstruction_db: Parent minus the sum of the stems, dB relative to the parent.
        inaudible: Sub-stems (not ``rest``) that fail the audibility gate.
    """

    parent: str
    stems: tuple[str, ...]
    reconstruction_db: float
    inaudible: tuple[str, ...]

    @property
    def ok(self) -> bool:
        """Whether the split satisfies FR-004..FR-006."""
        audible = not self.inaudible and self.reconstruction_db <= MAX_RECONSTRUCTION_DB
        return self.complete and audible

    @property
    def complete(self) -> bool:
        """Whether ``rest`` and at least two other sub-stems exist."""
        return REST in self.stems and len(self.stems) - 1 >= MIN_KEPT


def _stem_names(folder: Path) -> tuple[str, ...]:
    names = sorted(path.stem for path in folder.glob("*.flac"))
    return tuple([n for n in names if n != REST] + ([REST] if REST in names else []))


def check_parent(track_dir: Path, parent: str) -> ParentCheck:
    """Check the detail stems of ``parent`` below ``track_dir``."""
    audio = read_stem(track_dir / "stems" / f"{parent}.flac")
    folder = track_dir / "detail" / parent
    names = _stem_names(folder)
    stems = {name: read_stem(folder / f"{name}.flac") for name in names}
    error = relative_db(audio - np.sum(list(stems.values()), axis=0, dtype=np.float32), audio)
    reading = read_parent(audio)
    inaudible = tuple(
        n for n, s in stems.items() if n != REST and not sub_stem_audible(s, audio, reading)
    )
    return ParentCheck(parent, names, error, inaudible)


def check_track(track_dir: Path) -> list[ParentCheck]:
    """Check every parent with a ``detail/<parent>/`` folder, in standard display order."""
    present = {path.name for path in (track_dir / "detail").iterdir() if path.is_dir()}
    return [check_parent(track_dir, parent) for parent in STANDARD_ORDER if parent in present]


def format_report(checks: Sequence[ParentCheck]) -> str:
    """One line per parent: verdict, reconstruction error, stems and inaudible stems."""
    lines = [
        f"{'OK  ' if c.ok else 'FAIL'} {c.parent:<7} {c.reconstruction_db:8.1f} dB  "
        f"{', '.join(c.stems)}" + (f"  inaudible: {', '.join(c.inaudible)}" if c.inaudible else "")
        for c in checks
    ]
    return "\n".join(lines)


def main(argv: Sequence[str]) -> int:
    """CLI entry point: 0 = every split parent passes, 1 = a parent fails, 2 = usage error."""
    checks = _checks(argv)
    if not checks:
        print(USAGE, file=sys.stderr)  # noqa: T201 - command-line tool
        return 2
    print(format_report(checks))  # noqa: T201 - command-line tool
    return 0 if all(c.ok for c in checks) else 1


def _checks(argv: Sequence[str]) -> list[ParentCheck]:
    """Checks of the single track directory argument; empty for a usage error."""
    if len(argv) != 1 or not (Path(argv[0]) / "detail").is_dir():
        return []
    return check_track(Path(argv[0]))
