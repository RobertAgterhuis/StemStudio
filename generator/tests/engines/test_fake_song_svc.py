"""Tests for the fake song engine, voice converter and pitch tracker (010 T010)."""

from pathlib import Path

import numpy as np
import pytest
import soundfile as sf

from stemstudio_generator.config import FakeGeneratorMode
from stemstudio_generator.engines.fake import (
    SVC_SAMPLE_RATE,
    FakeMusicEngine,
    FakePitchTracker,
    FakeVoiceConverter,
)
from stemstudio_generator.engines.profiles import song_vocal_label
from stemstudio_generator.ports import (
    SONG_DIRECT,
    SONG_DIRECT_LOWMEM,
    SONG_PLANNED,
    SVC_FP16,
    Cancelled,
    ConversionInput,
    EngineSong,
    GeneratorError,
    GpuOutOfMemoryError,
    GpuRequiredError,
    InvalidInputError,
    PitchTracker,
    PromptInvalidError,
    UnvoicedTargetError,
    VoiceConverter,
    voiced_ratio,
)
from stemstudio_generator.schemas import (
    PLANNING_STAGE,
    UNVOICED_TARGET_TEXT,
    VoiceConversionParams,
)

from ..signals import noise, stereo, tone, write
from .fakes import Recorder, fake_settings, out_dir, song_params, use

# ------------------------------------------------------------------ song


def _song_engine(mode: FakeGeneratorMode = "ready") -> tuple[FakeMusicEngine, Recorder]:
    recorder = Recorder()
    engine = FakeMusicEngine(fake_settings(mode, 0.1), recorder.sleep)
    return engine, recorder


def test_song_takes_have_the_requested_length_and_the_planned_profile(tmp_path: Path) -> None:
    engine, recorder = _song_engine()
    engine.load(use(SONG_PLANNED), recorder.on_progress)

    takes = engine.song(
        EngineSong(song_params([3, 4], 12.5), True, out_dir(tmp_path)), recorder.on_progress
    )

    assert [(t.index, t.seed, t.profile) for t in takes] == [(1, 3, "planned"), (2, 4, "planned")]
    for take in takes:
        info = sf.info(take.path)
        assert (info.samplerate, info.channels, info.frames) == (48_000, 2, 600_000)
        assert take.duration_seconds == pytest.approx(12.5)


def test_planned_song_reports_the_planning_stage_first(tmp_path: Path) -> None:
    engine, recorder = _song_engine()
    engine.load(use(SONG_PLANNED), recorder.on_progress)
    recorder.progress.clear()

    engine.song(EngineSong(song_params([1]), True, out_dir(tmp_path)), recorder.on_progress)

    assert recorder.progress[0][1] == PLANNING_STAGE
    assert recorder.progress[-1] == (1.0, "Generating take 1 of 1")


def test_direct_song_skips_planning_and_reports_the_direct_profile(tmp_path: Path) -> None:
    engine, recorder = _song_engine()
    engine.load(use(SONG_DIRECT), recorder.on_progress)
    recorder.progress.clear()

    params = song_params([1], thinking=False)
    takes = engine.song(EngineSong(params, False, out_dir(tmp_path)), recorder.on_progress)

    assert takes[0].profile == "direct"
    assert PLANNING_STAGE not in {stage for _, stage in recorder.progress}


def test_instrumental_song_has_no_fake_voice(tmp_path: Path) -> None:
    engine, recorder = _song_engine()
    engine.load(use(SONG_DIRECT), recorder.on_progress)

    sung = engine.song(EngineSong(song_params([9]), False, out_dir(tmp_path, "a")),
                       recorder.on_progress)  # fmt: skip
    instrumental = engine.song(
        EngineSong(song_params([9], instrumental=True), False, out_dir(tmp_path, "b")),
        recorder.on_progress,
    )

    assert not np.array_equal(sf.read(sung[0].path)[0], sf.read(instrumental[0].path)[0])


def test_song_vocal_label_follows_instrumental() -> None:
    assert song_vocal_label(song_params([1])) == "synthetic"
    assert song_vocal_label(song_params([1], instrumental=True)) == "none"


def test_song_oom_mode_succeeds_after_the_low_memory_reload(tmp_path: Path) -> None:
    engine, recorder = _song_engine("oom")
    engine.load(use(SONG_DIRECT), recorder.on_progress)
    request = EngineSong(song_params([1]), False, out_dir(tmp_path))

    with pytest.raises(GpuOutOfMemoryError):
        engine.song(request, recorder.on_progress)
    engine.load(use(SONG_DIRECT_LOWMEM), recorder.on_progress)

    assert engine.song(request, recorder.on_progress)[0].profile == "direct"


def test_song_without_load_is_an_engine_error(tmp_path: Path) -> None:
    engine, recorder = _song_engine()

    with pytest.raises(GeneratorError, match="not loaded"):
        engine.song(EngineSong(song_params([1]), True, out_dir(tmp_path)), recorder.on_progress)


# ------------------------------------------------------------------ voice conversion


def _converter(mode: FakeGeneratorMode = "ready") -> tuple[FakeVoiceConverter, Recorder]:
    recorder = Recorder()
    converter = FakeVoiceConverter(fake_settings(mode, 0.1), recorder.sleep)
    converter.load(use(SVC_FP16), recorder.on_progress)
    return converter, recorder


def _conversion(tmp_path: Path, target_seconds: float = 3.0, *, voiced: bool = True
                ) -> ConversionInput:  # fmt: skip
    signal = tone(target_seconds, 44_100, 220.0) if voiced else noise(target_seconds, 44_100)
    target = write(tmp_path / "vocals.flac", stereo(signal), 44_100)
    prompt = write(tmp_path / "sample.flac", tone(10.0, 48_000, 180.0), 48_000)
    return ConversionInput(target, prompt, VoiceConversionParams(profile_name="My voice"))


