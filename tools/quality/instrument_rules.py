"""Instrument view rules (feature 016, tasks.md T009; ADR-031, ADR-032).

(a) No ``weights_only=False`` anywhere in ``worker/src`` (vendored code included): model
    checkpoints are loaded with ``torch.load(weights_only=True)`` and an explicit allow-list of
    globals only (``ports.BTC_SAFE_GLOBALS``).
(b) The engine's pitch code (``src/audio-engine/src/pitch/**``) uses no DOM or Web Audio globals,
    so the YIN core, the scorer and the worklet run in the AudioWorklet scope and in unit tests
    alike. Comments, string literals and member names (``options.window``) do not count.

The worker layering of T009 (harmony, notation, views_fake, the BTC vendor) is part of
``dependency_check`` (``PYTHON_ALLOWED``, ``PYTHON_SCOPES``), as is "nothing but ``index.ts``
imports ``engine.ts``" (``TS_MODULE_IMPORTERS``).

Usage::

    python tools/quality/instrument_rules.py [--root DIR]

Exit code 0 = clean, 1 = a rule is broken.
"""

import re
import sys
from collections.abc import Iterator, Sequence
from pathlib import Path
from typing import Final

from common import EXCLUDED_DIRS, Finding, Severity, build_parser, relative, report_findings

CHECK: Final = "instrument"
"""Check name of every finding of this module."""

WORKER_SRC: Final = "worker/src"
PITCH_DIR: Final = "src/audio-engine/src/pitch"

DOM_GLOBALS: Final = (
    "window", "document", "navigator", "localStorage", "sessionStorage", "requestAnimationFrame",
    "cancelAnimationFrame", "HTMLElement", "HTMLCanvasElement", "CanvasRenderingContext2D",
    "AudioContext", "OfflineAudioContext", "BaseAudioContext", "AudioNode", "AudioWorkletNode",
    "AudioBuffer", "MediaStream", "MediaStreamAudioSourceNode", "AnalyserNode", "GainNode",
)  # fmt: skip
"""Browser main-thread globals the pitch code must not use."""

_WEIGHTS_ONLY_FALSE = re.compile(r"\bweights_only\s*=\s*False\b")
_TS_NOISE = re.compile(
    r"//[^\n]*|/\*[\s\S]*?\*/|'(?:\\.|[^'\\\n])*'|\"(?:\\.|[^\"\\\n])*\"|`(?:\\.|[^`\\])*`"
)
_DOM = re.compile(rf"(?<![\w.$])(?P<name>{'|'.join(DOM_GLOBALS)})\b")


def run(root: Path) -> list[Finding]:
    """Check the worker sources and the engine pitch folder below ``root``."""
    return [*check_weights_only(root), *check_pitch_globals(root)]


def _files(folder: Path, suffix: str) -> Iterator[Path]:
    if folder.is_dir():
        for path in sorted(folder.rglob(f"*{suffix}")):
            if not EXCLUDED_DIRS & set(path.relative_to(folder).parts):
                yield path


def _line(text: str, offset: int) -> int:
    return text.count("\n", 0, offset) + 1


def check_weights_only(root: Path) -> list[Finding]:
    """(a) Every ``weights_only=False`` in a worker source file."""
    findings = []
    for path in _files(root / WORKER_SRC, ".py"):
        text = path.read_text(encoding="utf-8")
        for match in _WEIGHTS_ONLY_FALSE.finditer(text):
            message = "weights_only=False: load checkpoints with weights_only=True and safe globals"
            findings.append(_finding(relative(path, root), _line(text, match.start()), message))
    return findings


def check_pitch_globals(root: Path) -> list[Finding]:
    """(b) DOM and Web Audio globals in the engine's pitch code."""
    findings = []
    for path in _files(root / PITCH_DIR, ".ts"):
        code = _TS_NOISE.sub(lambda m: re.sub(r"[^\n]", " ", m.group()), path.read_text("utf-8"))
        for match in _DOM.finditer(code):
            message = f"pitch code uses the main-thread global {match.group('name')}"
            findings.append(_finding(relative(path, root), _line(code, match.start()), message))
    return findings


def _finding(path: str, line: int, message: str) -> Finding:
    return Finding(CHECK, Severity.ERROR, path, line, message)


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the exit code."""
    root: Path = build_parser(__doc__, with_paths=False).parse_args(argv).root
    return report_findings("instrument_rules", run(root))


if __name__ == "__main__":
    sys.exit(main())
