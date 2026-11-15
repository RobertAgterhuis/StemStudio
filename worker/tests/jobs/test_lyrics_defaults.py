"""Tests of the feature 006 stand-ins of a runtime built without lyrics adapters (006 T029)."""

from pathlib import Path

import numpy as np
import pytest

from stemstudio_worker.jobs.lyrics_defaults import default_lyrics_parts
from stemstudio_worker.ports import WHISPER_GPU_NEED, AlignmentError, AlignRequest, LyricsQuery
from stemstudio_worker.schemas import ALIGNMENT_FAILED_TEXT, CPU_DEVICE


def _ignore(fraction: float, stage: str) -> None:
    """A progress callback that drops every report."""


def test_lookups_are_disabled_and_alignments_fail_on_the_cpu(tmp_path: Path) -> None:
    parts = default_lyrics_parts()
    request = AlignRequest(np.zeros(16, dtype=np.float32), "Fake line one")

    result = parts.lookup.lookup(LyricsQuery("Fake Song", 180.0), _ignore)

    assert (result.outcome, result.lyrics) == ("disabled", None)
    assert parts.devices.info == CPU_DEVICE
    assert parts.devices.run(lambda device: device, WHISPER_GPU_NEED)[0] == "cpu"
    with pytest.raises(AlignmentError, match=ALIGNMENT_FAILED_TEXT):
        parts.aligner.align(request, "cpu", _ignore)
    with pytest.raises(AlignmentError, match=ALIGNMENT_FAILED_TEXT):
        parts.vocals.load(tmp_path / "vocals.flac")
