"""Tests for environment-based settings."""

from pathlib import Path

import pytest

from stemstudio_worker.config import load_settings


def test_defaults() -> None:
    settings = load_settings({})

    assert settings.data_dir == Path("data/worker")
    assert settings.separator == "demucs"
    assert settings.device == "cpu"
    assert settings.max_duration_seconds == 900
    assert settings.max_upload_bytes == 200 * 1024 * 1024
    assert (settings.primary_model, settings.fallback_model) == ("htdemucs_6s", "htdemucs")


def test_overrides_are_normalised() -> None:
    env = {
        "STEMSTUDIO_DATA_DIR": "/data",
        "STEMSTUDIO_SEPARATOR": " FAKE ",
        "STEMSTUDIO_DEVICE": "Auto",
    }

    settings = load_settings(env)

    assert settings.data_dir == Path("/data")
    assert settings.separator == "fake"
    assert settings.device == "auto"


@pytest.mark.parametrize("name", ["STEMSTUDIO_SEPARATOR", "STEMSTUDIO_DEVICE"])
def test_invalid_choice_raises(name: str) -> None:
    with pytest.raises(ValueError, match=name):
        load_settings({name: "quantum"})


def test_reads_os_environ_by_default(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("STEMSTUDIO_DEVICE", "cuda")

    assert load_settings().device == "cuda"
