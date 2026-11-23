"""Fake quality analyzer for the fake stack and E2E (T036, data-model 2.6, quickstart V1)."""

from pathlib import Path

import pytest

from stemstudio_worker.insights import fake as fake_module
from stemstudio_worker.insights.fake import FAKE_DURATION_SECONDS, FAKE_VERSION, FakeQualityAnalyzer
from stemstudio_worker.insights.rating import RatingInputs, rate
from stemstudio_worker.ports import (
    QualityAnalysisError,
    QualityAnalyzer,
    QualityCancelledError,
    QualityInputs,
    StemAudio,
)
from stemstudio_worker.schemas import QualityResult, StemQuality

CHANNELS = (
    "vocals", "drums", "bass", "guitar", "piano", "other",
    "drums.kick", "drums.snare", "drums.rest", "vocals.lead", "other.strings", "other.rest",
)  # fmt: skip


def _kind(channel: str) -> str:
    return "standard" if "." not in channel else "rest" if channel.endswith(".rest") else "detail"


@pytest.fixture
def inputs(tmp_path: Path) -> QualityInputs:
    stems = []
    for channel in CHANNELS:
        path = tmp_path / f"{channel}.flac"
        path.write_bytes(b"fLaC")  # never read: only the existence is checked
        stems.append(StemAudio(channel, _kind(channel), path))  # type: ignore[arg-type]
    return QualityInputs(stems=tuple(stems), detail_parents=())


@pytest.fixture
def result(inputs: QualityInputs) -> QualityResult:
    return FakeQualityAnalyzer(sleep=lambda _: None).analyze(inputs, lambda _: None)


def _stem(result: QualityResult, channel: str) -> StemQuality:
    return next(stem for stem in result.stems if stem.channel == channel)


def test_implements_the_port_with_its_own_version() -> None:
    analyzer = FakeQualityAnalyzer()
    assert isinstance(analyzer, QualityAnalyzer)
    assert analyzer.version == FAKE_VERSION == "fake-1"


def test_result_shape(result: QualityResult) -> None:
    assert result.analyzer_version == "fake-1"
    assert result.duration_seconds == FAKE_DURATION_SECONDS == 0.5
    assert [stem.channel for stem in result.stems] == list(CHANNELS)
    QualityResult.model_validate(result.model_dump())


@pytest.mark.parametrize(
    ("channel", "rating"),
    [("vocals", "fair"), ("drums", "good"), ("bass", "good"), ("guitar", "poor"),
     ("piano", "not-present"), ("other", "fair")],
)  # fmt: skip
def test_fixed_ratings_of_the_standard_stems(
    result: QualityResult, channel: str, rating: str
) -> None:
    assert _stem(result, channel).rating == rating


def test_the_details_of_data_model_2_6(result: QualityResult) -> None:
    vocals, guitar, other = (_stem(result, name) for name in ("vocals", "guitar", "other"))
    assert (vocals.bleed.level, vocals.bleed.main_source, vocals.rating_reason) == (
        "medium",
        "drums",
        "bleed",
    )
    assert (guitar.artefacts.level, guitar.artefacts.kinds, guitar.rating_reason) == (
        "high", ["flicker"], "artefacts",
    )  # fmt: skip
    assert (other.bleed.level, other.bleed.main_source) == ("medium", "guitar")
    assert _stem(result, "piano").rating_reason == "not-present"


def test_detail_stems_are_good_and_rest_stems_fair(result: QualityResult) -> None:
    for channel in ("drums.kick", "drums.snare", "vocals.lead", "other.strings"):
        stem = _stem(result, channel)
        assert (stem.rating, stem.rating_reason) == ("good", "clean")
    for channel in ("drums.rest", "other.rest"):
        stem = _stem(result, channel)
        assert (stem.rating, stem.artefacts.kinds, stem.rating_reason) == (
            "fair", ["rest-share"], "artefacts",
        )  # fmt: skip


def test_fixed_values_agree_with_the_real_rating_rules(result: QualityResult) -> None:
    """The fake never shows a combination the real analyzer could not produce."""
    for stem in result.stems:
        inputs = RatingInputs(
            active_seconds=stem.active_seconds,
            active_share=stem.active_share,
            level_db=stem.level_db,
            bleed_index=stem.bleed.index,
            bleed_level=stem.bleed.level,
            artefact_level=stem.artefacts.level,
            co_occurrence=stem.bleed.co_occurrence,
        )
        assert rate(inputs) == (stem.rating, stem.rating_reason), stem.channel


def test_labels_are_indicative(result: QualityResult) -> None:
    assert all(s.bleed.reliability == s.artefacts.reliability == "indicative" for s in result.stems)


def test_waits_the_fixed_duration_and_reports_progress(inputs: QualityInputs) -> None:
    waited: list[float] = []
    seen: list[float] = []
    FakeQualityAnalyzer(sleep=waited.append).analyze(inputs, seen.append)
    assert sum(waited) == pytest.approx(0.5)
    assert seen[0] == 0.0
    assert seen[-1] == 1.0
    assert seen == sorted(seen)


def test_cancellation_propagates(inputs: QualityInputs) -> None:
    def cancel(fraction: float) -> None:
        if fraction > 0:
            raise QualityCancelledError

    with pytest.raises(QualityCancelledError):
        FakeQualityAnalyzer(sleep=lambda _: None).analyze(inputs, cancel)


def test_a_missing_file_fails_like_the_real_analyzer(inputs: QualityInputs) -> None:
    inputs.stems[3].path.unlink()
    with pytest.raises(QualityAnalysisError, match=r"guitar\.flac"):
        FakeQualityAnalyzer(sleep=lambda _: None).analyze(inputs, lambda _: None)


def test_default_sleep_is_the_real_clock() -> None:
    assert fake_module.time.sleep is FakeQualityAnalyzer()._sleep  # noqa: SLF001
