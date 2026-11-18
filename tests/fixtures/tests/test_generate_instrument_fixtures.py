"""Tests of the fake-microphone singing fixtures (016:T014): format, pitch, reproducibility."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any

import librosa
import numpy as np
import numpy.typing as npt
import pytest
import soundfile as sf  # type: ignore[import-untyped]

import generate_instrument_fixtures as gen

GENERATOR = gen
COMMITTED = Path(gen.__file__).resolve().parent / "instrument-manifest.json"
NAMES = ("sing-on-pitch.wav", "sing-semitone-off.wav", "silence.wav")


def _read(out_dir: Path, name: str) -> npt.NDArray[np.float64]:
    data, rate = sf.read(str(out_dir / name), dtype="float64")
    assert rate == 44_100
    return np.asarray(data, dtype=np.float64)


def _median_cents(audio: npt.NDArray[np.float64], start: float, end: float) -> float:
    """Median YIN pitch (MIDI cents) of the middle half of one note."""
    quarter = (end - start) / 4
    piece = audio[round((start + quarter) * 44_100) : round((end - quarter) * 44_100)]
    f0 = librosa.yin(piece, fmin=60.0, fmax=1000.0, sr=44_100, frame_length=2048)
    return float(np.median(librosa.hz_to_midi(f0)) * 100)


def test_catalogue_has_the_three_files(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    assert tuple(manifest["fixtures"]) == NAMES


@pytest.mark.parametrize("name", NAMES)
def test_file_is_44k1_mono_pcm16(generated: tuple[Path, dict[str, Any]], name: str) -> None:
    out_dir, manifest = generated
    info = sf.info(str(out_dir / name))
    assert (info.samplerate, info.subtype, info.channels) == (44_100, "PCM_16", 1)
    assert info.frames == round(gen.SECONDS * 44_100)
    assert manifest["fixtures"][name]["durationSeconds"] == gen.SECONDS


def test_melody_walks_the_c_major_scale_up_and_back() -> None:
    walk = gen.reference_walk()
    assert walk == (60, 62, 64, 65, 67, 69, 71, 72, 71, 69, 67, 65, 64, 62)


def test_reference_notes_follow_the_beat_grid() -> None:
    notes = gen.reference_notes()
    assert notes[0] == {"startSeconds": 0.5, "endSeconds": 1.0, "midi": 60}
    assert notes[8]["midi"] == 71
    assert notes[14]["midi"] == 60
    assert all(n["endSeconds"] - n["startSeconds"] == pytest.approx(0.5) for n in notes)
    assert notes[-1]["endSeconds"] <= gen.SECONDS


@pytest.mark.parametrize(
    ("name", "offset_cents"), [("sing-on-pitch.wav", -1200), ("sing-semitone-off.wav", -1100)]
)
def test_sung_notes_sit_at_the_reference_plus_offset(
    generated: tuple[Path, dict[str, Any]], name: str, offset_cents: int
) -> None:
    out_dir, manifest = generated
    audio = _read(out_dir, name)
    entry = manifest["fixtures"][name]
    assert entry["offsetCentsFromReference"] == offset_cents
    for note in gen.reference_notes()[:16]:
        sung = _median_cents(audio, note["startSeconds"], note["endSeconds"])
        assert sung == pytest.approx(note["midi"] * 100 + offset_cents, abs=10), note


def test_sung_notes_have_two_harmonics(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, _ = generated
    audio = _read(out_dir, "sing-on-pitch.wav")
    piece = audio[round(0.6 * 44_100) : round(0.9 * 44_100)]
    spectrum = np.abs(np.fft.rfft(piece * np.hanning(len(piece))))
    freqs = np.fft.rfftfreq(len(piece), 1 / 44_100)
    fundamental = librosa.midi_to_hz(48)

    def level(hz: float) -> float:
        return float(spectrum[np.argmin(np.abs(freqs - hz))])

    assert level(2 * fundamental) > 0.2 * level(fundamental)
    assert level(3 * fundamental) > 0.1 * level(fundamental)
    assert level(4 * fundamental) < 0.01 * level(fundamental)


def test_audio_is_unvoiced_before_the_first_beat(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, _ = generated
    audio = _read(out_dir, "sing-on-pitch.wav")
    assert not np.any(audio[: round(0.5 * 44_100)])


def test_sung_files_peak_at_minus_3_dbfs(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    for name in NAMES[:2]:
        assert manifest["fixtures"][name]["peakDbfs"] == pytest.approx(-3.1, abs=0.05)


def test_silence_is_all_zero(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    assert not np.any(_read(out_dir, "silence.wav"))
    assert manifest["fixtures"]["silence.wav"]["peakDbfs"] is None
    assert manifest["fixtures"]["silence.wav"]["offsetCentsFromReference"] is None


def test_manifest_lists_the_reference_melody(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    melody = manifest["referenceMelody"]
    assert melody["bpm"] == 120
    assert melody["firstBeatSeconds"] == 0.5
    assert melody["notes"] == gen.reference_notes()


def test_hash_list_matches_the_files(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    for name, entry in manifest["fixtures"].items():
        assert entry["sha256"] == hashlib.sha256((out_dir / name).read_bytes()).hexdigest()


def test_rerun_is_byte_identical_and_matches_the_committed_manifest(
    generated: tuple[Path, dict[str, Any]], tmp_path: Path
) -> None:
    _, manifest = generated
    again = gen.generate(tmp_path, tmp_path / "m.json")
    assert again == manifest
    assert json.loads(COMMITTED.read_text(encoding="utf-8")) == manifest


def test_main_writes_into_the_given_folder(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    code = gen.main(["--out-dir", str(tmp_path), "--manifest", str(tmp_path / "m.json")])
    assert code == 0
    assert (tmp_path / "sing-on-pitch.wav").exists()
    assert "3 instrument fixtures" in capsys.readouterr().out
