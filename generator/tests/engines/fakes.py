"""Builders shared by the fake-engine tests."""

from pathlib import Path

from stemstudio_generator.config import FakeGeneratorMode, GeneratorSettings
from stemstudio_generator.ports import COVER_TURBO, DeviceUse, GpuNeed
from stemstudio_generator.schemas import JobDevice, RestyleParams, SongParams


def fake_settings(mode: FakeGeneratorMode = "ready", step: float = 1.0) -> GeneratorSettings:
    """Settings of a fake generator in ``mode`` with ``step`` seconds per fake step."""
    return GeneratorSettings(engine="fake", fake_mode=mode, fake_step_seconds=step)


def use(need: GpuNeed = COVER_TURBO) -> DeviceUse:
    """A gate grant for ``need`` on a fake GPU."""
    return DeviceUse(need, JobDevice(kind="gpu", name="Fake GPU"), 14_960)


class Recorder:
    """Records progress calls and sleeps."""

    def __init__(self) -> None:
        self.progress: list[tuple[float, str]] = []
        self.sleeps: list[float] = []

    def on_progress(self, fraction: float, stage: str) -> None:
        self.progress.append((fraction, stage))

    def sleep(self, seconds: float) -> None:
        self.sleeps.append(seconds)


def restyle_params(seeds: list[int], mode: str = "range") -> RestyleParams:
    """Restyle parameters for a 10 s range."""
    return RestyleParams.model_validate(
        {
            "caption": "jazz trio",
            "audioCoverStrength": 0.5,
            "seeds": seeds,
            "mode": mode,
            "rangeStartSeconds": 0.0,
            "rangeEndSeconds": 10.0,
        }
    )


def song_params(
    seeds: list[int], duration: float = 10.0, *, instrumental: bool = False, thinking: bool = True
) -> SongParams:
    """Song parameters of ``duration`` seconds."""
    return SongParams.model_validate(
        {
            "caption": "pop ballad",
            "lyrics": "[Instrumental]" if instrumental else "[verse]\nla la la",
            "instrumental": instrumental,
            "vocalLanguage": "en",
            "durationSeconds": duration,
            "seeds": seeds,
            "thinking": thinking,
        }
    )


def out_dir(tmp_path: Path, name: str = "job") -> Path:
    """A fresh output directory under ``tmp_path``."""
    path = tmp_path / name
    path.mkdir()
    return path
