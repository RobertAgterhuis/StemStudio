"""Composition of the insights lane (020 T040): analyzer from the settings, health.insights."""

from pathlib import Path
from types import ModuleType, SimpleNamespace
from typing import Any

import pytest

from stemstudio_worker.config import InsightsSettings, RenderSettings, WorkerSettings
from stemstudio_worker.handouts.health import fonts_health, renderer_health
from stemstudio_worker.jobs.quality_service import UNAVAILABLE_TEXT, UnavailableAnalyzer
from stemstudio_worker.main import (
    QUALITY_ANALYZERS,
    AppOverrides,
    create_quality_analyzer,
    default_insights,
)
from stemstudio_worker.ports import QualityAnalysisError, QualityInputs

from .conftest import ApiFactory
from .insights_fakes import FakeQualityAnalyzer


class _Real:
    version = "1.0.0"


class _Fake:
    version = "fake-1"


def _loader(modules: dict[str, Any]) -> Any:
    def load(name: str) -> ModuleType:
        if name not in modules:
            raise ImportError(name)
        module: ModuleType = modules[name]
        return module

    return load


_MODULES = {
    "stemstudio_worker.insights.analyzer": SimpleNamespace(SignalQualityAnalyzer=_Real),
    "stemstudio_worker.insights.fake": SimpleNamespace(FakeQualityAnalyzer=_Fake),
}


def _settings(kind: str = "real", threads: int = 4) -> WorkerSettings:
    insights = InsightsSettings(quality_analyzer=kind, threads=threads)  # type: ignore[arg-type]
    return WorkerSettings(data_dir=Path("data"), separator="fake", device="cpu", insights=insights)


def test_the_analyzer_targets_name_the_beat_analysis_modules() -> None:
    assert QUALITY_ANALYZERS == {
        "real": "insights.analyzer:SignalQualityAnalyzer",
        "fake": "insights.fake:FakeQualityAnalyzer",
    }


@pytest.mark.parametrize(("kind", "expected"), [("real", _Real), ("fake", _Fake)])
def test_the_settings_pick_the_real_or_the_fake_analyzer(kind: str, expected: type) -> None:
    analyzer = create_quality_analyzer(_settings(kind), _loader(_MODULES))

    assert isinstance(analyzer, expected)


def test_a_missing_analyzer_module_gives_an_analyzer_that_fails_readably() -> None:
    analyzer = create_quality_analyzer(_settings("fake"), _loader({}))

    assert isinstance(analyzer, UnavailableAnalyzer)
    assert analyzer.version == "0.0.0"
    with pytest.raises(QualityAnalysisError, match=UNAVAILABLE_TEXT):
        analyzer.analyze(QualityInputs(stems=(), detail_parents=()), lambda _f: None)


def test_default_insights_reports_kind_version_renderer_fonts_and_threads() -> None:
    parts = default_insights(_settings("fake", threads=2), AppOverrides(), _loader(_MODULES))

    assert parts.threads == 2
    assert parts.health.analyzer == "fake"
    assert parts.health.analyzer_version == "fake-1"
    assert parts.health.renderer == renderer_health()
    assert parts.health.fonts == fonts_health()


def test_an_injected_analyzer_wins(make_api: ApiFactory) -> None:
    analyzer = FakeQualityAnalyzer()
    parts = default_insights(_settings(), AppOverrides(quality_analyzer=analyzer), _loader({}))

    assert parts.analyzer is analyzer
    assert parts.health.analyzer_version == "fake-1"


def test_the_running_worker_reports_the_configured_insights(
    make_api: ApiFactory, tmp_path: Path
) -> None:
    api = make_api(overrides=AppOverrides(quality_analyzer=FakeQualityAnalyzer()))

    body = api.client.get("/health").json()["insights"]

    assert body["renderer"] == renderer_health().model_dump(by_alias=True)
    assert body["fonts"] == fonts_health().model_dump(by_alias=True)


def test_render_settings_are_untouched_by_the_insights_composition() -> None:
    settings = WorkerSettings(data_dir=Path("d"), separator="fake", device="cpu",
                              render=RenderSettings(library_dir=None))  # fmt: skip

    assert default_insights(settings, AppOverrides(), _loader({})).threads == 4
