"""Tests of the stable-ts aligner adapter with a fake Whisper runtime (006 T040, FR-004, FR-014).

Binding rule (research.md P0-2): default options with ``original_split=True``; a collapsed run is
retried once with ``vad=True``; a second collapse fails with ``ALIGNMENT_FAILED_TEXT``.
"""

from pathlib import Path

import numpy as np
import pytest

from stemstudio_worker.alignment.stable_ts_aligner import StableTsAligner
from stemstudio_worker.alignment.weights import WhisperWeights
from stemstudio_worker.ports import AlignmentCancelledError, AlignmentError, AlignRequest
from stemstudio_worker.schemas import (
    ALIGNMENT_FAILED_TEXT,
    LYRICS_STAGE_ALIGNING,
    LYRICS_STAGE_DOWNLOADING,
    SPEECH_MODEL_DOWNLOAD_TEXT,
)

from .fakes import TEXT, FakeRuntime, collapsed, spread

AUDIO = np.zeros(16_000, dtype=np.float32)


class Progress:
    """Records ``(fraction, stage)`` reports; raises ``error`` once ``fail_at`` is reached."""

    def __init__(self, fail_at: float | None = None) -> None:
        self.reports: list[tuple[float, str]] = []
        self.fail_at = fail_at

    def __call__(self, fraction: float, stage: str) -> None:
        self.reports.append((fraction, stage))
        if self.fail_at is not None and fraction >= self.fail_at:
            raise AlignmentCancelledError("cancelled")


def _weights(root: Path, cached: bool = True, name: str = "turbo") -> WhisperWeights:
    weights = WhisperWeights(name=name, root=root / "whisper", file_name="large-v3-turbo.pt",
                             size_bytes=7)  # fmt: skip
    if cached:
        weights.root.mkdir(parents=True)
        weights.path.write_bytes(b"weights")
    return weights


def _aligner(tmp_path: Path, runtime: FakeRuntime, cached: bool = True) -> StableTsAligner:
    return StableTsAligner(_weights(tmp_path, cached), runtime)


def test_an_alignment_keeps_our_line_breaks_and_reports_progress(tmp_path: Path) -> None:
    runtime = FakeRuntime()
    progress = Progress()

    document = _aligner(tmp_path, runtime).align(AlignRequest(AUDIO, TEXT, "en"), "cuda", progress)

    assert runtime.loads == [("turbo", "cuda", tmp_path / "whisper")]
    assert runtime.downloads == []
    kind, options = runtime.models[0].calls[0]
    assert (kind, options["text"], options["language"]) == ("align", TEXT, "en")
    assert (options["original_split"], options["vad"]) == (True, False)
    assert [line.text for line in document.lines] == ["Fake line one", "La la la"]
    assert progress.reports == [(0.0, LYRICS_STAGE_ALIGNING), (0.5, LYRICS_STAGE_ALIGNING),
                                (1.0, LYRICS_STAGE_ALIGNING)]  # fmt: skip


def test_missing_weights_are_downloaded_into_the_model_dir_first(tmp_path: Path) -> None:
    runtime = FakeRuntime()
    progress = Progress()

    _aligner(tmp_path, runtime, cached=False).align(
        AlignRequest(AUDIO, TEXT, "en"), "cpu", progress
    )

    assert runtime.downloads == [("turbo", tmp_path / "whisper")]
    assert progress.reports[0] == (0.0, LYRICS_STAGE_DOWNLOADING)
    assert progress.reports[1] == (0.0, LYRICS_STAGE_ALIGNING)


def test_a_failed_download_gives_the_fixed_text(tmp_path: Path) -> None:
    runtime = FakeRuntime(download_error=OSError("offline"))

    with pytest.raises(AlignmentError, match=SPEECH_MODEL_DOWNLOAD_TEXT):
        _aligner(tmp_path, runtime, cached=False).align(
            AlignRequest(AUDIO, TEXT, "en"), "cpu", Progress()
        )


