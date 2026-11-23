"""Scriptable fakes of the instrument analysis parts and request bodies (016 T061, T084, T093).

Built from the worker 1.9.0 contract examples (consumer-driven): the chord chart of
``completedChords``, the drum pieces of ``drumHits`` and the vocal stem of ``vocalPitch``.
"""

import threading
from collections.abc import Mapping, Sequence
from pathlib import Path
from typing import Any

import numpy as np

from stemstudio_worker.jobs.instrument_analysis_ports import (
    ChordInputs,
    ChordOutcome,
    InstrumentAnalysisParts,
)
from stemstudio_worker.ports import (
    AnalysisProgress,
    DeviceUse,
    DrumHit,
    MonoAudio,
    TranscriptionProgress,
)
from stemstudio_worker.schemas import (
    AnalysisKey,
    ChordChart,
    ChordEngine,
    ChordSegment,
    ChordsJobRequest,
    DrumPiece,
    InstrumentAnalysisModels,
    KeyRegion,
    PitchEngine,
    PitchRange,
    RenderRange,
    VocalPitch,
    VocalSource,
)

WAIT = 5.0
TRACK = "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a"
HARMONIC = ("bass", "guitar", "piano", "other")
PIECES: tuple[DrumPiece, ...] = ("kick", "snare", "toms", "hihat", "cymbals")


def stem(name: str) -> str:
    """Library path of a standard stem of the contract track."""
    return f"tracks/{TRACK}/stems/{name}.flac"


def drum(piece: str) -> str:
    """Library path of a drum detail stem of the contract track."""
    return f"tracks/{TRACK}/detail/drums/{piece}.flac"


LEAD = f"tracks/{TRACK}/detail/vocals/lead.flac"


def write_library(root: Path) -> None:
    """The stems, drum pieces and the lead vocal of the contract examples below ``root``."""
    paths = [stem(n) for n in (*HARMONIC, "vocals")] + [drum(p) for p in PIECES] + [LEAD]
    for path in paths:
        target = root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(b"fLaC")


def chords_body(**changes: Any) -> dict[str, Any]:
    """The ``chords`` example request with ``changes``."""
    body: dict[str, Any] = {
        "kind": "chords",
        "inputs": [stem(n) for n in HARMONIC],
        "bassInput": stem("bass"),
        "grid": {"kind": "beat-grid", "bpm": 120, "beatsPerBar": 4,
                 "beats": [0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0], "downbeats": [0.5, 2.5]},
        "vocabulary": "simple",
        "engine": None,
    }  # fmt: skip
    body.update(changes)
    return body


def drums_body(pieces: Sequence[str] = PIECES) -> dict[str, Any]:
    """The ``drumHits`` example request with ``pieces``."""
    return {"kind": "drum-hits", "drumInputs": {p: drum(p) for p in pieces}}


def pitch_body(**changes: Any) -> dict[str, Any]:
    """The ``vocalPitch`` example request with ``changes``."""
    return {"kind": "vocal-pitch", "input": LEAD, "source": "vocals.lead", **changes}


def example_chart(engine: ChordEngine = "btc") -> ChordChart:
    """The chart of the contract example ``completedChords``."""
    return ChordChart(
        vocabulary="simple", engine=engine,
        key=AnalysisKey(tonic=0, mode="major", confidence=0.81),
        key_regions=[KeyRegion(start_ms=500, end_ms=16_500, tonic=0, mode="major", confidence=0.84),
                     KeyRegion(start_ms=16_500, end_ms=32_500, tonic=2, mode="major",
                               confidence=0.77)],
        beat_snapped=True,
        segments=[ChordSegment(start_ms=0, end_ms=500, label="N", confidence=0.95),
                  ChordSegment(start_ms=500, end_ms=2500, label="C:maj", root=0, quality="maj",
                               confidence=0.9),
                  ChordSegment(start_ms=2500, end_ms=4500, label="G:maj", root=7, quality="maj",
                               confidence=0.88)],
    )  # fmt: skip


