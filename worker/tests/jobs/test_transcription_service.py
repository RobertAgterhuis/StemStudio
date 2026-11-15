"""Tests of the transcription service and pipeline with in-test parts (008 T038/T039).

Edge cases behind the routes: a library that cannot be read when the job is created, a delete
while running, the piano CRNN's device and the ``cpu-piano`` warning, ranges, unreadable inputs
and the stand-in parts of a runtime built without transcription.
"""

import threading
from collections.abc import Iterator, Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import numpy as np
import pytest
import soundfile as sf

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.transcription_defaults import (
    TRANSCRIPTION_UNAVAILABLE_TEXT,
    default_transcription_parts,
)
from stemstudio_worker.jobs.transcription_pipeline import ScoreRules, TranscriptionParts
from stemstudio_worker.jobs.transcription_service import TranscriptionService
from stemstudio_worker.ports import (
    DeviceUse,
    EngravedScore,
    InputNotFoundError,
    NotationSettings,
    NoteEvent,
    RenderError,
    ScoreDocument,
    Transcription,
)
from stemstudio_worker.schemas import (
    LIBRARY_UNAVAILABLE_TEXT,
    SCORE_WARNING_TEXTS,
    LibraryHealth,
    RenderJobStatus,
    RenderRange,
    ScoreWarning,
    TranscriptionJobRequest,
    TranscriptionModels,
)

from .conftest import T0

S = RenderJobStatus
WAIT = 5.0
INPUT = "tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/stems/piano.flac"


def _request(**changes: Any) -> TranscriptionJobRequest:
    body: dict[str, Any] = {
        "profile": "piano", "input": {"path": INPUT}, "titles": {"title": "Piano"},
        "grid": {"kind": "manual", "bpm": 120, "beatsPerBar": 4, "firstDownbeatSeconds": 0},
    }  # fmt: skip
    return TranscriptionJobRequest.model_validate({**body, **changes})


@dataclass
class Library:
    """Resolves every path to ``file`` (or raises ``error``)."""

    file: Path
    error: Exception | None = None

    def status(self) -> LibraryHealth:
        return LibraryHealth(readable=True)

    def resolve(self, path: str) -> Path:
        if self.error is not None:
            raise self.error
        return self.file


@dataclass
class Engine:
    """A pitched engine posing as the piano CRNN; ``gate`` holds it inside ``transcribe``."""

    use: DeviceUse | None = None
    gate: threading.Event = field(default_factory=threading.Event)
    entered: threading.Event = field(default_factory=threading.Event)
    engine: str = "piano-crnn"
    attribution: str | None = "credit"

    def __post_init__(self) -> None:
        self.gate.set()

    def pitched(self, request: Any, pitch_range: Any) -> "Engine":
        return self

    def drums(self, request: Any) -> Any:
        raise AssertionError("no drums here")

    def transcribe(self, path: Path, window: Any, progress: Any) -> Transcription:
        self.entered.set()
        assert self.gate.wait(WAIT)
        progress(0.5)
        return Transcription(notes=(NoteEvent(0.0, 0.5, 60, 0.8),), device=self.use)

    def models_health(self) -> TranscriptionModels:
        return TranscriptionModels()

    def release(self) -> None:
        pass


class Notator:
    def __init__(self) -> None:
        self.settings: list[NotationSettings] = []

    def notate(self, notes: Sequence[NoteEvent], settings: NotationSettings) -> ScoreDocument:
        self.settings.append(settings)
        return ScoreDocument(b"<x/>", b"MThd", None, 120, "4/4", 1, len(notes))

    notate_drums = notate


class Engraver:
    def engrave(self, musicxml: bytes, title: str, subject: str | None) -> EngravedScore:
        return EngravedScore(b"%PDF", 1)


def _warning(code: Any, **values: object) -> ScoreWarning:
    return ScoreWarning(code=code, text=SCORE_WARNING_TEXTS[code].format(**values))


RULES = ScoreRules(pitch_range=lambda p: (21, 108), resolution=lambda p, r: r or "sixteenth",
                   warning=_warning)  # fmt: skip


class Env:
    def __init__(self, root: Path) -> None:
        self.paths = DataPaths(root)
        self.file = root / "piano.flac"
        sf.write(self.file, np.zeros((44_100 * 2, 2)), 44_100, format="FLAC")
        self.library = Library(self.file)
        self.engine = Engine()
        self.notator = Notator()
        self.services: list[TranscriptionService] = []

    def start(self, parts: TranscriptionParts | None = None) -> TranscriptionService:
        parts = parts or TranscriptionParts(self.engine, self.notator, Engraver(), RULES,  # type: ignore[arg-type]
                                            self.library, lambda: True)  # fmt: skip
        service = TranscriptionService.start(self.paths, parts, lambda: T0)
        self.services.append(service)
        return service


