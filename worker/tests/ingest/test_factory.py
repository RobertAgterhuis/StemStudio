"""T139: ``create_ingest_adapters`` wires the production adapters with the settings' limits."""

import sys
from pathlib import Path

import pytest

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ingest import create_ingest_adapters
from stemstudio_worker.ingest.fetcher import YtDlpFetcher
from stemstudio_worker.ingest.normalizer import FfmpegNormalizer
from stemstudio_worker.ingest.resolver import UrlSourceResolver
from stemstudio_worker.ingest.upload import FileUploadIntake
from stemstudio_worker.ports import (
    AudioFetcher,
    AudioNormalizer,
    IngestAdapters,
    SourceResolver,
    UploadIntake,
)
from stemstudio_worker.schemas import SourceType


def _settings(**overrides: float | int) -> WorkerSettings:
    return WorkerSettings(data_dir=Path("unused"), **overrides)  # type: ignore[arg-type]


def test_factory_returns_the_four_production_adapters() -> None:
    adapters = create_ingest_adapters(_settings())

    assert isinstance(adapters, IngestAdapters)
    assert isinstance(adapters.resolver, UrlSourceResolver)
    assert isinstance(adapters.upload_intake, FileUploadIntake)
    assert isinstance(adapters.fetcher, YtDlpFetcher)
    assert isinstance(adapters.normalizer, FfmpegNormalizer)


def test_members_satisfy_the_ingest_protocols() -> None:
    adapters = create_ingest_adapters(_settings())

    resolver: SourceResolver = adapters.resolver
    intake: UploadIntake = adapters.upload_intake
    fetcher: AudioFetcher = adapters.fetcher
    normalizer: AudioNormalizer = adapters.normalizer
    assert callable(resolver.resolve)
    assert callable(intake.accept)
    assert callable(fetcher.fetch)
    assert callable(normalizer.normalize)


def test_factory_uses_the_settings_limits() -> None:
    adapters = create_ingest_adapters(_settings(max_duration_seconds=120.0, max_upload_bytes=1024))

    assert isinstance(adapters.fetcher, YtDlpFetcher)
    assert isinstance(adapters.normalizer, FfmpegNormalizer)
    assert isinstance(adapters.upload_intake, FileUploadIntake)
    assert adapters.fetcher.max_duration_seconds == 120.0
    assert adapters.normalizer.max_duration_seconds == 120.0
    assert adapters.upload_intake.max_upload_bytes == 1024


def test_default_settings_give_15_minutes_and_200_mib() -> None:
    adapters = create_ingest_adapters(_settings())

    assert isinstance(adapters.fetcher, YtDlpFetcher)
    assert isinstance(adapters.upload_intake, FileUploadIntake)
    assert adapters.fetcher.max_duration_seconds == 900.0
    assert adapters.upload_intake.max_upload_bytes == 209_715_200


def test_factory_does_not_import_spotdl_or_yt_dlp_eagerly(monkeypatch: pytest.MonkeyPatch) -> None:
    for name in [m for m in sys.modules if m.split(".")[0] in ("spotdl", "yt_dlp")]:
        monkeypatch.delitem(sys.modules, name)

    adapters = create_ingest_adapters(_settings())
    youtube = adapters.resolver.resolve("https://youtu.be/dQw4w9WgXcQ")

    assert youtube.source_type is SourceType.YOUTUBE
    assert not any(m.split(".")[0] in ("spotdl", "yt_dlp") for m in sys.modules)
