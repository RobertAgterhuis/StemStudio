"""Tests of the alignment readiness behind ``Health.lyricsAlignment`` (006 T042, SC-005)."""

import importlib.util
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.alignment import alignment_status
from stemstudio_worker.config import LyricsSettings, WorkerSettings
from stemstudio_worker.ports import WHISPER_TURBO_WEIGHTS
from stemstudio_worker.schemas import LyricsAlignmentHealth

MISSING = "The speech alignment packages are not installed."


def _settings(tmp_path: Path, **changes: Any) -> WorkerSettings:
    return WorkerSettings(data_dir=tmp_path, model_dir=tmp_path / "models", **changes)


def _turbo(tmp_path: Path, size: int) -> None:
    path = tmp_path / "models" / "whisper" / "large-v3-turbo.pt"
    path.parent.mkdir(parents=True)
    with path.open("wb") as handle:
        handle.truncate(size)


def test_a_real_worker_reports_the_turbo_model_and_whether_it_is_cached(tmp_path: Path) -> None:
    status = alignment_status(_settings(tmp_path))
    assert status.health() == LyricsAlignmentHealth(available=True, model="large-v3-turbo",
                                                    model_cached=False, auto="gpu")  # fmt: skip
    _turbo(tmp_path, WHISPER_TURBO_WEIGHTS.size_bytes)

    assert status.model_cached()
    assert status.health().model_cached


def test_a_partial_download_is_not_cached(tmp_path: Path) -> None:
    _turbo(tmp_path, 1_000)

    assert not alignment_status(_settings(tmp_path)).model_cached()


def test_the_small_model_and_the_auto_mode_are_reported(tmp_path: Path) -> None:
    lyrics = LyricsSettings(align_model="small", align_auto="never")

    health = alignment_status(_settings(tmp_path, lyrics=lyrics)).health()

    assert (health.model, health.auto) == ("small", "never")


def test_the_fake_stack_needs_no_model(tmp_path: Path) -> None:
    status = alignment_status(_settings(tmp_path, separator="fake"))

    assert status.model_cached()
    assert status.health().available


def test_missing_packages_make_alignment_unavailable(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setattr(importlib.util, "find_spec", lambda name: None)

    health = alignment_status(_settings(tmp_path)).health()

    assert (health.available, health.reason) == (False, MISSING)
