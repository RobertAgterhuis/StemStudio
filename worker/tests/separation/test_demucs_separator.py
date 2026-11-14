"""Tests of the Demucs separator with an injected fake model runtime (T058)."""

from collections.abc import Callable
from dataclasses import dataclass, field
from pathlib import Path

import numpy as np
import numpy.typing as npt
import pytest
import soundfile as sf
import torch

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import SeparationError
from stemstudio_worker.separation import create_separator
from stemstudio_worker.separation import demucs_runtime as runtime_module
from stemstudio_worker.separation.demucs_separator import DemucsRuntime, DemucsSeparator
from stemstudio_worker.separation.fake import FakeSeparator

SOURCES_6S = ["drums", "bass", "other", "vocals", "guitar", "piano"]
SOURCES_4 = ["drums", "bass", "other", "vocals"]
Audio = npt.NDArray[np.float32]


@dataclass
class FakeModel:
    sources: list[str]
    samplerate: int = 44_100


@dataclass
class FakeRuntime:
    """Records calls; ``fail_loading`` names models whose loading raises."""

    fail_loading: set[str] = field(default_factory=set)
    cuda: bool = False
    fractions: list[float] = field(default_factory=lambda: [0.3, 0.2, 0.7, 1.4])
    loaded: list[str] = field(default_factory=list)
    devices: list[str] = field(default_factory=list)
    apply_error: Exception | None = None

    def load(self, name: str) -> FakeModel:
        self.loaded.append(name)
        if name in self.fail_loading:
            raise RuntimeError(f"cannot download {name}")
        return FakeModel(SOURCES_6S if name == "htdemucs_6s" else SOURCES_4)

    def apply(
        self, model: FakeModel, mix: Audio, device: str, on_fraction: Callable[[float], None]
    ) -> Audio:
        self.devices.append(device)
        if self.apply_error:
            raise self.apply_error
        for fraction in self.fractions:
            on_fraction(fraction)
        scale = np.arange(1, len(model.sources) + 1, dtype=np.float32)[:, None, None] / 10
        return (mix[None] * scale).astype(np.float32)

    def as_runtime(self) -> DemucsRuntime:
        return DemucsRuntime(load=self.load, apply=self.apply, cuda_available=lambda: self.cuda)


def _separator(fake: FakeRuntime, device: str = "cpu") -> DemucsSeparator:
    return DemucsSeparator(("htdemucs_6s", "htdemucs"), device, fake.as_runtime())  # type: ignore[arg-type]


def test_primary_model_writes_six_aligned_flac_stems(mix_wav: Path, tmp_path: Path) -> None:
    fake = FakeRuntime()

    result = _separator(fake).separate(mix_wav, tmp_path, lambda f, s: None)

    assert result.model == "htdemucs_6s"
    assert list(result.stems) == ["vocals", "drums", "bass", "guitar", "piano", "other"]
    frames = sf.info(mix_wav).frames
    for path in result.stems.values():
        info = sf.info(path)
        assert (info.format, info.subtype, info.samplerate, info.channels) == (
            "FLAC",
            "PCM_16",
            44_100,
            2,
        )
        assert info.frames == frames
    mix, _ = sf.read(mix_wav, dtype="float32")
    vocals, _ = sf.read(result.stems["vocals"], dtype="float32")
    np.testing.assert_allclose(vocals, mix * 0.4, atol=1e-3)  # vocals is source index 3


def test_falls_back_to_four_stem_model_when_primary_cannot_load(
    mix_wav: Path, tmp_path: Path
) -> None:
    fake = FakeRuntime(fail_loading={"htdemucs_6s"})

    result = _separator(fake).separate(mix_wav, tmp_path, lambda f, s: None)

    assert fake.loaded == ["htdemucs_6s", "htdemucs"]
    assert result.model == "htdemucs"
    assert list(result.stems) == ["vocals", "drums", "bass", "other"]


def test_both_models_failing_raises_separation_error(mix_wav: Path, tmp_path: Path) -> None:
    fake = FakeRuntime(fail_loading={"htdemucs_6s", "htdemucs"})

    with pytest.raises(SeparationError, match="could not be loaded"):
        _separator(fake).separate(mix_wav, tmp_path, lambda f, s: None)


