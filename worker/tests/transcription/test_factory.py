"""Tests of the transcription factories: real or fake engines by settings (008 T027)."""

from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.config import TranscriptionSettings, WorkerSettings
from stemstudio_worker.devices.fake import FakeDeviceRunner
from stemstudio_worker.schemas import BeatGridTempo, TranscriptionJobRequest
from stemstudio_worker.transcription import create_transcription_engines
from stemstudio_worker.transcription.basic_pitch import BasicPitchTranscriber
from stemstudio_worker.transcription.fake import FakeDrumTranscriber, FakePitchTranscriber
from stemstudio_worker.transcription.piano import PianoTranscriber

GRID = BeatGridTempo(bpm=120, beats_per_bar=4, beats=[0.0, 0.5], downbeats=[0.0])


class Drums:
    """The real drum transcriber stand-in (beat-analysis)."""

    def transcribe(self, inputs: Any, window: Any, progress: Any) -> tuple[()]:
        return ()


def _request(profile: str, quality: str = "accurate") -> TranscriptionJobRequest:
    body: dict[str, Any] = {"profile": profile, "quality": quality, "grid": GRID,
                            "titles": {"title": "Score"}}  # fmt: skip
    if profile == "drums":
        body["drumInputs"] = {"kick": "tracks/x/detail/drums/kick.flac"}
    else:
        body["input"] = {"path": "tracks/x/stems/bass.flac"}
    return TranscriptionJobRequest.model_validate(body)


def _engines(tmp_path: Path, transcriber: str) -> Any:
    settings = WorkerSettings(
        data_dir=tmp_path, model_dir=tmp_path / "models",
        transcription=TranscriptionSettings(transcriber=transcriber, threads=3),  # type: ignore[arg-type]
    )  # fmt: skip
    return create_transcription_engines(settings, FakeDeviceRunner("cpu"), Drums())


def test_the_real_engines_follow_the_profile_and_quality(tmp_path: Path) -> None:
    engines = _engines(tmp_path, "real")

    bass = engines.pitched(_request("bass"), (28, 67))
    piano = engines.pitched(_request("piano"), (21, 108))
    fast = engines.pitched(_request("piano", "fast"), (21, 108))

    assert isinstance(bass, BasicPitchTranscriber)
    assert isinstance(piano, PianoTranscriber)
    assert fast is bass
    assert engines.pitched(_request("keys"), (21, 108)) is bass
    assert isinstance(engines.drums(_request("drums")), Drums)


def test_the_fake_engines_play_on_the_request_grid(tmp_path: Path) -> None:
    engines = _engines(tmp_path, "fake")

    assert isinstance(engines.pitched(_request("vocals"), (40, 84)), FakePitchTranscriber)
    assert isinstance(engines.drums(_request("drums")), FakeDrumTranscriber)


def test_model_readiness_comes_from_the_model_store(tmp_path: Path) -> None:
    engines = _engines(tmp_path, "real")

    models = engines.models_health()

    assert models.model_dump(by_alias=True) == {
        "basic-pitch-icassp2022": "not-downloaded", "piano-crnn-note-pedal": "not-downloaded",
    }  # fmt: skip


def test_release_drops_the_piano_models(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    engines = _engines(tmp_path, "real")
    released: list[int] = []
    piano = engines.pitched(_request("piano"), (21, 108))
    monkeypatch.setattr(piano, "release", lambda: released.append(1))

    engines.release()

    assert released == [1]
