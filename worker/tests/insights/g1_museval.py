"""BSS Eval v4 scoring for gate G1; runs ONLY in a throw-away env with ``museval==0.4.1`` (MIT).

Usage: ``python -I g1_museval.py <reference folder> <estimate folder>``. Both folders hold
``vocals/drums/bass/other.flac``. Prints JSON ``{source: [median SIR, median SAR]}`` (dB, over
the 1-second BSS Eval v4 frames). Not imported by the worker or the default test suite.
"""

import json
import sys
from pathlib import Path

import museval  # type: ignore[import-not-found]
import numpy as np
import soundfile as sf

SOURCES = ("vocals", "drums", "bass", "other")


def main(reference: Path, estimate: Path) -> None:
    """Score the four sources and print the medians."""
    refs = np.stack([sf.read(reference / f"{name}.flac", always_2d=True)[0] for name in SOURCES])
    ests = np.stack([sf.read(estimate / f"{name}.flac", always_2d=True)[0] for name in SOURCES])
    _, _, sir, sar, _ = museval.metrics.bss_eval(refs, ests, window=44_100, hop=44_100)
    result = {
        name: [float(np.nanmedian(sir[i])), float(np.nanmedian(sar[i]))]
        for i, name in enumerate(SOURCES)
    }
    print(json.dumps(result))  # noqa: T201


if __name__ == "__main__":
    main(Path(sys.argv[1]), Path(sys.argv[2]))
