"""Tests of the real/fake composition of the instrument analyses by the settings (016 T032)."""

import importlib
import types
from dataclasses import replace
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.analysis import LibrosaDrumTranscriber
from stemstudio_worker.config import (
    InstrumentAnalysisSettings,
    TranscriptionSettings,
    WorkerSettings,
)
from stemstudio_worker.devices.fake import FakeDeviceRunner
from stemstudio_worker.harmony.btc import BtcRecognizer
from stemstudio_worker.jobs.instrument_analysis_chords import PipelineChordAnalyzer
from stemstudio_worker.main import (
    MISSING_PART_TEXT,
    MissingPart,
    create_instrument_analysis_parts,
)
from stemstudio_worker.ports import AnalysisError
from stemstudio_worker.views_fake import (
    FakeChordAnalyzer,
    FakeDrumHitsTranscriber,
    FakePitchAnalyzer,
)


class _Made:
    """A stand-in adapter that records its constructor arguments."""

    def __init__(self, **kwargs: Any) -> None:
        self.kwargs = kwargs
        self.engine = kwargs.get("engine", "chroma-hmm")


def _apply_key(chart: Any, estimate: Any) -> Any:
    return chart


def _loader(modules: dict[str, dict[str, Any]]):  # type: ignore[no-untyped-def]
    def load(name: str) -> types.ModuleType:
        short = name.removeprefix("stemstudio_worker.")
        if short not in modules:
            raise ModuleNotFoundError(name)
        module = types.ModuleType(name)
        for attribute, value in modules[short].items():
            setattr(module, attribute, value)
        return module

    return load


ALL = {
    "analysis_inputs.mixdown": {"SoundfileHarmonicMixer": _Made},
    "harmony.chroma_hmm": {"ChromaHmmRecognizer": _Made},
    "harmony.postprocess": {"BeatSnapPostProcessor": _Made},
    "harmony.key": {"HmmKeyEstimator": _Made, "apply_key": _apply_key},
    "analysis.vocal_pitch": {"LibrosaPitchAnalyzer": _Made},
}


def _settings(
    tmp_path: Path, chord: str = "btc", pitch: str = "pyin", transcriber: str = "real"
) -> WorkerSettings:
    base = WorkerSettings(data_dir=tmp_path, model_dir=tmp_path / "models")
    return replace(
        base,
        instrument_analysis=InstrumentAnalysisSettings(
            chord_engine=chord,
            pitch_engine=pitch,  # type: ignore[arg-type]
            threads=2,
        ),
        transcription=TranscriptionSettings(transcriber=transcriber),  # type: ignore[arg-type]
    )


def test_real_settings_compose_btc_with_the_chroma_hmm_fallback(tmp_path: Path) -> None:
    parts, btc = create_instrument_analysis_parts(
        _settings(tmp_path), FakeDeviceRunner("cpu"), _loader(ALL)
    )

    assert isinstance(btc, BtcRecognizer) and isinstance(btc.fallback, _Made)
    assert isinstance(parts.chords, PipelineChordAnalyzer)
    assert parts.chords.default_engine == "btc"
    assert parts.chords.models().btc_majmin == "not-downloaded"
    assert isinstance(parts.pitch, _Made) and parts.pitch.kwargs == {"engine": "pyin"}
    assert parts.pitch_engine == "pyin"
    assert isinstance(parts.mixer, _Made) and parts.mixer.kwargs == {"allowed_root": None}
    assert isinstance(parts.drums, LibrosaDrumTranscriber) and parts.drum_engine == "drum-onsets"


def test_chroma_hmm_is_the_default_engine_when_configured(tmp_path: Path) -> None:
    parts, btc = create_instrument_analysis_parts(
        _settings(tmp_path, chord="chroma-hmm"), FakeDeviceRunner("cpu"), _loader(ALL)
    )

    assert parts.chords.default_engine == "chroma-hmm" and btc is not None