class Gate:
    """Holds a step after its first progress report until ``gate`` is set."""

    def __init__(self) -> None:
        self.gate = threading.Event()
        self.gate.set()
        self.entered = threading.Event()
        self.error: Exception | None = None

    def hold(self, progress: Any) -> None:
        """Report 0.5, signal ``entered``, wait for the gate, then raise ``error`` if set."""
        progress(0.5)
        self.entered.set()
        assert self.gate.wait(WAIT)
        if self.error is not None:
            raise self.error


class FakeChords(Gate):
    """``ChordAnalyzer``: records the request and files, returns the example chart."""

    def __init__(self, default: ChordEngine = "btc", device: DeviceUse | None = None) -> None:
        super().__init__()
        self.default = default
        self.device = device
        self.calls: list[tuple[ChordsJobRequest, ChordInputs]] = []

    @property
    def default_engine(self) -> ChordEngine:
        return self.default

    def models(self) -> InstrumentAnalysisModels:
        return InstrumentAnalysisModels(btc_majmin="ready")

    def analyze(
        self, request: ChordsJobRequest, inputs: ChordInputs, progress: AnalysisProgress
    ) -> ChordOutcome:
        self.calls.append((request, inputs))
        self.hold(progress)
        return ChordOutcome(chart=example_chart(), device=self.device)


class FakeDrums(Gate):
    """``DrumTranscriber``: one hit per piece at 0.25 s steps (or ``hits``), recorded inputs."""

    def __init__(self) -> None:
        super().__init__()
        self.calls: list[Mapping[DrumPiece, Path]] = []
        self.hits: tuple[DrumHit, ...] | None = None

    def transcribe(
        self,
        inputs: Mapping[DrumPiece, Path],
        window: RenderRange | None,
        progress: TranscriptionProgress,
    ) -> tuple[DrumHit, ...]:
        self.calls.append(inputs)
        self.hold(progress)
        if self.hits is not None:
            return self.hits
        return tuple(DrumHit(0.25 * i, piece, 1.5 if i == 0 else 0.5)
                     for i, piece in reversed(list(enumerate(inputs))))  # fmt: skip


class FakePitch(Gate):
    """``PitchAnalyzer``: three frames, records the audio and source."""

    def __init__(self) -> None:
        super().__init__()
        self.calls: list[tuple[MonoAudio, VocalSource]] = []
        self.engine_name: PitchEngine = "pyin"

    @property
    def engine(self) -> PitchEngine:
        return self.engine_name

    def analyze(
        self, audio: MonoAudio, source: VocalSource, progress: AnalysisProgress
    ) -> VocalPitch:
        self.calls.append((audio, source))
        self.hold(progress)
        return VocalPitch(frames=[0, 6900, 6950], source=source, engine=self.engine_name,
                          voiced_ratio=2 / 3,
                          range=PitchRange(low_cents=6900, high_cents=6950))  # fmt: skip


class FakeMixer:
    """``HarmonicMixer``: 0.5 s of silence per call, records paths and rates."""

    def __init__(self) -> None:
        self.calls: list[tuple[tuple[Path, ...], int]] = []

    def mix(self, paths: Sequence[Path], sample_rate: int) -> MonoAudio:
        self.calls.append((tuple(paths), sample_rate))
        return np.zeros(sample_rate // 2, dtype=np.float32)


class Fakes:
    """One set of the fakes above, as ``InstrumentAnalysisParts``."""

    def __init__(self, device: DeviceUse | None = None) -> None:
        self.chords = FakeChords(device=device)
        self.drums = FakeDrums()
        self.pitch = FakePitch()
        self.mixer = FakeMixer()

    def parts(self) -> InstrumentAnalysisParts:
        return InstrumentAnalysisParts(chords=self.chords, drums=self.drums,
                                       drum_engine="drum-onsets", pitch=self.pitch,
                                       mixer=self.mixer)  # fmt: skip
