"""Shared helpers of the analyzer tests: input kinds and result lookup."""

from typing import cast

from stemstudio_worker.schemas import QualityInputKind, QualityResult, StemQuality


def kind_of(channel: str) -> QualityInputKind:
    """``standard`` = no dot, ``rest`` = ``<parent>.rest``, else ``detail`` (data-model 2.3)."""
    if "." not in channel:
        return "standard"
    return cast("QualityInputKind", "rest" if channel.endswith(".rest") else "detail")


def stem_of(result: QualityResult, channel: str) -> StemQuality:
    """The estimate of one channel."""
    return next(stem for stem in result.stems if stem.channel == channel)
