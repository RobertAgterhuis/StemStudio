"""Tests of the detail step plan and the per-parent steps with fake models (T040, FR-001..006)."""

import json
from pathlib import Path

import numpy as np
import pytest

from stemstudio_worker.detail.audio_io import read_stem
from stemstudio_worker.detail.folding import DRUMSEP_TABLE, MEGA_TABLE, VOCAL_TABLE
from stemstudio_worker.detail.gating import relative_db
from stemstudio_worker.detail.steps import (
    FAMILIES,
    PARTITION,
    STEP_WEIGHTS,
    DetailPipeline,
    RegistrySource,
)
from stemstudio_worker.ports import DetailCancelledError, DetailModel
from stemstudio_worker.schemas import DetailModelId, ParentOutcome, ParentOutcomeKind, StemName

from .models import ScaledModel, Source, TransformModel, band, choice, identity_table, write_stems
from .signals import Audio

K = ParentOutcomeKind
DRUMS = ScaledModel({"kick": 0.5, "snare": 0.3, "toms": 0.15, "hh": 0.05})
VOCALS = ScaledModel({"lead": 0.7, "backing": 0.3})
FAMILY = ScaledModel({"electric-guitar": 0.6, "keys": 0.4})


def _source(**overrides: object) -> Source:
    choices = {
        "drums": choice(DRUMS, DRUMSEP_TABLE, "mdx23c-drumsep-6s"),
        "vocals": choice(VOCALS, VOCAL_TABLE, "uvr-mdxnet-kara-2"),
        **{p: choice(FAMILY, MEGA_TABLE, "mvsep-mega-53", families=True)
           for p in ("guitar", "piano", "other")},
    }  # fmt: skip
    choices.update(overrides)  # type: ignore[arg-type]
    return Source(choices)  # type: ignore[arg-type]


def _outcome(partial: Path, parent: str) -> ParentOutcome:
    data = json.loads((partial / parent / "outcome.json").read_text(encoding="utf-8"))
    return ParentOutcome.model_validate(data["outcome"])


def _run_all(pipeline: DetailPipeline, stems: dict[StemName, Path], partial: Path) -> list[float]:
    fractions: list[float] = []
    for step in pipeline.plan(stems, partial):
        step.run(fractions.append)
    return fractions


@pytest.fixture
def stems(tmp_path: Path) -> dict[StemName, Path]:
    return write_stems(tmp_path / "stems", band())


def test_plan_checkpoints_bass_and_silent_parents_and_groups_instruments(
    stems: dict[StemName, Path], tmp_path: Path
) -> None:
    partial = tmp_path / "partial"
    steps = DetailPipeline(_source()).plan(stems, partial)
    assert [s.parents for s in steps] == [("drums",), ("vocals",), ("guitar", "other")]
    assert [s.weight for s in steps] == [0.20, 0.05, 0.74]
    assert STEP_WEIGHTS == {"drums": 0.20, "vocals": 0.05, "instruments": 0.74}
    bass = _outcome(partial, "bass")
    assert (bass.outcome, bass.model, bass.stems) == (K.NOT_SUPPORTED, None, [])
    assert bass.metrics.active_share == 1.0
    piano = _outcome(partial, "piano")
    assert (piano.outcome, piano.metrics.parent_level_dbfs) == (K.SKIPPED_SILENT, -160.0)


def test_four_stem_fallback_has_no_guitar_or_piano(tmp_path: Path) -> None:
    four: dict[StemName, Audio] = {
        k: v for k, v in band(silent=()).items() if k in ("vocals", "drums", "bass", "other")
    }
    stems = write_stems(tmp_path / "stems", four)
    steps = DetailPipeline(_source()).plan(stems, tmp_path / "partial")
    assert [s.parents for s in steps] == [("drums",), ("vocals",), ("other",)]


def test_per_parent_sources_get_one_instrument_step_per_parent(tmp_path: Path) -> None:
    stems = write_stems(tmp_path / "stems", band(silent=()))
    source = _source()
    source.sum_instruments = False
    steps = DetailPipeline(source).plan(stems, tmp_path / "partial")
    assert [s.parents for s in steps][2:] == [("guitar",), ("piano",), ("other",)]
    assert [s.weight for s in steps][2:] == pytest.approx([0.74 / 3] * 3)


def test_drums_step_writes_kept_stems_rest_and_checkpoint(
    stems: dict[StemName, Path], tmp_path: Path
) -> None:
    partial = tmp_path / "partial"
    pipeline = DetailPipeline(_source())
    fractions: list[float] = []
    pipeline.plan(stems, partial)[0].run(fractions.append)
    drums = _outcome(partial, "drums")
    assert (drums.outcome, drums.model) == (K.SPLIT, "mdx23c-drumsep-6s")
    assert [s.name for s in drums.stems] == ["kick", "snare", "toms", "rest"]
    assert drums.stems[0].path == "drums/kick"
    assert drums.stems[0].size_bytes == (partial / "drums" / "kick.flac").stat().st_size
    assert fractions[-1] == 1.0
    assert fractions == sorted(fractions)
    assert not list(partial.glob("*.tmp"))