def test_model_is_loaded_once_and_reused(mix_wav: Path, tmp_path: Path) -> None:
    fake = FakeRuntime()
    separator = _separator(fake)

    separator.separate(mix_wav, tmp_path, lambda f, s: None)
    separator.separate(mix_wav, tmp_path, lambda f, s: None)

    assert fake.loaded == ["htdemucs_6s"]


def test_apply_failure_raises_readable_separation_error(mix_wav: Path, tmp_path: Path) -> None:
    fake = FakeRuntime(apply_error=RuntimeError("CUDA out of memory at C:\\secret"))

    with pytest.raises(SeparationError, match=r"^Stem separation failed\.$"):
        _separator(fake).separate(mix_wav, tmp_path, lambda f, s: None)


def test_unknown_model_source_raises_separation_error(mix_wav: Path, tmp_path: Path) -> None:
    fake = FakeRuntime()
    fake.load = lambda name: FakeModel(["vocals", "kazoo"])  # type: ignore[method-assign]

    with pytest.raises(SeparationError):
        _separator(fake).separate(mix_wav, tmp_path, lambda f, s: None)


@pytest.mark.parametrize(
    ("device", "cuda", "expected"),
    [("cpu", True, "cpu"), ("cuda", False, "cuda"), ("auto", True, "cuda"), ("auto", False, "cpu")],
)
def test_device_follows_settings(
    mix_wav: Path, tmp_path: Path, device: str, cuda: bool, expected: str
) -> None:
    fake = FakeRuntime(cuda=cuda)

    _separator(fake, device).separate(mix_wav, tmp_path, lambda f, s: None)

    assert fake.devices == [expected]


def test_progress_is_clamped_and_non_decreasing(mix_wav: Path, tmp_path: Path) -> None:
    reports: list[tuple[float, str]] = []

    _separator(FakeRuntime()).separate(mix_wav, tmp_path, lambda f, s: reports.append((f, s)))

    assert [f for f, _ in reports] == [0.0, 0.3, 0.3, 0.7, 1.0, 1.0]
    assert {s for _, s in reports} == {"Separating stems"}


@pytest.mark.parametrize(("kind", "expected"), [("fake", FakeSeparator), ("demucs", DemucsSeparator)])
def test_create_separator_selects_by_settings(tmp_path: Path, kind: str, expected: type) -> None:
    settings = WorkerSettings(data_dir=tmp_path, separator=kind)  # type: ignore[arg-type]

    assert isinstance(create_separator(settings), expected)


# ---------------------------------------------------------------- real runtime adapter


class _HalfModel(torch.nn.Module):
    """Tiny stand-in for a Demucs model: two sources, each half of the input."""

    sources = ["vocals", "other"]  # noqa: RUF012 - mirrors the Demucs attribute
    samplerate = 44_100
    audio_channels = 2
    segment = 0.1

    def forward(self, mix: torch.Tensor) -> torch.Tensor:
        return torch.stack([mix * 0.5, mix * 0.5], dim=1)


def test_runtime_apply_runs_demucs_apply_model_and_denormalises() -> None:
    mix = (np.random.default_rng(1).standard_normal((2, 22_050)) * 0.1).astype(np.float32)
    fractions: list[float] = []

    out = runtime_module.apply_pretrained(_HalfModel(), mix, "cpu", fractions.append)

    assert out.shape == (2, 2, 22_050)
    np.testing.assert_allclose(out[0] + out[1], mix, atol=1e-4)
    assert fractions
    assert fractions == sorted(fractions)


def test_runtime_load_uses_demucs_get_model(monkeypatch: pytest.MonkeyPatch) -> None:
    import demucs.pretrained  # noqa: PLC0415

    monkeypatch.setattr(demucs.pretrained, "get_model", lambda name: f"model:{name}")

    assert runtime_module.load_pretrained("htdemucs_6s") == "model:htdemucs_6s"


def test_runtime_cuda_available_matches_torch() -> None:
    assert runtime_module.cuda_available() is torch.cuda.is_available()
    assert isinstance(runtime_module.default_runtime(), DemucsRuntime)
