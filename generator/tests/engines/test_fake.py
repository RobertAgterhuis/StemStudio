"""Tests for the fake music engine used by CI and E2E (009 T020, data-model 1)."""

from pathlib import Path

import numpy as np
import pytest
import soundfile as sf

from stemstudio_generator.config import FakeGeneratorMode
from stemstudio_generator.engines.fake import (
    FAKE_BUSY_FREE_MEMORY_MIB,
    FAKE_FREE_MEMORY_MIB,
    FAKE_TOTAL_MEMORY_MIB,
    SLOW_FACTOR,
    FakeMusicEngine,
    fake_free_memory_mib,
)
from stemstudio_generator.ports import (
    CONVERSION_NEEDS,
    COVER_TURBO_LOWMEM,
    Cancelled,
    EngineRestyle,
    GeneratorError,
    GpuOutOfMemoryError,
    GpuRequiredError,
    MusicEngine,
)

from ..signals import stereo, tone, write
from .fakes import Recorder, fake_settings, out_dir, restyle_params, use


def _engine(
    mode: FakeGeneratorMode = "ready", step: float = 1.0
) -> tuple[FakeMusicEngine, Recorder]:
    recorder = Recorder()
    return FakeMusicEngine(fake_settings(mode, step), recorder.sleep), recorder


def _request(tmp_path: Path, seeds: list[int], name: str = "out") -> EngineRestyle:
    source = write(tmp_path / "source.wav", stereo(tone(10.0, 44_100)), 44_100, "FLOAT")
    return EngineRestyle(source, restyle_params(seeds), out_dir(tmp_path, name))


def test_fake_engine_is_a_music_engine() -> None:
    assert isinstance(_engine()[0], MusicEngine)


def test_required_models_are_the_acestep_bundle_all_present() -> None:
    models = _engine()[0].required_models()

    assert [m.id for m in models] == [
        "acestep-v15-turbo", "acestep-vae", "qwen3-embedding-0.6b", "acestep-5hz-lm-1.7b",
    ]  # fmt: skip
    assert all(m.present for m in models)


def test_load_reports_progress_and_keeps_the_profile() -> None:
    engine, recorder = _engine()

    engine.load(use(COVER_TURBO_LOWMEM), recorder.on_progress)

    assert engine.loaded_profile == "lowMemory"
    assert recorder.progress[0] == (0.0, "Loading model")
    assert recorder.progress[-1] == (1.0, "Loading model")


def test_unload_drops_the_profile() -> None:
    engine, recorder = _engine()
    engine.load(use(), recorder.on_progress)

    engine.unload()

    assert engine.loaded_profile is None


def test_restyle_without_load_is_an_engine_error(tmp_path: Path) -> None:
    engine, recorder = _engine()

    with pytest.raises(GeneratorError, match="not loaded"):
        engine.restyle(_request(tmp_path, [1]), recorder.on_progress)


def test_restyle_writes_one_48k_stereo_take_per_seed_of_the_source_length(tmp_path: Path) -> None:
    engine, recorder = _engine()
    engine.load(use(), recorder.on_progress)

    takes = engine.restyle(_request(tmp_path, [11, 22]), recorder.on_progress)

    assert [(t.index, t.seed, t.profile) for t in takes] == [(1, 11, None), (2, 22, None)]
    for take in takes:
        info = sf.info(take.path)
        assert (info.samplerate, info.channels, info.format) == (48_000, 2, "FLAC")
        assert info.frames == 480_000
        assert take.duration_seconds == pytest.approx(10.0)
        assert take.path.parent == tmp_path / "out"


def test_takes_are_deterministic_per_seed_and_differ_between_seeds(tmp_path: Path) -> None:
    first, recorder = _engine()
    first.load(use(), recorder.on_progress)
    a = first.restyle(_request(tmp_path, [5, 6], "a"), recorder.on_progress)
    b = first.restyle(_request(tmp_path, [5], "b"), recorder.on_progress)

    take_a, other = (sf.read(t.path)[0] for t in a)
    take_b = sf.read(b[0].path)[0]
    np.testing.assert_array_equal(take_a, take_b)
    assert not np.array_equal(take_a, other)
    assert 0.05 < float(np.max(np.abs(take_a))) < 1.0


