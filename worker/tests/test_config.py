"""Tests for environment-based settings."""

from pathlib import Path

import pytest

from stemstudio_worker.config import GPU_LEASE_BUSY_REASON, load_settings


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


# ------------------------------------------------------------------ GPU file lease (009 T004)


def test_gpu_lease_is_off_by_default() -> None:
    settings = load_settings({})

    assert settings.gpu_lease_file is None
    assert settings.gpu_lease_wait_seconds == 900


def test_gpu_lease_file_and_wait_are_read() -> None:
    env = {
        "STEMSTUDIO_GPU_LEASE_FILE": " /gpu-lease/gpu.lock ",
        "STEMSTUDIO_GPU_LEASE_WAIT_SECONDS": "30",
    }

    settings = load_settings(env)

    assert settings.gpu_lease_file == Path("/gpu-lease/gpu.lock")
    assert settings.gpu_lease_wait_seconds == 30


def test_blank_gpu_lease_file_means_off() -> None:
    assert load_settings({"STEMSTUDIO_GPU_LEASE_FILE": "  "}).gpu_lease_file is None


@pytest.mark.parametrize("raw", ["-1", "soon"])
def test_invalid_gpu_lease_wait_raises(raw: str) -> None:
    with pytest.raises(ValueError, match="STEMSTUDIO_GPU_LEASE_WAIT_SECONDS"):
        load_settings({"STEMSTUDIO_GPU_LEASE_WAIT_SECONDS": raw})


def test_busy_reason_matches_the_contract_text() -> None:
    assert GPU_LEASE_BUSY_REASON == "GPU busy with another StemStudio job"
