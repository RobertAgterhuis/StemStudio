"""Folding of raw model outputs into the closed ``DetailStemName`` set (research.md D-02, D-05).

Each detail stem takes a fixed tuple of model outputs. The Mega 53 outputs form an overlapping
hierarchy, so every family takes exactly one umbrella output (never children, which would
double count); outputs that are not listed are ignored and end up in ``rest``.
"""

from collections.abc import Mapping
from typing import Final

import numpy as np

from stemstudio_worker.ports import DetailAudio
from stemstudio_worker.schemas import DetailStemName

FoldTable = Mapping[DetailStemName, tuple[str, ...]]
"""Detail stem -> the model outputs summed into it (in display order)."""

DRUMSEP_TABLE: Final[FoldTable] = {
    "kick": ("kick",),
    "snare": ("snare",),
    "toms": ("toms",),
    "hihat": ("hh",),
    "cymbals": ("ride", "crash"),
}
"""MDX23C DrumSep 6-stem: ``ride + crash -> cymbals``, ``hh -> hihat``."""

VOCAL_TABLE: Final[FoldTable] = {"lead": ("lead",), "backing": ("backing",)}
"""UVR MDX-Net KARA 2 (outputs named by the runtime)."""

MEGA_TABLE: Final[FoldTable] = {
    "strings": ("bowed_strings",),
    "brass": ("brass",),
    "woodwinds": ("woodwind",),
    "keys": ("keys",),
    "synth": ("synth",),
    "acoustic-guitar": ("acoustic-guitar",),
    "electric-guitar": ("electric-guitar",),
    "percussion": ("percussion",),
}
"""MVSep Mega 53-stem: one umbrella output per family (``strings`` excludes guitars)."""

MEGA_OUTPUTS: Final[tuple[str, ...]] = tuple(out for outs in MEGA_TABLE.values() for out in outs)
"""The only Mega 53 outputs the pipeline needs (accumulated inside ``demix``)."""


def fold(outputs: Mapping[str, DetailAudio], table: FoldTable) -> dict[DetailStemName, DetailAudio]:
    """Sum the model ``outputs`` into detail stems following ``table``.

    A detail stem whose outputs are all missing is left out; extra outputs are ignored.

    Args:
        outputs: Model output name -> audio (all of the same shape).
        table: The fold table of the model.

    Returns:
        Detail stem name -> audio, in the table's order.
    """
    folded: dict[DetailStemName, DetailAudio] = {}
    for name, sources in table.items():
        present = [outputs[source] for source in sources if source in outputs]
        if present:
            folded[name] = output_sum(present)
    return folded


def output_sum(outputs: Mapping[str, DetailAudio] | list[DetailAudio]) -> DetailAudio:
    """Return the sample-wise sum of ``outputs`` (a mapping's values or a list)."""
    arrays = list(outputs.values()) if isinstance(outputs, Mapping) else outputs
    total = np.zeros_like(arrays[0], dtype=np.float32)
    for array in arrays:
        total += array
    return total
