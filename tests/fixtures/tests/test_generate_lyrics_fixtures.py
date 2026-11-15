"""Tests of the sung alignment clip (006:T012) and its formant synthesiser."""

from __future__ import annotations

import itertools
import json
import runpy
import sys
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import pytest
import soundfile as sf  # type: ignore[import-untyped]

import generate_lyrics_fixtures as gen
import sung_voice as voice
from generate_fixtures import FIXTURE_DIR

GENERATOR = gen

COMMITTED = Path(__file__).resolve().parents[1] / "lyrics" / "align-clip.json"


def _mono(out_dir: Path) -> npt.NDArray[np.float64]:
    data, _ = sf.read(str(out_dir / gen.CLIP_NAME), dtype="float64", always_2d=True)
    return np.asarray(data.mean(axis=1), dtype=np.float64)


def _rms(signal: npt.NDArray[np.float64], start_ms: int, end_ms: int) -> float:
    part = signal[start_ms * 44_100 // 1000 : end_ms * 44_100 // 1000]
    return float(np.sqrt(np.mean(part**2)))


def _words(manifest: dict[str, Any]) -> list[dict[str, Any]]:
    return [word for line in manifest["lines"] for word in line["words"]]


def test_committed_annotation_matches_the_generator(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    assert json.loads(COMMITTED.read_text(encoding="utf-8")) == manifest


def test_clip_is_a_20_to_30_s_stereo_pcm16_file(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    info = sf.info(str(out_dir / gen.CLIP_NAME))
    assert (info.samplerate, info.channels, info.subtype) == (44_100, 2, "PCM_16")
    assert info.duration == pytest.approx(manifest["durationSeconds"], abs=0.001)
    assert 20.0 <= info.duration <= 30.0


def test_plain_text_is_the_annotated_words(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    assert manifest["plainText"].split() == [w["text"] for w in _words(manifest)]
    assert [line["text"] for line in manifest["lines"]] == manifest["plainText"].split("\n")


def test_word_times_are_ordered_and_inside_the_clip(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    words = _words(manifest)
    starts = [w["startMs"] for w in words]
    assert starts == sorted(starts)
    assert all(w["startMs"] < w["endMs"] <= manifest["durationSeconds"] * 1000 for w in words)
    assert all(a["endMs"] <= b["startMs"] for a, b in itertools.pairwise(words))


def test_every_word_is_sung_and_the_gaps_are_silent(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    signal = _mono(out_dir)
    lines = manifest["lines"]
    assert all(_rms(signal, w["startMs"], w["endMs"]) > 0.01 for w in _words(manifest))
    assert _rms(signal, 0, 900) < 1e-3
    assert _rms(signal, lines[1]["endMs"] + 100, lines[2]["startMs"] - 100) < 1e-3


def test_audio_has_headroom_and_is_reproducible(
    generated: tuple[Path, dict[str, Any]], tmp_path: Path
) -> None:
    out_dir, _ = generated
    gen.generate(tmp_path, tmp_path / "a.json")
    first = (out_dir / gen.CLIP_NAME).read_bytes()
    assert (tmp_path / gen.CLIP_NAME).read_bytes() == first
    assert float(np.max(np.abs(_mono(out_dir)))) == pytest.approx(gen.PEAK, abs=0.001)


def test_sc003_target_is_recorded(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    assert manifest["sc003"] == {"toleranceMs": 300, "minWithinShare": 0.8}
    assert (manifest["licence"], manifest["language"]) == ("CC0-1.0", "en")


def test_syllable_too_short_for_its_consonants_is_refused() -> None:
    with pytest.raises(gen.SongError, match="shorter than its consonants"):
        gen.syllable_segments(gen.Syllable("S T R IY", 0.1, 60), 0.0, 0.1)


def test_unknown_phone_is_refused() -> None:
    with pytest.raises(voice.UnknownPhoneError, match="XX"):
        voice.expand("AA XX")


def test_stops_expand_to_closure_and_burst() -> None:
    assert voice.expand("B OW T") == ["CLV", "BB", "OW", "CL", "TB"]


def test_diphthong_glides_between_its_targets() -> None:
    ay = voice.PHONES["AY"]
    assert list(ay.formants_at(0.0)) == list(voice.PHONES["AA"].formants)
    assert list(ay.formants_at(1.0)) == list(voice.PHONES["IY"].formants)


def test_resonator_peaks_at_its_frequency() -> None:
    frames = voice.BLOCK * 200
    impulse = np.zeros(frames)
    impulse[0] = 1.0
    response = voice.resonate(impulse, np.full(200, 1000.0), 80.0)
    spectrum = np.abs(np.fft.rfft(response))
    peak_hz = np.argmax(spectrum) * 44_100 / frames
    assert peak_hz == pytest.approx(1000.0, abs=20.0)


def test_glottal_source_follows_the_sung_pitch() -> None:
    frames = voice.BLOCK * 400
    source = voice.glottal_source(np.full(400, 69.0), frames)
    spectrum = np.abs(np.fft.rfft(source * np.hanning(frames)))
    fundamental = np.argmax(spectrum[: frames * 1000 // 44_100]) * 44_100 / frames
    assert fundamental == pytest.approx(440.0, rel=0.02)


def test_main_writes_into_the_given_folders(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    annotation = tmp_path / "lyrics" / "clip.json"
    assert gen.main(["--out-dir", str(tmp_path / "audio"), "--annotation", str(annotation)]) == 0
    assert (tmp_path / "audio" / gen.CLIP_NAME).is_file()
    assert "22 words" in capsys.readouterr().out
    assert json.loads(annotation.read_text(encoding="utf-8"))["file"] == gen.CLIP_NAME


def test_default_paths_point_into_the_fixture_folder() -> None:
    args = gen.parse_args([])
    assert args.out_dir == FIXTURE_DIR / "audio"
    assert args.annotation == FIXTURE_DIR / "lyrics" / "align-clip.json"


def test_running_the_script_exits_with_zero(
    monkeypatch: pytest.MonkeyPatch, tmp_path: Path
) -> None:
    script = Path(gen.__file__ or "")
    argv = [script.name, "--out-dir", str(tmp_path), "--annotation", str(tmp_path / "a.json")]
    monkeypatch.setattr(sys, "argv", argv)
    with pytest.raises(SystemExit) as exit_info:
        runpy.run_path(str(script), run_name="__main__")
    assert exit_info.value.code == 0
