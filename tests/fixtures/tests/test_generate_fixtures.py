"""Tests for the deterministic E2E audio fixture generator (T020)."""

from __future__ import annotations

import shutil
from pathlib import Path
from typing import Any

import numpy as np
import pytest
import soundfile as sf  # type: ignore[import-untyped]

import generate_fixtures as gen

GENERATOR = gen

COMMITTED_MANIFEST = Path(__file__).resolve().parents[1] / "manifest.json"
CATALOGUE = {
    "click_120bpm_4-4_12s.wav",
    "click_90bpm_3-4_12s.wav",
    "music_100bpm_20s.mp3",
    "music_100bpm_20s.flac",
    "upload_4mb.wav",
    "silence_5s.wav",
    "tone_440hz_20s.flac",
    "corrupt.mp3",
    "unsupported.ogg",
}
PCM_FILES = ["click_120bpm_4-4_12s.wav", "click_90bpm_3-4_12s.wav", "music_100bpm_20s.flac"]
PCM_FILES += ["upload_4mb.wav", "silence_5s.wav", "tone_440hz_20s.flac"]
CLICK_FILES = ["click_120bpm_4-4_12s.wav", "click_90bpm_3-4_12s.wav"]


def _entry(manifest: dict[str, Any], name: str) -> dict[str, Any]:
    entry: dict[str, Any] = manifest["fixtures"][name]
    return entry


def test_the_manifest_lists_the_catalogue(generated: tuple[Path, dict[str, Any]]) -> None:
    assert set(generated[1]["fixtures"]) == CATALOGUE


@pytest.mark.parametrize("name", [*PCM_FILES, "music_100bpm_20s.mp3"])
def test_audio_is_44k1_stereo(generated: tuple[Path, dict[str, Any]], name: str) -> None:
    info = sf.info(str(generated[0] / name))
    assert info.samplerate == 44_100
    assert info.channels == 2


@pytest.mark.parametrize("name", PCM_FILES)
def test_lossless_durations_are_exact(generated: tuple[Path, dict[str, Any]], name: str) -> None:
    out_dir, manifest = generated
    expected = _entry(manifest, name)["durationSeconds"]
    assert sf.info(str(out_dir / name)).frames == round(expected * 44_100)


