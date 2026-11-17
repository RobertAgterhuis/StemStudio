"""Tests of the play-along recording fixtures (015:T009): format, families, reproducibility."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
from typing import Any

import numpy as np
import pytest
import soundfile as sf  # type: ignore[import-untyped]

from recording import generate_recording_fixtures as gen

GENERATOR = gen
FAMILIES = {"vocals", "guitar", "bass", "piano", "drums", "other"}
STEREO = {"input-piano.wav", "input-drums.wav", "click-track.wav"}
COMMITTED = Path(gen.__file__).resolve().parent / "manifest.json"


def test_every_family_has_exactly_one_input(generated: tuple[Path, dict[str, Any]]) -> None:
    _, manifest = generated
    inputs = [f["family"] for f in manifest["fixtures"].values() if f["role"] == "input"]
    assert sorted(inputs) == sorted(FAMILIES)


@pytest.mark.parametrize("fixture", gen.CATALOGUE, ids=lambda f: f.name)
def test_file_is_44k1_pcm16_of_20_seconds(
    generated: tuple[Path, dict[str, Any]], fixture: gen.RecordingFixture
) -> None:
    out_dir, manifest = generated
    info = sf.info(str(out_dir / fixture.name))
    assert (info.samplerate, info.subtype, info.frames) == (44_100, "PCM_16", 882_000)
    assert info.channels == (2 if fixture.name in STEREO else 1)
    assert manifest["fixtures"][fixture.name]["channels"] == info.channels


@pytest.mark.parametrize("fixture", [f for f in gen.CATALOGUE if f.role != "silence"], ids=str)
def test_sounding_files_peak_at_minus_3_dbfs(
    generated: tuple[Path, dict[str, Any]], fixture: gen.RecordingFixture
) -> None:
    _, manifest = generated
    assert manifest["fixtures"][fixture.name]["peakDbfs"] == pytest.approx(-3.1, abs=0.05)


def test_silent_file_is_all_zero(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    data, _ = sf.read(str(out_dir / "silent.wav"), dtype="int16")
    assert not np.any(data)
    assert manifest["fixtures"]["silent.wav"]["peakDbfs"] is None


def test_click_track_onsets_are_sample_exact(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    data, _ = sf.read(str(out_dir / "click-track.wav"), dtype="int16")
    times = manifest["fixtures"]["click-track.wav"]["clickTimesSeconds"]
    assert times == gen.click_times()
    assert times[:3] == [0.5, 1.0, 1.5]
    assert len(times) == 39
    onsets = [round(t * 44_100) for t in times]
    assert all(data[o, 0] == np.max(data[:, 0]) for o in onsets)
    assert not np.any(data[: onsets[0], 0])


def test_stereo_inputs_differ_between_channels(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, _ = generated
    for name in ("input-piano.wav", "input-drums.wav"):
        data, _ = sf.read(str(out_dir / name), dtype="int16")
        assert np.any(data[:, 0] != data[:, 1]), name


def test_inputs_sound_through_the_whole_take_window(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, _ = generated
    for fixture in (f for f in gen.CATALOGUE if f.role == "input"):
        data, _ = sf.read(str(out_dir / fixture.name), dtype="float64")
        mono = data if data.ndim == 1 else data.mean(axis=1)
        seconds = [mono[s * 44_100 : (s + 1) * 44_100] for s in range(1, 18)]
        assert all(np.max(np.abs(block)) > 0.05 for block in seconds), fixture.name


def test_hash_list_matches_the_files(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    for name, entry in manifest["fixtures"].items():
        assert entry["sha256"] == hashlib.sha256((out_dir / name).read_bytes()).hexdigest()


def test_rerun_is_byte_identical_and_matches_the_committed_manifest(
    generated: tuple[Path, dict[str, Any]], tmp_path: Path
) -> None:
    _, manifest = generated
    again = gen.generate(tmp_path, tmp_path / "manifest.json")
    assert again == manifest
    assert json.loads(COMMITTED.read_text(encoding="utf-8")) == manifest


def test_pluck_has_the_requested_length() -> None:
    audio = gen.pluck(110.0, 0.25, np.random.default_rng(1))
    assert len(audio) == round(0.25 * 44_100)
    assert np.max(np.abs(audio[-500:])) < np.max(np.abs(audio[:500]))


def test_main_writes_into_the_given_folder(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    code = gen.main(["--out-dir", str(tmp_path), "--manifest", str(tmp_path / "m.json")])
    assert code == 0
    assert (tmp_path / "input-vocals.wav").exists()
    assert "wrote 8 fixtures" in capsys.readouterr().out


def test_default_arguments_point_at_the_recording_folder() -> None:
    args = gen.parse_args([])
    assert args.out_dir == gen.OUT_DIR
    assert args.manifest == gen.OUT_DIR / "manifest.json"
