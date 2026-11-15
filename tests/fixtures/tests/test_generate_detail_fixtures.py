"""Tests for the detail-separation fixture generator (feature 002, T009).

The measurements here are an independent re-implementation of the research.md D-04 definitions
(mono mix-down, frames of 2048 samples with a hop of 512, frame level = RMS in dBFS), so the
committed ``detail-manifest.json`` is checked against the audio, not against the generator's
own arithmetic.
"""

from __future__ import annotations

from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt
import pytest
import soundfile as sf  # type: ignore[import-untyped]

import generate_detail_fixtures as gen

GENERATOR = gen

Mono = npt.NDArray[np.float64]

FIXTURE_ROOT = Path(__file__).resolve().parents[1]
COMMITTED_MANIFEST = FIXTURE_ROOT / "detail-manifest.json"
SUMS = {
    "detail_drums_sum.wav": ("drums", ["kick", "snare", "hihat"]),
    "detail_vocals_sum.wav": ("vocals", ["lead", "backing"]),
    "detail_instruments_sum.wav": ("other", ["strings", "brass"]),
}
COMPONENTS = [
    f"detail_{group}_{name}.wav"
    for group, names in (
        ("drums", ["kick", "snare", "hihat"]),
        ("vocals", ["lead", "backing"]),
        ("instruments", ["strings", "brass"]),
    )
    for name in names
]
SILENT_PARENTS = ["detail_silent_parent.wav", "detail_bleed_parent.wav"]
CATALOGUE = {*SUMS, *COMPONENTS, *SILENT_PARENTS, "detail_10s_band.wav"}
FRAME, HOP, ACTIVE_DBFS = 2048, 512, -40.0
DB_TOLERANCE = 0.1


def _entry(manifest: dict[str, Any], name: str) -> dict[str, Any]:
    entry: dict[str, Any] = manifest["fixtures"][name]
    return entry


def _pcm(path: Path) -> npt.NDArray[np.int16]:
    data, _ = sf.read(str(path), dtype="int16", always_2d=True)
    return np.asarray(data, dtype=np.int16)


def _mono(path: Path) -> Mono:
    data, _ = sf.read(str(path), dtype="float64", always_2d=True)
    return np.asarray(data.mean(axis=1), dtype=np.float64)


def _frame_db(mono: Mono) -> Mono:
    frames = np.lib.stride_tricks.sliding_window_view(mono, FRAME)[::HOP]
    rms = np.sqrt(np.mean(frames**2, axis=1))
    return np.asarray(20 * np.log10(np.maximum(rms, 1e-10)), dtype=np.float64)


def _level_db(mono: Mono) -> float:
    return float(10 * np.log10(max(float(np.mean(mono**2)), 1e-20)))


def _share(mono: Mono, threshold_db: float) -> float:
    return float(np.mean(_frame_db(mono) > threshold_db))


def _rel_db(sub: Mono, parent: Mono) -> float:
    return float(10 * np.log10(np.sum(sub**2) / np.sum(parent**2)))


def test_the_manifest_lists_the_catalogue(generated: tuple[Path, dict[str, Any]]) -> None:
    assert set(generated[1]["fixtures"]) == CATALOGUE


@pytest.mark.parametrize("name", sorted(CATALOGUE))
def test_audio_is_44k1_stereo_pcm16_of_the_manifest_duration(
    generated: tuple[Path, dict[str, Any]], name: str
) -> None:
    out_dir, manifest = generated
    info = sf.info(str(out_dir / name))
    assert (info.samplerate, info.channels, info.subtype) == (44_100, 2, "PCM_16")
    assert info.frames == round(_entry(manifest, name)["durationSeconds"] * 44_100)


@pytest.mark.parametrize("name", sorted(CATALOGUE))
def test_level_and_active_share_are_reproduced(
    generated: tuple[Path, dict[str, Any]], name: str
) -> None:
    out_dir, manifest = generated
    entry, mono = _entry(manifest, name), _mono(out_dir / name)
    assert _level_db(mono) == pytest.approx(entry["levelDbfs"], abs=DB_TOLERANCE)
    assert _share(mono, ACTIVE_DBFS) == pytest.approx(entry["activeShare"], abs=0.005)


@pytest.mark.parametrize("name", sorted(SUMS))
def test_component_metrics_are_reproduced(
    generated: tuple[Path, dict[str, Any]], name: str
) -> None:
    out_dir, manifest = generated
    entry, parent = _entry(manifest, name), _mono(out_dir / name)
    floor = max(-50.0, entry["levelDbfs"] - 25.0)
    for component in entry["components"]:
        sub = _mono(out_dir / component["file"])
        assert _rel_db(sub, parent) == pytest.approx(component["relEnergyDb"], abs=DB_TOLERANCE)
        assert _share(sub, floor) == pytest.approx(component["activeShare"], abs=0.005)