def test_drums_split_reconstructs_the_parent(stems: dict[StemName, Path], tmp_path: Path) -> None:
    partial = tmp_path / "partial"
    DetailPipeline(_source()).plan(stems, partial)[0].run(lambda f: None)
    drums = _outcome(partial, "drums")
    assert drums.metrics.reconstruction_error_db is not None
    assert drums.metrics.reconstruction_error_db <= -30
    assert drums.metrics.model_residual_db is not None
    assert drums.metrics.model_residual_db <= -100
    total = sum(read_stem(partial / "drums" / f"{s.name}.flac") for s in drums.stems)
    assert relative_db(total - read_stem(stems["drums"]), read_stem(stems["drums"])) <= -30


def test_vocals_split_into_lead_and_backing(stems: dict[StemName, Path], tmp_path: Path) -> None:
    partial = tmp_path / "partial"
    pipeline = DetailPipeline(_source())
    pipeline.plan(stems, partial)[1].run(lambda f: None)
    vocals = _outcome(partial, "vocals")
    assert [s.name for s in vocals.stems] == ["lead", "backing", "rest"]
    assert vocals.model == "uvr-mdxnet-kara-2"


def test_instrument_group_runs_the_model_once_on_the_sum(tmp_path: Path) -> None:
    stems = write_stems(tmp_path / "stems", band())
    family = ScaledModel({"electric-guitar": 0.6, "keys": 0.4})
    source = _source(**{p: choice(family, MEGA_TABLE, "mvsep-mega-53", True)
                        for p in ("guitar", "piano", "other")})  # fmt: skip
    partial = tmp_path / "partial"
    pipeline = DetailPipeline(source)
    pipeline.plan(stems, partial)[2].run(lambda f: None)
    assert len(family.inputs) == 1
    expected = read_stem(stems["guitar"]) + read_stem(stems["other"])
    assert np.allclose(family.inputs[0], expected, atol=1e-6)
    for parent in ("guitar", "other"):
        outcome = _outcome(partial, parent)
        assert outcome.model == "mvsep-mega-53"
        assert outcome.outcome is K.SPLIT
        assert [s.name for s in outcome.stems] == ["keys", "electric-guitar", "rest"]


def test_a_single_active_instrument_parent_runs_directly(tmp_path: Path) -> None:
    stems = write_stems(tmp_path / "stems", band(silent=("piano", "other")))
    family = ScaledModel({"electric-guitar": 0.6, "keys": 0.4})
    source = _source(guitar=choice(family, MEGA_TABLE, "mvsep-mega-53", True))
    steps = DetailPipeline(source).plan(stems, tmp_path / "partial")
    assert steps[-1].parents == ("guitar",)
    steps[-1].run(lambda f: None)
    assert np.array_equal(family.inputs[0], read_stem(stems["guitar"]))


def test_fewer_than_two_audible_sub_stems_means_nothing_further(
    stems: dict[StemName, Path], tmp_path: Path
) -> None:
    model = ScaledModel({"lead": 0.999, "backing": 0.001})
    partial = tmp_path / "partial"
    DetailPipeline(_source(vocals=choice(model, VOCAL_TABLE))).plan(stems, partial)[1].run(
        lambda f: None
    )
    vocals = _outcome(partial, "vocals")
    assert (vocals.outcome, vocals.stems) == (K.NOTHING_FURTHER, [])
    assert not (partial / "vocals" / "lead.flac").exists()


def test_a_large_model_residual_is_not_reliable(
    stems: dict[StemName, Path], tmp_path: Path
) -> None:
    model = ScaledModel({"lead": 0.2, "backing": 0.15})
    partial = tmp_path / "partial"
    DetailPipeline(_source(vocals=choice(model, VOCAL_TABLE))).plan(stems, partial)[1].run(
        lambda f: None
    )
    vocals = _outcome(partial, "vocals")
    assert vocals.outcome is K.NOT_RELIABLE
    assert vocals.metrics.model_residual_db == pytest.approx(-3.7, abs=0.1)


def test_double_counted_families_are_not_reliable(tmp_path: Path) -> None:
    stems = write_stems(tmp_path / "stems", band(silent=("piano", "other")))
    model = ScaledModel({"electric-guitar": 1.0, "keys": 1.0})
    partial = tmp_path / "partial"
    source = _source(guitar=choice(model, MEGA_TABLE, "mvsep-mega-53", True))
    DetailPipeline(source).plan(stems, partial)[-1].run(lambda f: None)
    assert _outcome(partial, "guitar").outcome is K.NOT_RELIABLE


