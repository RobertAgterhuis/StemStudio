"""Tests of the real chords pipeline with fake parts (016 T032/T062, data-model 2.3).

Order: mix the harmonic stems (and the bass stem for ``full``) at 22,050 Hz -> recognizer of the
requested engine (null = the configured one) -> post-processing -> key (skipped without chords)
-> ``fallback-engine`` when BTC fell back. Progress 0-0.05 mix, 0.05-0.80 model, then 0.85 and
0.95 for post-processing and key; the device of the model step is returned.
"""

from collections.abc import Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import numpy as np
import pytest

from stemstudio_worker.jobs.instrument_analysis_chords import (
    ChordPipelineParts,
    PipelineChordAnalyzer,
)
from stemstudio_worker.jobs.instrument_analysis_ports import ChordInputs
from stemstudio_worker.ports import (
    CHORD_SAMPLE_RATE,
    AnalysisError,
    ChordFrames,
    DeviceUse,
    KeyEstimate,
    PostProcessOptions,
)
from stemstudio_worker.schemas import (
    ANALYSIS_WARNING_TEXTS,
    AnalysisKey,
    AnalysisWarning,
    BeatGridTempo,
    ChordChart,
    ChordSegment,
    ChordsJobRequest,
    ChordVocabulary,
    InstrumentAnalysisModels,
)

STEM = "tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/stems/{}.flac"
GRID = BeatGridTempo(bpm=120, beats_per_bar=4, beats=[0.5, 1.0], downbeats=[0.5])
GPU = DeviceUse(kind="gpu", torch_device="cuda", name="GPU")


@dataclass
class FakeMixer:
    calls: list[tuple[tuple[Path, ...], int]] = field(default_factory=list)

    def mix(self, paths: Sequence[Path], sample_rate: int) -> Any:
        self.calls.append((tuple(paths), sample_rate))
        return np.full(len(self.calls), float(len(self.calls)), dtype=np.float32)


@dataclass
class FakeRecognizer:
    name: str
    fallback: bool = False
    calls: list[tuple[Any, ChordVocabulary]] = field(default_factory=list)

    @property
    def engine(self) -> str:
        return self.name

    def recognize(self, audio: Any, vocabulary: ChordVocabulary, progress: Any) -> ChordFrames:
        self.calls.append((audio, vocabulary))
        progress(0.5)
        progress(1.0)
        return ChordFrames(
            posteriors=np.ones((2, 1), dtype=np.float32),
            labels=("C:maj",),
            frame_seconds=0.1,
            chroma=np.full((2, 12), 7, dtype=np.float32),
            engine="chroma-hmm" if self.fallback else self.name,  # type: ignore[arg-type]
            device=None if self.fallback else GPU,
            fallback=self.fallback,
        )


def _segments(label: str = "C:maj") -> list[ChordSegment]:
    root = None if label == "N" else 0
    return [ChordSegment(start_ms=0, end_ms=1000, label=label, root=root, confidence=0.9)]


@dataclass
class FakePost:
    label: str = "C:maj"
    warnings: list[AnalysisWarning] = field(default_factory=list)
    calls: list[tuple[ChordFrames, PostProcessOptions]] = field(default_factory=list)

    def segments(self, frames: ChordFrames, options: PostProcessOptions) -> ChordChart:
        self.calls.append((frames, options))
        return ChordChart(
            vocabulary=options.vocabulary,
            engine=frames.engine,
            segments=_segments(self.label),
            no_chords=self.label == "N",
            warnings=list(self.warnings),
        )


@dataclass
class FakeKeys:
    calls: list[tuple[Any, Any, Any]] = field(default_factory=list)

    def estimate(self, chroma: Any, segments: Any, grid: Any) -> KeyEstimate:
        self.calls.append((chroma, segments, grid))
        return KeyEstimate(key=AnalysisKey(tonic=0, mode="major", confidence=0.8))


def _apply_key(chart: ChordChart, estimate: KeyEstimate) -> ChordChart:
    return chart.model_copy(update={"key": estimate.key})


def _analyzer(default: str = "btc", post: FakePost | None = None, fallback: bool = False):  # type: ignore[no-untyped-def]
    parts = ChordPipelineParts(
        mixer=FakeMixer(),
        recognizers={
            "btc": FakeRecognizer("btc", fallback),
            "chroma-hmm": FakeRecognizer("chroma-hmm"),
        },
        postprocessor=post or FakePost(),
        keys=FakeKeys(),
        apply_key=_apply_key,
    )
    models = InstrumentAnalysisModels(btc_majmin="ready")
    return PipelineChordAnalyzer(parts, default, lambda: models), parts  # type: ignore[arg-type]