def test_fake_converter_is_a_voice_converter_with_the_singer_models() -> None:
    converter, _ = _converter()

    assert isinstance(converter, VoiceConverter)
    assert [m.id for m in converter.required_models()] == [
        "soulx-singer-svc", "rmvpe", "whisper-base",
    ]  # fmt: skip
    assert all(m.present for m in converter.required_models())


def test_conversion_is_24k_mono_of_the_target_length_and_deterministic(tmp_path: Path) -> None:
    converter, recorder = _converter()
    request = _conversion(tmp_path, 3.5)

    first = converter.convert(request, recorder.on_progress)
    second = converter.convert(request, recorder.on_progress)

    assert SVC_SAMPLE_RATE == 24_000
    assert first.shape == (84_000,)
    assert first.dtype == np.float32
    np.testing.assert_array_equal(first, second)
    assert 0.1 < float(np.max(np.abs(first))) <= 1.0
    assert recorder.progress[-1] == (1.0, "Converting voice")


def test_unvoiced_target_fails_with_the_contract_text(tmp_path: Path) -> None:
    converter, recorder = _converter()

    with pytest.raises(UnvoicedTargetError, match="almost no singing") as info:
        converter.convert(_conversion(tmp_path, voiced=False), recorder.on_progress)
    assert str(info.value) == UNVOICED_TARGET_TEXT


def test_unreadable_prompt_is_prompt_invalid(tmp_path: Path) -> None:
    converter, recorder = _converter()
    request = _conversion(tmp_path)
    request.prompt.write_bytes(b"junk")

    with pytest.raises(PromptInvalidError):
        converter.convert(request, recorder.on_progress)


def test_unreadable_target_is_invalid_input(tmp_path: Path) -> None:
    converter, recorder = _converter()
    request = _conversion(tmp_path)
    request.target.write_bytes(b"junk")

    with pytest.raises(InvalidInputError):
        converter.convert(request, recorder.on_progress)


def test_converter_oom_mode_fails_the_first_attempt_only(tmp_path: Path) -> None:
    converter, recorder = _converter("oom")
    request = _conversion(tmp_path)

    with pytest.raises(GpuOutOfMemoryError):
        converter.convert(request, recorder.on_progress)

    assert converter.convert(request, recorder.on_progress).size == 72_000


def test_converter_oom_twice_mode_always_fails(tmp_path: Path) -> None:
    converter, recorder = _converter("oom-twice")
    request = _conversion(tmp_path)

    for _ in range(2):
        with pytest.raises(GpuOutOfMemoryError):
            converter.convert(request, recorder.on_progress)


def test_converter_no_gpu_mode_refuses_to_load() -> None:
    recorder = Recorder()

    with pytest.raises(GpuRequiredError):
        FakeVoiceConverter(fake_settings("no-gpu")).load(use(SVC_FP16), recorder.on_progress)


def test_converter_needs_a_load_and_stops_on_cancel(tmp_path: Path) -> None:
    converter, recorder = _converter()
    request = _conversion(tmp_path)

    def cancel(fraction: float, stage: str) -> None:
        raise Cancelled

    with pytest.raises(Cancelled):
        converter.convert(request, cancel)
    converter.unload()
    with pytest.raises(GeneratorError, match="not loaded"):
        converter.convert(request, recorder.on_progress)


# ------------------------------------------------------------------ pitch tracker


def test_fake_pitch_tracker_hears_a_tone_as_voiced_at_its_pitch(tmp_path: Path) -> None:
    tracker = FakePitchTracker()
    path = write(tmp_path / "tone.flac", stereo(tone(2.0, 48_000, 440.0)), 48_000)

    f0 = tracker.f0(path)

    assert isinstance(tracker, PitchTracker)
    assert f0.dtype == np.float32
    assert f0.shape == (200,)
    assert voiced_ratio(f0) > 0.9
    assert float(np.median(f0[f0 > 0])) == pytest.approx(440.0, rel=0.02)


@pytest.mark.parametrize("rate", [16_000, 24_000, 44_100])
def test_fake_pitch_tracker_works_at_other_rates(tmp_path: Path, rate: int) -> None:
    f0 = FakePitchTracker().f0(write(tmp_path / "t.wav", tone(1.0, rate, 200.0), rate))

    assert f0.shape == (100,)
    assert float(np.median(f0[f0 > 0])) == pytest.approx(200.0, rel=0.03)


def test_fake_pitch_tracker_hears_noise_and_silence_as_unvoiced(tmp_path: Path) -> None:
    tracker = FakePitchTracker()
    noisy = write(tmp_path / "noise.flac", noise(1.0), 48_000)
    silent = write(tmp_path / "silence.flac", np.zeros(48_000, dtype=np.float32), 48_000)

    assert voiced_ratio(tracker.f0(noisy)) == 0.0
    assert voiced_ratio(tracker.f0(silent)) == 0.0


def test_unvoiced_sample_mode_hears_no_voice_at_all(tmp_path: Path) -> None:
    path = write(tmp_path / "tone.flac", tone(1.0), 48_000)

    f0 = FakePitchTracker("unvoiced-sample").f0(path)

    assert f0.shape == (100,)
    assert voiced_ratio(f0) == 0.0


def test_fake_pitch_tracker_refuses_unreadable_audio(tmp_path: Path) -> None:
    bad = tmp_path / "bad.webm"
    bad.write_bytes(b"junk")

    with pytest.raises(InvalidInputError):
        FakePitchTracker().f0(bad)
