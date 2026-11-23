"""The signal quality analyzer behind ``ports.QualityAnalyzer`` (T034/T035, data-model 2.4, 4)."""

from pathlib import Path

import numpy as np
import pytest

from stemstudio_worker.insights.analyzer import SignalQualityAnalyzer
from stemstudio_worker.insights.thresholds import ANALYZER_VERSION
from stemstudio_worker.ports import (
    QualityAnalysisError,
    QualityAnalyzer,
    QualityCancelledError,
    QualityInputs,
    StemAudio,
)
from stemstudio_worker.schemas import DetailParentInput, QualityResult, StemQuality

from .synthetic import FAMILIES, arranged_song, gate_holes, inject_bleed, write_stem

SECONDS = 20.0


@pytest.fixture(scope="module")
def library(tmp_path_factory: pytest.TempPathFactory) -> dict[str, Path]:
    root = tmp_path_factory.mktemp("stems")
    stems = arranged_song(seconds=SECONDS)
    drums = stems["drums"]
    files = {name: stems[name] for name in FAMILIES}
    files["vocals"] = inject_bleed(stems["vocals"], drums, 0.3)  # much drums in the vocals
    files["guitar"] = gate_holes(stems["guitar"], 2.0, seed=4)  # gating artefacts
    files["piano"] = 0.02 * stems["other"]  # no piano in this song: spill only
    files["drums.kick"] = 0.4 * drums
    files["drums.rest"] = 0.6 * drums  # 36 % of the drums energy went to the rest stem
    return {name: write_stem(root / f"{name}.flac", sound) for name, sound in files.items()}


def _inputs(
    library: dict[str, Path], *, parents: tuple[DetailParentInput, ...] = ()
) -> QualityInputs:
    def kind(name: str) -> str:
        return "standard" if "." not in name else "rest" if name.endswith(".rest") else "detail"

    stems = tuple(StemAudio(name, kind(name), path) for name, path in library.items())  # type: ignore[arg-type]
    return QualityInputs(stems=stems, detail_parents=parents)


@pytest.fixture(scope="module")
def result(library: dict[str, Path]) -> QualityResult:
    parent = DetailParentInput(
        parent="drums", model_residual_db=-12.0, reconstruction_error_db=-170.0
    )
    return SignalQualityAnalyzer().analyze(_inputs(library, parents=(parent,)), lambda _: None)


def _stem(result: QualityResult, channel: str) -> StemQuality:
    return next(stem for stem in result.stems if stem.channel == channel)


def test_implements_the_port() -> None:
    analyzer = SignalQualityAnalyzer()
    assert isinstance(analyzer, QualityAnalyzer)
    assert analyzer.version == ANALYZER_VERSION


def test_result_keeps_the_request_order_and_validates(result: QualityResult, library: dict) -> None:
    assert [stem.channel for stem in result.stems] == list(library)
    assert result.analyzer_version == ANALYZER_VERSION
    assert result.duration_seconds > 0
    QualityResult.model_validate(result.model_dump())


def test_clean_stems_rate_good(result: QualityResult) -> None:
    for channel in ("bass", "other"):
        stem = _stem(result, channel)
        assert (stem.rating, stem.rating_reason) == ("good", "clean"), stem
        assert stem.active_seconds >= 10.0


def test_bleed_of_drums_into_vocals(result: QualityResult) -> None:
    vocals = _stem(result, "vocals")
    assert vocals.bleed.main_source == "drums"
    assert vocals.bleed.level == "high"
    assert (vocals.rating, vocals.rating_reason) == ("poor", "bleed")


def test_gating_artefacts_in_guitar(result: QualityResult) -> None:
    guitar = _stem(result, "guitar")
    assert guitar.artefacts.level in ("medium", "high")
    assert guitar.artefacts.kinds == ["dropouts"]
    assert guitar.rating_reason in ("artefacts", "bleed-and-artefacts")


def test_spill_only_piano_is_not_present(result: QualityResult) -> None:
    piano = _stem(result, "piano")
    assert piano.level_db < -30.0
    assert piano.bleed.main_source == "other"
    assert (piano.rating, piano.rating_reason) == ("not-present", "not-present")


def test_detail_and_rest_stems_get_their_kinds(result: QualityResult) -> None:
    kick = _stem(result, "drums.kick")
    rest = _stem(result, "drums.rest")
    assert "model-residual" in kick.artefacts.kinds
    assert "rest-share" in kick.artefacts.kinds
    assert "rest-share" in rest.artefacts.kinds
    assert "model-residual" not in rest.artefacts.kinds


def test_every_label_is_indicative_and_flicker_is_never_reported(result: QualityResult) -> None:
    for stem in result.stems:
        assert stem.bleed.reliability == "indicative"
        assert stem.artefacts.reliability == "indicative"
        assert "flicker" not in stem.artefacts.kinds
        assert -120.0 <= stem.level_db <= 20.0
        assert 0.0 <= stem.active_share <= 1.0


def test_progress_rises_to_one(library: dict[str, Path]) -> None:
    seen: list[float] = []
    SignalQualityAnalyzer().analyze(_inputs(library), seen.append)
    assert seen[0] == 0.0
    assert seen[-1] == 1.0
    assert seen == sorted(seen)
    assert len(seen) >= len(library)


def test_cancellation_propagates(library: dict[str, Path]) -> None:
    def cancel(fraction: float) -> None:
        if fraction > 0.3:
            raise QualityCancelledError

    with pytest.raises(QualityCancelledError):
        SignalQualityAnalyzer().analyze(_inputs(library), cancel)


def test_silent_and_short_stems_are_too_short(tmp_path: Path, library: dict[str, Path]) -> None:
    files = dict(library)
    files["piano"] = write_stem(tmp_path / "silent.flac", np.zeros(44_100 * 5))
    files["other"] = write_stem(tmp_path / "short.flac", 0.1 * np.sin(np.arange(44_100 * 5) / 7))
    result = SignalQualityAnalyzer().analyze(_inputs(files), lambda _: None)
    for channel in ("piano", "other"):
        stem = _stem(result, channel)
        assert (stem.rating, stem.rating_reason) == ("unknown", "too-short")
    assert _stem(result, "piano").level_db == -120.0


def test_a_missing_standard_stem_is_refused(library: dict[str, Path]) -> None:
    files = {name: path for name, path in library.items() if name != "bass"}
    with pytest.raises(QualityAnalysisError, match="six standard stems"):
        SignalQualityAnalyzer().analyze(_inputs(files), lambda _: None)


def test_an_unreadable_stem_fails_the_job(tmp_path: Path, library: dict[str, Path]) -> None:
    bad = tmp_path / "kick.flac"
    bad.write_bytes(b"\x00" * 64)
    files = dict(library)
    files["drums.kick"] = bad
    with pytest.raises(QualityAnalysisError, match=r"kick\.flac"):
        SignalQualityAnalyzer().analyze(_inputs(files), lambda _: None)
