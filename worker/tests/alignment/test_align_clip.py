"""SC-003 on the real model: >= 80 % of word starts within 300 ms on the qa clip (006 T042).

Deselected by default (``-m integration``); downloads Whisper large-v3-turbo (1.6 GB) into
``STEMSTUDIO_MODEL_DIR/whisper`` on first use and runs on the CPU unless ``STEMSTUDIO_DEVICE``
selects ``cuda``. Executed by the CTO in 006 T066. The clip and its exact word annotation come
from qa (006 T012): ``tests/fixtures/lyrics/align-clip.json`` (``file``, ``language``,
``plainText``, ``lines[].words[].startMs``, ``sc003``); the audio is generated with
``uv run --project worker python tests/fixtures/generate_lyrics_fixtures.py``.
"""

import json
import os
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.alignment import create_aligner, create_vocal_loader
from stemstudio_worker.config import load_settings
from stemstudio_worker.ports import AlignRequest

FIXTURES = Path(__file__).resolve().parents[3] / "tests" / "fixtures"
CLIP = FIXTURES / "lyrics" / "align-clip.json"


def _ignore(fraction: float, stage: str) -> None:
    """Drop progress reports."""


def _clip() -> dict[str, Any]:
    clip: dict[str, Any] = json.loads(CLIP.read_text(encoding="utf-8"))
    if not (FIXTURES / "audio" / clip["file"]).is_file():
        pytest.skip("generate the clip with tests/fixtures/generate_lyrics_fixtures.py")
    return clip


@pytest.mark.integration
def test_the_real_aligner_meets_sc_003_on_the_annotated_clip() -> None:
    clip = _clip()
    settings = load_settings({**os.environ, "STEMSTUDIO_SEPARATOR": "demucs"})
    audio = create_vocal_loader().load(FIXTURES / "audio" / clip["file"])
    device = "cuda" if settings.device == "cuda" else "cpu"

    document = create_aligner(settings).align(
        AlignRequest(audio, clip["plainText"], clip["language"]), device, _ignore
    )

    starts = [w.start_ms or 0 for line in document.lines for w in line.words]
    expected = [w["startMs"] for line in clip["lines"] for w in line["words"]]
    tolerance = clip["sc003"]["toleranceMs"]
    hits = sum(abs(got - want) <= tolerance for got, want in zip(starts, expected, strict=True))
    print(f"SC-003: {hits}/{len(expected)} word starts within {tolerance} ms")  # noqa: T201
    assert hits / len(expected) >= clip["sc003"]["minWithinShare"]