@pytest.fixture
def env(tmp_path: Path) -> Iterator[Env]:
    environment = Env(tmp_path)
    yield environment
    for service in environment.services:
        service.stop(WAIT)


def _run(service: TranscriptionService, request: TranscriptionJobRequest) -> Any:
    job = service.create(request)
    assert service.join(WAIT)
    return service.get(job.transcription_job_id)


def test_the_piano_crnn_on_the_cpu_adds_the_cpu_piano_warning(env: Env) -> None:
    env.engine.use = DeviceUse(kind="cpu", torch_device="cpu", fallback_reason="Out of memory")

    job = _run(env.start(), _request())

    assert job.status is S.COMPLETED
    assert [w.code for w in job.result.summary.warnings] == ["cpu-piano"]
    assert (job.result.summary.quality, job.result.summary.attribution) == ("accurate", "credit")
    assert (job.device.kind, job.device.cpu_fallback) == ("cpu", True)


def test_the_piano_crnn_on_the_gpu_reports_the_gpu(env: Env) -> None:
    env.engine.use = DeviceUse(kind="gpu", torch_device="cuda", name="GPU")

    job = _run(env.start(), _request())

    assert job.result.summary.warnings == []
    assert (job.device.kind, job.device.name) == ("gpu", "GPU")


def test_a_range_sets_the_notation_length(env: Env) -> None:
    _run(env.start(), _request(range={"startSeconds": 0.5, "endSeconds": 1.75}))

    assert env.notator.settings[0].duration_seconds == pytest.approx(1.25)


def test_an_unreadable_input_fails_the_job(env: Env) -> None:
    env.file.write_bytes(b"no audio")

    job = _run(env.start(), _request())

    assert (job.status, job.error) == (S.FAILED, "The audio file could not be read.")


def test_a_library_that_cannot_be_read_now_queues_the_job_and_fails_it(env: Env) -> None:
    env.library.error = RenderError(LIBRARY_UNAVAILABLE_TEXT)

    job = _run(env.start(), _request())

    assert (job.status, job.error) == (S.FAILED, LIBRARY_UNAVAILABLE_TEXT)


def test_missing_inputs_are_refused_when_the_job_is_created(env: Env) -> None:
    env.library.error = InputNotFoundError("Input file does not exist: x")

    with pytest.raises(InputNotFoundError):
        env.start().create(_request())


def test_deleting_a_running_job_stops_it_and_leaves_nothing(env: Env) -> None:
    env.engine.gate.clear()
    service = env.start()
    job = service.create(_request())
    assert env.engine.entered.wait(WAIT)

    service.delete(job.transcription_job_id)
    env.engine.gate.set()
    assert service.join(WAIT)

    assert service.get(job.transcription_job_id) is None
    assert list(env.paths.transcription_jobs.iterdir()) == []


def test_unknown_and_finished_jobs_are_not_run_again(env: Env) -> None:
    service = env.start()
    job = _run(service, _request())

    service.process(job.transcription_job_id)
    service.process("e" * 32)

    assert service.get(job.transcription_job_id).status is S.COMPLETED  # type: ignore[union-attr]
    assert len(env.notator.settings) == 1


def test_a_runtime_without_transcription_parts_fails_every_job(env: Env) -> None:
    parts = default_transcription_parts(env.library)
    service = env.start(parts)

    job = _run(service, _request())
    kick = {"kick": INPUT.replace("stems/piano", "detail/drums/kick")}
    drums = _run(service, _request(profile="drums", input=None, drumInputs=kick))

    assert (job.error, drums.error) == (
        TRANSCRIPTION_UNAVAILABLE_TEXT,
        TRANSCRIPTION_UNAVAILABLE_TEXT,
    )
    assert service.health().model_dump(by_alias=True)["font"] == {"leipzig": False}
    assert parts.rules.pitch_range("bass") == (0, 127)
    assert parts.rules.resolution("bass", None) == "sixteenth"
    assert parts.rules.warning("few-notes").code == "few-notes"
    with pytest.raises(Exception, match="not available"):
        parts.notator.notate([], None)  # type: ignore[arg-type]
    with pytest.raises(Exception, match="not available"):
        parts.engraver.engrave(b"", "t", None)
    parts.engines.release()


def test_window_lengths_come_from_the_range(env: Env) -> None:
    window = RenderRange(start_seconds=1.0, end_seconds=2.0)

    job = _run(env.start(), _request(range=window.model_dump(by_alias=True)))

    assert job.status is S.COMPLETED