def test_mp3_duration_within_encoder_padding(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, _ = generated
    duration = sf.info(str(out_dir / "music_100bpm_20s.mp3")).duration
    assert duration == pytest.approx(20.0, abs=0.1)


def test_catalogue_facts(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    assert _entry(manifest, "click_120bpm_4-4_12s.wav")["bpm"] == 120
    assert _entry(manifest, "click_120bpm_4-4_12s.wav")["beatsPerBar"] == 4
    assert _entry(manifest, "click_90bpm_3-4_12s.wav")["bpm"] == 90
    assert _entry(manifest, "click_90bpm_3-4_12s.wav")["beatsPerBar"] == 3
    assert _entry(manifest, "music_100bpm_20s.mp3")["bpm"] == 100
    assert _entry(manifest, "silence_5s.wav")["bpm"] is None
    assert _entry(manifest, "corrupt.mp3")["durationSeconds"] is None


@pytest.mark.parametrize("name", CLICK_FILES)
def test_click_onsets_match_beat_times_within_one_sample(
    generated: tuple[Path, dict[str, Any]], name: str
) -> None:
    out_dir, manifest = generated
    audio, rate = sf.read(str(out_dir / name), dtype="float64")
    mono = np.abs(audio).max(axis=1)
    loud = np.flatnonzero(mono > 0.01)
    onsets = loud[np.concatenate(([True], np.diff(loud) > rate // 20))]
    expected = np.round(np.asarray(_entry(manifest, name)["beatTimes"]) * rate)
    assert len(onsets) == len(expected)
    assert np.max(np.abs(onsets - expected)) <= 1


@pytest.mark.parametrize("name", CLICK_FILES)
def test_downbeats_are_accented(generated: tuple[Path, dict[str, Any]], name: str) -> None:
    out_dir, manifest = generated
    audio, rate = sf.read(str(out_dir / name), dtype="float64")
    entry = _entry(manifest, name)
    peaks = {
        t: np.abs(audio[round(t * rate) : round(t * rate) + 200]).max() for t in entry["beatTimes"]
    }
    downbeats = set(entry["downbeatTimes"])
    assert len(downbeats) == -(-len(entry["beatTimes"]) // entry["beatsPerBar"])
    assert min(peaks[t] for t in downbeats) > max(p for t, p in peaks.items() if t not in downbeats)


def test_tone_is_a_440_hz_sine_at_minus_12_dbfs(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    entry = _entry(manifest, "tone_440hz_20s.flac")
    audio, rate = sf.read(str(out_dir / "tone_440hz_20s.flac"), dtype="float64")
    spectrum = np.abs(np.fft.rfft(audio[rate : 3 * rate, 0]))
    assert np.argmax(spectrum) * rate / (2 * rate) == pytest.approx(entry["frequencyHz"], abs=0.5)
    assert 20 * np.log10(np.abs(audio).max()) == pytest.approx(entry["peakDbfs"], abs=0.05)
    assert np.array_equal(audio[:, 0], audio[:, 1])
    assert entry["bpm"] is None


def test_only_the_tone_has_tone_facts(generated: tuple[Path, dict[str, Any]]) -> None:
    with_tone = {n for n, e in generated[1]["fixtures"].items() if "frequencyHz" in e}
    assert with_tone == {"tone_440hz_20s.flac"}


def test_silence_is_digital_silence(generated: tuple[Path, dict[str, Any]]) -> None:
    audio, _ = sf.read(str(generated[0] / "silence_5s.wav"), dtype="int16")
    assert not audio.any()


def test_upload_fixture_is_a_few_megabytes(generated: tuple[Path, dict[str, Any]]) -> None:
    size = (generated[0] / "upload_4mb.wav").stat().st_size
    assert 3_500_000 < size < 6_000_000


def test_corrupt_mp3_cannot_be_decoded(generated: tuple[Path, dict[str, Any]]) -> None:
    path = generated[0] / "corrupt.mp3"
    assert path.read_bytes().startswith(b"ID3")
    with pytest.raises(sf.LibsndfileError):
        sf.read(str(path))


def test_unsupported_ogg_has_an_ogg_signature(generated: tuple[Path, dict[str, Any]]) -> None:
    assert (generated[0] / "unsupported.ogg").read_bytes().startswith(b"OggS")


def test_missing_ffmpeg_is_reported(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    monkeypatch.setattr(shutil, "which", lambda _name: None)
    with pytest.raises(gen.FfmpegNotFoundError, match="ffmpeg"):
        gen.generate(tmp_path, tmp_path / "manifest.json")


def test_failing_ffmpeg_is_reported(monkeypatch: pytest.MonkeyPatch, tmp_path: Path) -> None:
    monkeypatch.setattr(gen, "FFMPEG_CODEC_ARGS", ("-c:a", "no-such-codec"))
    with pytest.raises(gen.FixtureGenerationError, match="ffmpeg failed"):
        gen.generate(tmp_path, tmp_path / "manifest.json")


def test_beat_times_cover_the_clip_from_the_first_beat() -> None:
    times = gen.beat_times(gen.Rhythm(bpm=120, beats_per_bar=4, duration_s=2.0))
    assert times == [0.5, 1.0, 1.5]


def test_rhythmic_fixture_without_rhythm_is_rejected(tmp_path: Path) -> None:
    with pytest.raises(gen.FixtureGenerationError, match="needs a rhythm"):
        gen.write_fixture(gen.Fixture("broken.wav", "click", 1.0), tmp_path)