def test_fake_settings_compose_the_fakes(tmp_path: Path) -> None:
    settings = _settings(tmp_path, chord="fake", pitch="fake", transcriber="fake")

    parts, btc = create_instrument_analysis_parts(settings, FakeDeviceRunner("cpu"), _loader(ALL))

    assert btc is None
    assert isinstance(parts.chords, FakeChordAnalyzer)
    assert isinstance(parts.pitch, FakePitchAnalyzer) and parts.pitch_engine == "fake"
    assert isinstance(parts.drums, FakeDrumHitsTranscriber) and parts.drum_engine == "fake"


def test_the_yin_engine_is_passed_to_the_pitch_analyzer(tmp_path: Path) -> None:
    parts, _ = create_instrument_analysis_parts(
        _settings(tmp_path, pitch="yin"), FakeDeviceRunner("cpu"), _loader(ALL)
    )

    assert parts.pitch.kwargs == {"engine": "yin"}  # type: ignore[attr-defined]


def test_missing_adapters_become_parts_that_fail_readably(tmp_path: Path) -> None:
    parts, btc = create_instrument_analysis_parts(
        _settings(tmp_path), FakeDeviceRunner("cpu"), _loader({})
    )

    assert isinstance(parts.mixer, MissingPart) and isinstance(parts.pitch, MissingPart)
    assert parts.pitch_engine == "pyin"
    assert btc is not None and isinstance(btc.fallback, MissingPart)
    with pytest.raises(AnalysisError, match=MISSING_PART_TEXT):
        parts.mixer.mix([], 22_050)


def test_a_module_without_the_class_is_a_missing_part(tmp_path: Path) -> None:
    loader = _loader({**ALL, "harmony.postprocess": {}, "harmony.key": {"HmmKeyEstimator": _Made}})

    parts, _ = create_instrument_analysis_parts(
        _settings(tmp_path), FakeDeviceRunner("cpu"), loader
    )

    assert isinstance(parts.chords, PipelineChordAnalyzer)


def test_the_default_loader_starts_a_worker_with_whatever_is_installed(tmp_path: Path) -> None:
    parts, btc = create_instrument_analysis_parts(_settings(tmp_path), FakeDeviceRunner("cpu"))

    assert btc is not None and parts.chords.default_engine == "btc"
    assert importlib.import_module("stemstudio_worker.harmony.btc") is not None


def test_the_chord_model_store_keeps_weights_below_model_dir_harmony(tmp_path: Path) -> None:
    from stemstudio_worker.main import ChordModelStore  # noqa: PLC0415
    from stemstudio_worker.ports import BTC_LARGE_VOCA_WEIGHTS, BTC_MAJMIN_WEIGHTS  # noqa: PLC0415

    store = ChordModelStore(tmp_path)

    assert store.path(BTC_MAJMIN_WEIGHTS) == tmp_path / "harmony" / "btc-majmin" / "btc_model.pt"
    assert store.path(BTC_LARGE_VOCA_WEIGHTS).name == "btc_model_large_voca.pt"
    assert not store.ready(BTC_MAJMIN_WEIGHTS)


def test_a_fake_worker_composes_the_fake_analyses(tmp_path: Path) -> None:
    from fastapi.testclient import TestClient  # noqa: PLC0415

    from stemstudio_worker.main import create_app  # noqa: PLC0415

    settings = WorkerSettings(data_dir=tmp_path, separator="fake", model_dir=tmp_path / "m")
    with TestClient(create_app(settings)) as client:
        service = client.app.state.runtime.lanes.instrument_analysis  # type: ignore[attr-defined]
        parts = service.parts

    assert isinstance(parts.chords, FakeChordAnalyzer)
    assert isinstance(parts.pitch, FakePitchAnalyzer)


def test_the_idle_hook_releases_the_btc_models(tmp_path: Path) -> None:
    from stemstudio_worker.main import _GpuModels  # noqa: PLC0415

    released: list[str] = []

    class Model:
        def __init__(self, name: str) -> None:
            self.name = name

        def release(self) -> None:
            released.append(self.name)

    class Devices:
        def release(self) -> None:
            released.append("devices")

    models = _GpuModels(object(), Model("aligner"), Model("btc"))
    models.idle_release(Devices())()  # type: ignore[arg-type]

    assert released == ["aligner", "btc", "devices"]