def test_a_clipping_rest_fails_reconstruction_and_discards_files(tmp_path: Path) -> None:
    loud = band()
    loud["vocals"] = np.full_like(loud["vocals"], 0.9)
    stems = write_stems(tmp_path / "stems", loud)

    def opposite(audio: np.ndarray) -> dict[str, np.ndarray]:
        return {"lead": -audio, "backing": audio * 0.5}

    partial = tmp_path / "partial"
    model = TransformModel(opposite)
    source = _source(vocals=choice(model, VOCAL_TABLE, families=True))
    DetailPipeline(source).plan(stems, partial)[1].run(lambda f: None)
    vocals = _outcome(partial, "vocals")
    assert vocals.outcome is K.NOT_RELIABLE
    assert vocals.metrics.reconstruction_error_db is not None
    assert vocals.metrics.reconstruction_error_db > -30
    assert sorted(p.name for p in (partial / "vocals").iterdir()) == ["outcome.json"]


def test_finished_parents_are_skipped_when_planning_again(
    stems: dict[StemName, Path], tmp_path: Path
) -> None:
    partial = tmp_path / "partial"
    pipeline = DetailPipeline(_source())
    pipeline.plan(stems, partial)[0].run(lambda f: None)
    (partial / "vocals.tmp").mkdir()
    steps = pipeline.plan(stems, partial)
    assert [s.parents for s in steps] == [("vocals",), ("guitar", "other")]
    assert not (partial / "vocals.tmp").exists()


def test_checkpoints_of_another_separator_version_are_ignored(
    stems: dict[StemName, Path], tmp_path: Path
) -> None:
    partial = tmp_path / "partial"
    DetailPipeline(_source(), version="0").plan(stems, partial)[0].run(lambda f: None)
    (partial / "vocals").mkdir()
    (partial / "vocals" / "outcome.json").write_text("{broken", encoding="utf-8")
    steps = DetailPipeline(_source()).plan(stems, partial)
    assert [s.parents for s in steps][:2] == [("drums",), ("vocals",)]


def test_outcomes_are_read_back_in_standard_order(
    stems: dict[StemName, Path], tmp_path: Path
) -> None:
    partial = tmp_path / "partial"
    pipeline = DetailPipeline(_source())
    _run_all(pipeline, stems, partial)
    outcomes = pipeline.outcomes(tuple(stems), partial)
    assert [o.parent for o in outcomes] == ["vocals", "drums", "bass", "guitar", "piano", "other"]
    assert pipeline.separator_version == "1"


def test_outcomes_without_a_checkpoint_raise(tmp_path: Path) -> None:
    with pytest.raises(LookupError):
        DetailPipeline(_source()).outcomes(("drums",), tmp_path)


def test_cancel_from_progress_stops_the_step_without_a_checkpoint(
    stems: dict[StemName, Path], tmp_path: Path
) -> None:
    partial = tmp_path / "partial"
    step = DetailPipeline(_source()).plan(stems, partial)[0]

    def cancel(fraction: float) -> None:
        if fraction > 0:
            raise DetailCancelledError("cancelled")

    with pytest.raises(DetailCancelledError):
        step.run(cancel)
    assert not (partial / "drums").exists()


class _Provider:
    def __init__(self) -> None:
        self.requested: list[DetailModelId] = []

    def get(self, model_id: DetailModelId) -> DetailModel:
        self.requested.append(model_id)
        return ScaledModel({})


def test_registry_source_maps_parents_to_the_adr_010_models() -> None:
    provider = _Provider()
    source = RegistrySource(provider)
    assert source.sum_instruments
    drums, vocals, guitar = (source.choose(p) for p in ("drums", "vocals", "guitar"))
    assert (drums.model_id, drums.table, drums.policy) == (
        "mdx23c-drumsep-6s", DRUMSEP_TABLE, PARTITION,
    )  # fmt: skip
    assert (vocals.model_id, vocals.table) == ("uvr-mdxnet-kara-2", VOCAL_TABLE)
    assert (guitar.model_id, guitar.table, guitar.policy) == ("mvsep-mega-53", MEGA_TABLE, FAMILIES)
    assert provider.requested == ["mdx23c-drumsep-6s", "uvr-mdxnet-kara-2", "mvsep-mega-53"]
    assert identity_table(("a",)) == {"a": ("a",)}


def test_without_active_instrument_parents_there_is_no_instrument_step(tmp_path: Path) -> None:
    stems = write_stems(tmp_path / "stems", band(silent=("guitar", "piano", "other")))
    steps = DetailPipeline(_source()).plan(stems, tmp_path / "partial")
    assert [s.parents for s in steps] == [("drums",), ("vocals",)]
