"""Tests for the environment-based generator settings (009 + 010 data-model.md 1)."""

from pathlib import Path
from typing import get_args

import pytest

from stemstudio_generator.config import FakeGeneratorMode, GeneratorSettings, load_settings


def test_defaults_are_the_acestep_generator() -> None:
    settings = load_settings({})

    assert settings == GeneratorSettings()
    assert settings.kinds == ("restyle", "song")
    assert settings.voice_sample_check is False
    assert settings.gpu_lease_file == Path("/gpu-lease/gpu.lock")
    assert (settings.gpu_memory_wait_seconds, settings.svc_max_target_seconds) == (600, 480)


def test_soulx_engine_defaults_to_the_singer_kinds() -> None:
    settings = load_settings({"STEMSTUDIO_GENERATOR": "SoulX"})

    assert (settings.engine, settings.kinds, settings.voice_sample_check) == (
        "soulx",
        ("voiceConversion",),
        True,
    )


def test_fake_singer_from_explicit_kinds_and_mode() -> None:
    env = {
        "STEMSTUDIO_GENERATOR": "fake",
        "STEMSTUDIO_GENERATOR_KINDS": " voiceConversion , voiceConversion ",
        "STEMSTUDIO_FAKE_GENERATOR_MODE": "unvoiced-sample",
        "STEMSTUDIO_FAKE_GENERATOR_STEP_SECONDS": "0.1",
        "STEMSTUDIO_DATA_DIR": "/srv/gen",
        "STEMSTUDIO_MODEL_DIR": "/cache",
        "STEMSTUDIO_GPU_LEASE_FILE": "",
        "STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS": "5",
        "STEMSTUDIO_SVC_MAX_TARGET_SECONDS": "120",
    }

    settings = load_settings(env)

    assert settings == GeneratorSettings(
        engine="fake",
        kinds=("voiceConversion",),
        fake_mode="unvoiced-sample",
        fake_step_seconds=0.1,
        data_dir=Path("/srv/gen"),
        model_dir=Path("/cache"),
        gpu_lease_file=None,
        gpu_memory_wait_seconds=5,
        svc_max_target_seconds=120,
    )


def test_fake_modes_are_the_documented_set() -> None:
    assert get_args(FakeGeneratorMode) == (
        "ready", "no-gpu", "oom", "oom-twice", "busy", "slow", "unvoiced-sample",
    )  # fmt: skip


@pytest.mark.parametrize(
    ("name", "value"),
    [
        ("STEMSTUDIO_GENERATOR", "yue"),
        ("STEMSTUDIO_FAKE_GENERATOR_MODE", "melting"),
        ("STEMSTUDIO_GENERATOR_KINDS", "restyle,karaoke"),
        ("STEMSTUDIO_GENERATOR_KINDS", " , "),
        ("STEMSTUDIO_GPU_MEMORY_WAIT_SECONDS", "-1"),
        ("STEMSTUDIO_FAKE_GENERATOR_STEP_SECONDS", "soon"),
    ],
)
def test_invalid_values_raise(name: str, value: str) -> None:
    with pytest.raises(ValueError, match=name):
        load_settings({name: value})


def test_reads_os_environ_by_default(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setenv("STEMSTUDIO_GENERATOR", "fake")

    assert load_settings().engine == "fake"