def test_a_model_load_error_propagates_for_the_cpu_retry(tmp_path: Path) -> None:
    runtime = FakeRuntime(load_error=RuntimeError("CUDA out of memory"))

    with pytest.raises(RuntimeError, match="out of memory"):
        _aligner(tmp_path, runtime).align(AlignRequest(AUDIO, TEXT, "en"), "cuda", Progress())


def test_the_model_is_kept_per_device_until_released(tmp_path: Path) -> None:
    runtime = FakeRuntime(results=[spread(), spread(), spread()])
    aligner = _aligner(tmp_path, runtime)
    request = AlignRequest(AUDIO, TEXT, "en")

    aligner.align(request, "cuda", Progress())
    aligner.align(request, "cuda", Progress())
    aligner.release()
    aligner.align(request, "cuda", Progress())

    assert [load[1] for load in runtime.loads] == ["cuda", "cuda"]


def test_without_a_language_hint_the_language_is_detected(tmp_path: Path) -> None:
    runtime = FakeRuntime(detected="nl")

    document = _aligner(tmp_path, runtime).align(AlignRequest(AUDIO, TEXT), "cpu", Progress())

    assert runtime.models[0].calls[0][1]["language"] == "nl"
    assert document.language == "en"


def test_a_collapsed_run_is_retried_once_with_vad(tmp_path: Path) -> None:
    runtime = FakeRuntime(results=[collapsed(), spread()])

    document = _aligner(tmp_path, runtime).align(AlignRequest(AUDIO, TEXT, "en"), "cpu", Progress())

    assert [options["vad"] for _kind, options in runtime.models[0].calls] == [False, True]
    assert document.lines[0].words[0].start_ms == 1000


@pytest.mark.parametrize("results", [[collapsed(), collapsed()], [None, None]])
def test_a_second_collapse_fails_the_alignment(tmp_path: Path, results: list[object]) -> None:
    runtime = FakeRuntime(results=results)  # type: ignore[arg-type]

    with pytest.raises(AlignmentError, match=ALIGNMENT_FAILED_TEXT):
        _aligner(tmp_path, runtime).align(AlignRequest(AUDIO, TEXT, "en"), "cpu", Progress())


def test_a_refine_aligns_words_inside_the_given_lines(tmp_path: Path) -> None:
    first = _aligner(tmp_path, FakeRuntime()).align(
        AlignRequest(AUDIO, TEXT, "en"), "cpu", Progress()
    )
    refine = first.model_copy(update={"sync_level": "line", "origin": "interpolated"})
    runtime = FakeRuntime()

    document = StableTsAligner(_weights(tmp_path / "b"), runtime).align(
        AlignRequest(AUDIO, TEXT, None, refine), "cpu", Progress()
    )

    kind, options = runtime.models[0].calls[0]
    assert kind == "align_words"
    assert options["language"] == "en"
    assert options["segments"] == [
        {"start": 1.0, "end": 2.2, "text": "Fake line one"},
        {"start": 2.2, "end": 3.4, "text": "La la la"},
    ]
    assert [(line.start_ms, line.end_ms) for line in document.lines] == [(1000, 2200), (2200, 3400)]


def test_a_cancel_from_the_progress_callback_propagates(tmp_path: Path) -> None:
    runtime = FakeRuntime()

    with pytest.raises(AlignmentCancelledError):
        _aligner(tmp_path, runtime).align(AlignRequest(AUDIO, TEXT, "en"), "cpu", Progress(0.5))


def test_weights_report_whether_they_are_cached(tmp_path: Path) -> None:
    weights = _weights(tmp_path, cached=False)
    assert not weights.cached()
    weights.root.mkdir(parents=True)
    weights.path.write_bytes(b"short")
    assert not weights.cached()
    weights.path.write_bytes(b"weights")
    assert weights.cached()
    unsized = WhisperWeights("small", weights.root, "large-v3-turbo.pt", None)
    assert unsized.cached()


def test_an_unusable_language_is_dropped(tmp_path: Path) -> None:
    result = spread()
    result.language = None
    runtime = FakeRuntime(results=[result], detected="english")

    document = _aligner(tmp_path, runtime).align(AlignRequest(AUDIO, TEXT), "cpu", Progress())

    assert document.language is None