@pytest.mark.parametrize("name", sorted(SUMS))
def test_sum_is_the_exact_sample_sum_of_its_components(
    generated: tuple[Path, dict[str, Any]], name: str
) -> None:
    out_dir, manifest = generated
    entry = _entry(manifest, name)
    parts = [_pcm(out_dir / c["file"]).astype(np.int32) for c in entry["components"]]
    assert np.array_equal(_pcm(out_dir / name).astype(np.int32), np.sum(parts, axis=0))
    assert entry["reconstructionErrorDb"] <= -30.0


@pytest.mark.parametrize(("name", "parent_and_stems"), sorted(SUMS.items()))
def test_sums_are_expected_to_split_into_their_components(
    generated: tuple[Path, dict[str, Any]], name: str, parent_and_stems: tuple[str, list[str]]
) -> None:
    entry = _entry(generated[1], name)
    parent, stems = parent_and_stems
    assert entry["kind"] == "parent"
    assert entry["parent"] == parent
    assert entry["expectedOutcome"] == "split"
    assert entry["expectedStems"] == [*stems, "rest"]
    assert [c["name"] for c in entry["components"]] == stems


@pytest.mark.parametrize("name", sorted(SUMS))
def test_split_parents_pass_the_gates_of_research_d04(
    generated: tuple[Path, dict[str, Any]], name: str
) -> None:
    out_dir, manifest = generated
    entry, parent = _entry(manifest, name), _mono(out_dir / name)
    assert _share(parent, ACTIVE_DBFS) >= 0.08
    floor = max(-50.0, _level_db(parent) - 25.0)
    for component in entry["components"]:
        sub = _mono(out_dir / component["file"])
        assert _rel_db(sub, parent) >= -20.0
        assert _share(sub, floor) >= 0.05


def test_silent_parent_is_noise_at_minus_80_dbfs(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    mono, entry = (
        _mono(out_dir / "detail_silent_parent.wav"),
        _entry(manifest, "detail_silent_parent.wav"),
    )
    assert _level_db(mono) == pytest.approx(-80.0, abs=0.5)
    assert _share(mono, ACTIVE_DBFS) == 0.0
    assert np.any(_pcm(out_dir / "detail_silent_parent.wav"))
    assert (entry["expectedOutcome"], entry["expectedStems"]) == ("skipped-silent", [])


def test_bleed_parent_has_about_4_percent_active_frames(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    out_dir, manifest = generated
    share = _share(_mono(out_dir / "detail_bleed_parent.wav"), ACTIVE_DBFS)
    entry = _entry(manifest, "detail_bleed_parent.wav")
    assert 0.035 <= share <= 0.045
    assert (entry["expectedOutcome"], entry["expectedStems"]) == ("skipped-silent", [])


def test_band_fixture_is_a_10_s_mix_with_real_content(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    out_dir, manifest = generated
    entry, mono = _entry(manifest, "detail_10s_band.wav"), _mono(out_dir / "detail_10s_band.wav")
    assert (entry["kind"], entry["durationSeconds"], entry["bpm"]) == ("band", 10.0, 120)
    assert entry["parents"] == ["vocals", "drums", "bass", "other"]
    assert _share(mono, ACTIVE_DBFS) >= 0.66
    assert np.max(np.abs(mono)) < 1.0


def test_backing_vocals_are_not_centred(generated: tuple[Path, dict[str, Any]]) -> None:
    pcm = _pcm(generated[0] / "detail_vocals_backing.wav").astype(np.float64)
    assert np.sum(pcm[:, 0] ** 2) > 2 * np.sum(pcm[:, 1] ** 2)


def test_manifest_records_the_d04_gating_constants(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    assert generated[1]["gating"] == {
        "frame": 2048,
        "hop": 512,
        "parentActiveDbfs": -40.0,
        "parentMinActiveShare": 0.08,
        "subMinRelDb": -20.0,
        "subActiveFloorDbfs": -50.0,
        "subActiveFloorRelDb": -25.0,
        "subMinActiveShare": 0.05,
        "minKept": 2,
        "maxReconstructionDb": -30.0,
    }


def test_quantising_a_clipping_signal_is_refused() -> None:
    with pytest.raises(gen.DetailFixtureError, match="clip"):
        gen.quantise(np.full((4, 2), 1.5))


def test_an_exact_sum_that_would_clip_is_refused() -> None:
    loud = np.full((4, 2), 30_000, dtype=np.int16)
    with pytest.raises(gen.DetailFixtureError, match="clip"):
        gen.exact_sum([loud, loud])


def test_outcome_of_a_parent_with_one_audible_component_is_nothing_further() -> None:
    parent = gen.quantise(gen.stereo(0.5 * np.sin(np.arange(44_100) * 0.05)))
    quiet = np.zeros_like(parent)
    assert gen.expected_outcome(parent, [("lead", parent), ("backing", quiet)]) == (
        "nothing-further",
        [],
    )


def test_reconstruction_error_of_a_half_scale_part_is_minus_6_db() -> None:
    parent = gen.quantise(gen.stereo(0.5 * np.sin(np.arange(44_100) * 0.05)))
    half = (parent // 2).astype(np.int16)
    assert gen.reconstruction_error_db(parent, [half]) == pytest.approx(-6.02, abs=0.05)