def _request(
    vocabulary: ChordVocabulary = "simple", engine: Any = None, bass: bool = True
) -> ChordsJobRequest:
    return ChordsJobRequest(
        kind="chords",
        inputs=[STEM.format("guitar")],
        grid=GRID,
        bass_input=STEM.format("bass") if bass else None,
        vocabulary=vocabulary,
        engine=engine,
    )


INPUTS = ChordInputs(harmonic=(Path("g.flac"), Path("p.flac")), bass=Path("b.flac"))


def test_the_pipeline_mixes_recognises_post_processes_and_adds_the_key() -> None:
    analyzer, parts = _analyzer()
    seen: list[float] = []

    outcome = analyzer.analyze(_request(), INPUTS, seen.append)

    assert parts.mixer.calls == [((Path("g.flac"), Path("p.flac")), CHORD_SAMPLE_RATE)]
    audio, vocabulary = parts.recognizers["btc"].calls[0]
    assert vocabulary == "simple" and audio.tolist() == [1.0]
    frames, options = parts.postprocessor.calls[0]
    assert options == PostProcessOptions(grid=GRID, vocabulary="simple", bass=None)
    chroma, segments, grid = parts.keys.calls[0]
    assert chroma is frames.chroma and segments == _segments() and grid == GRID
    assert outcome.chart.key == AnalysisKey(tonic=0, mode="major", confidence=0.8)
    assert outcome.device == GPU
    assert seen == pytest.approx([0.0, 0.05, 0.425, 0.8, 0.85, 0.95])


def test_full_mixes_the_bass_stem_for_slash_chords() -> None:
    analyzer, parts = _analyzer()

    analyzer.analyze(_request("full"), INPUTS, lambda _f: None)

    assert parts.mixer.calls[1] == ((Path("b.flac"),), CHORD_SAMPLE_RATE)
    options = parts.postprocessor.calls[0][1]
    assert options.vocabulary == "full" and options.bass is not None
    assert options.bass.tolist() == [2.0, 2.0]


def test_full_without_a_bass_input_has_no_inversions() -> None:
    analyzer, parts = _analyzer()

    analyzer.analyze(
        _request("full", bass=False), ChordInputs(harmonic=(Path("g.flac"),)), lambda _f: None
    )

    assert len(parts.mixer.calls) == 1
    assert parts.postprocessor.calls[0][1].bass is None


def test_the_request_engine_overrides_the_configured_one() -> None:
    analyzer, parts = _analyzer()

    analyzer.analyze(_request(engine="chroma-hmm"), INPUTS, lambda _f: None)

    assert parts.recognizers["btc"].calls == []
    assert len(parts.recognizers["chroma-hmm"].calls) == 1


def test_a_null_engine_uses_the_configured_default() -> None:
    analyzer, parts = _analyzer(default="chroma-hmm")

    analyzer.analyze(_request(), INPUTS, lambda _f: None)

    assert len(parts.recognizers["chroma-hmm"].calls) == 1
    assert analyzer.default_engine == "chroma-hmm"


def test_an_engine_the_worker_does_not_have_fails_readably() -> None:
    analyzer, _ = _analyzer()

    with pytest.raises(AnalysisError, match="fake"):
        analyzer.analyze(_request(engine="fake"), INPUTS, lambda _f: None)


def test_no_chords_skip_the_key() -> None:
    analyzer, parts = _analyzer(post=FakePost(label="N"))

    outcome = analyzer.analyze(_request(), INPUTS, lambda _f: None)

    assert parts.keys.calls == []
    assert outcome.chart.key is None and outcome.chart.no_chords


def test_a_btc_fallback_adds_the_fallback_warning_once() -> None:
    warning = AnalysisWarning(
        code="fallback-engine", text=ANALYSIS_WARNING_TEXTS["fallback-engine"]
    )
    analyzer, _ = _analyzer(fallback=True)
    already, _ = _analyzer(post=FakePost(warnings=[warning]), fallback=True)

    outcome = analyzer.analyze(_request(), INPUTS, lambda _f: None)
    second = already.analyze(_request(), INPUTS, lambda _f: None)

    assert outcome.chart.warnings == [warning]
    assert second.chart.warnings == [warning]
    assert outcome.chart.engine == "chroma-hmm" and outcome.device is None


def test_the_models_readiness_comes_from_the_injected_source() -> None:
    analyzer, _ = _analyzer()

    assert analyzer.models().btc_majmin == "ready"