def test_progress_is_monotonic_with_take_stage_texts(tmp_path: Path) -> None:
    engine, recorder = _engine()
    engine.load(use(), recorder.on_progress)
    recorder.progress.clear()

    engine.restyle(_request(tmp_path, [1, 2]), recorder.on_progress)

    fractions = [f for f, _ in recorder.progress]
    assert fractions == sorted(fractions)
    assert fractions[-1] == 1.0
    assert {s for _, s in recorder.progress} == {"Generating take 1 of 2", "Generating take 2 of 2"}


def test_each_take_sleeps_one_fake_step(tmp_path: Path) -> None:
    engine, recorder = _engine(step=0.5)
    engine.load(use(), recorder.on_progress)
    recorder.sleeps.clear()

    engine.restyle(_request(tmp_path, [1, 2]), recorder.on_progress)

    assert sum(recorder.sleeps) == pytest.approx(1.0)


def test_slow_mode_makes_steps_longer(tmp_path: Path) -> None:
    engine, recorder = _engine("slow", step=0.5)
    engine.load(use(), recorder.on_progress)
    recorder.sleeps.clear()

    engine.restyle(_request(tmp_path, [1]), recorder.on_progress)

    assert sum(recorder.sleeps) == pytest.approx(0.5 * SLOW_FACTOR)


def test_cancel_from_the_progress_callback_stops_the_take(tmp_path: Path) -> None:
    engine, recorder = _engine()
    engine.load(use(), recorder.on_progress)

    def cancel(fraction: float, stage: str) -> None:
        raise Cancelled

    request = _request(tmp_path, [1])
    with pytest.raises(Cancelled):
        engine.restyle(request, cancel)
    assert list(request.out_dir.iterdir()) == []


def test_no_gpu_mode_refuses_to_load() -> None:
    engine, recorder = _engine("no-gpu")

    with pytest.raises(GpuRequiredError):
        engine.load(use(), recorder.on_progress)
    assert engine.loaded_profile is None


def test_oom_mode_fails_the_standard_profile_and_the_low_memory_retry_succeeds(
    tmp_path: Path,
) -> None:
    engine, recorder = _engine("oom")
    engine.load(use(), recorder.on_progress)
    request = _request(tmp_path, [1])

    with pytest.raises(GpuOutOfMemoryError):
        engine.restyle(request, recorder.on_progress)
    engine.unload()
    engine.load(use(COVER_TURBO_LOWMEM), recorder.on_progress)

    assert len(engine.restyle(request, recorder.on_progress)) == 1


def test_oom_twice_mode_also_fails_the_low_memory_retry(tmp_path: Path) -> None:
    engine, recorder = _engine("oom-twice")
    engine.load(use(COVER_TURBO_LOWMEM), recorder.on_progress)

    with pytest.raises(GpuOutOfMemoryError):
        engine.restyle(_request(tmp_path, [1]), recorder.on_progress)


@pytest.mark.parametrize("mode", ["busy", "unvoiced-sample"])
def test_modes_without_engine_effect_generate_normally(
    tmp_path: Path, mode: FakeGeneratorMode
) -> None:
    engine, recorder = _engine(mode)
    engine.load(use(), recorder.on_progress)

    assert len(engine.restyle(_request(tmp_path, [1]), recorder.on_progress)) == 1


def test_fake_nvml_reports_too_little_memory_only_in_busy_mode() -> None:
    smallest_need = min(n.memory_mib for n in (*CONVERSION_NEEDS, COVER_TURBO_LOWMEM))

    assert fake_free_memory_mib("busy") == FAKE_BUSY_FREE_MEMORY_MIB < smallest_need
    assert fake_free_memory_mib("ready") == FAKE_FREE_MEMORY_MIB
    assert fake_free_memory_mib("oom") == FAKE_FREE_MEMORY_MIB > 13_926
    assert FAKE_FREE_MEMORY_MIB < FAKE_TOTAL_MEMORY_MIB
