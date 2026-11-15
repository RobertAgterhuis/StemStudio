"""SC-003 on the real model: >= 80 % of word starts within 300 ms on the qa clip (006 T042).

Deselected by default (``-m integration``); downloads Whisper large-v3-turbo (1.6 GB) into
``STEMSTUDIO_MODEL_DIR/whisper`` on first use and runs on the CPU unless ``STEMSTUDIO_DEVICE``
selects ``cuda``. Executed by the CTO in 006 T066. The clip and its manual word annotation come
from qa (006 T012): ``tests/fixtures/lyrics/align-clip.json`` = ``{"audio": "<file next to it>",
"language": "en", "text": "<lines>", "words": [{"text": ..., "startMs": ...}, ...]}``.
"""

import json
import os
from pathlib import Path

import pytest

from stemstudio_worker.alignment import create_aligner, create_vocal_loader
from stemstudio_worker.config import load_settings
from stemstudio_worker.ports import AlignRequest

CLIP = Path(__file__).resolve().parents[3] / "tests" / "fixtures" / "lyrics" / "align-clip.json"
TOLERANCE_MS = 300
REQUIRED_SHARE = 0.8


def _ignore(fraction: float, stage: str) -> None:
    """Drop progress reports."""


@pytest.mark.integration
def test_the_real_aligner_meets_sc_003_on_the_annotated_clip() -> None:
    if not CLIP.is_file():
        pytest.skip(f"qa clip {CLIP} is not there yet (006 T012)")
    clip = json.loads(CLIP.read_text(encoding="utf-8"))
    settings = load_settings({**os.environ, "STEMSTUDIO_SEPARATOR": "demucs"})
    audio = create_vocal_loader().load(CLIP.parent / clip["audio"])
    device = "cuda" if settings.device == "cuda" else "cpu"

    document = create_aligner(settings).align(
        AlignRequest(audio, clip["text"], clip.get("language")), device, _ignore
    )

    starts = [w.start_ms for line in document.lines for w in line.words]
    expected = [w["startMs"] for w in clip["words"]]
    assert len(starts) == len(expected)
    hits = sum(
        abs((got or 0) - want) <= TOLERANCE_MS for got, want in zip(starts, expected, strict=True)
    )
    print(f"SC-003: {hits}/{len(expected)} word starts within {TOLERANCE_MS} ms")  # noqa: T201
    assert hits / len(expected) >= REQUIRED_SHARE
