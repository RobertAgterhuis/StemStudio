"""Tests of the real detail runtimes with fake torch/ONNX callables (T048).

The vendored networks and real weights are exercised only by the marked integration test.
"""

import subprocess
import sys
from pathlib import Path
from types import SimpleNamespace

import numpy as np
import pytest
import torch

from stemstudio_worker.detail import demix as demix_module
from stemstudio_worker.detail import mdx_net, runtimes
from stemstudio_worker.detail.demix import DemixPlan, MsstModel, demix
from stemstudio_worker.detail.download import ModelDownloader
from stemstudio_worker.detail.folding import MEGA_OUTPUTS
from stemstudio_worker.detail.gating import relative_db
from stemstudio_worker.detail.mdx_net import COMPENSATE, Kara2Model
from stemstudio_worker.detail.registry import ModelSpec
from stemstudio_worker.detail.runtimes import RealModelProvider, resolve_device
from stemstudio_worker.ports import (
    DetailAudio,
    DetailCancelledError,
    DetailModel,
    DetailModelProvider,
    DetailSeparationError,
)
from stemstudio_worker.schemas import DetailModelId

from .signals import noise, tone

PLAN = DemixPlan(chunk_size=4000, overlap=2)


def scaled_network(scales: tuple[float, ...]):  # type: ignore[no-untyped-def]
    """A fake network: (batch, channels, time) -> (batch, outputs, channels, time)."""

    def network(batch: torch.Tensor) -> torch.Tensor:
        return torch.stack([batch * s for s in scales], dim=1)

    return network


def test_demix_overlap_adds_chunks_back_to_the_full_signal() -> None:
    mix = noise(0.5, 0.3)
    fractions: list[float] = []
    out = demix(scaled_network((0.5, 0.25)), mix, PLAN, [0, 1], lambda f, s: fractions.append(f))
    assert out.shape == (2, *mix.shape)
    assert np.allclose(out[0], 0.5 * mix, atol=1e-5)
    assert np.allclose(out[1], 0.25 * mix, atol=1e-5)
    assert fractions == sorted(fractions)
    assert fractions[-1] == 1.0
    assert len(fractions) == -(
        -(mix.shape[1] + 2 * (PLAN.chunk_size // 2)) // (PLAN.chunk_size // 2)
    )


def test_demix_keeps_only_the_selected_outputs() -> None:
    mix = noise(0.3, 0.3)
    out = demix(scaled_network((0.1, 0.2, 0.3)), mix, PLAN, [2], lambda f, s: None)
    assert out.shape == (1, *mix.shape)
    assert np.allclose(out[0], 0.3 * mix, atol=1e-5)


def test_demix_of_a_short_input_pads_the_single_chunk() -> None:
    mix = noise(0.05, 0.3)
    out = demix(scaled_network((1.0,)), mix, PLAN, [0], lambda f, s: None)
    assert np.allclose(out[0], mix, atol=1e-5)


def test_demix_stops_when_progress_cancels() -> None:
    def cancel(fraction: float, stage: str) -> None:
        raise DetailCancelledError("stop")

    with pytest.raises(DetailCancelledError):
        demix(scaled_network((1.0,)), noise(0.5, 0.3), PLAN, [0], cancel)


def test_msst_model_names_the_kept_outputs() -> None:
    model = MsstModel(scaled_network((0.1, 0.2, 0.3)), PLAN, ("a", "b", "c"), ("c", "a"))
    outputs = model.separate(noise(0.2, 0.3), lambda f, s: None)
    assert list(outputs) == ["c", "a"]
    assert outputs["a"].dtype == np.float32


def test_mega_keeps_eight_family_accumulators() -> None:
    names = ("bass", *MEGA_OUTPUTS, "vocal")
    scales = tuple(0.01 * (i + 1) for i in range(len(names)))
    model = MsstModel(scaled_network(scales), PLAN, names, MEGA_OUTPUTS)
    outputs = model.separate(noise(0.2, 0.3), lambda f, s: None)
    assert list(outputs) == list(MEGA_OUTPUTS)


@pytest.mark.parametrize(
    ("message", "error"),
    [("DefaultCPUAllocator: can't allocate memory", MemoryError),
     ("shape mismatch", DetailSeparationError)],
)  # fmt: skip
def test_inference_errors_become_memory_or_separation_errors(
    message: str, error: type[Exception]
) -> None:
    def broken(batch: torch.Tensor) -> torch.Tensor:
        raise RuntimeError(message)

    with pytest.raises(error):
        MsstModel(broken, PLAN, ("a",), ("a",)).separate(noise(0.1, 0.3), lambda f, s: None)


def test_kara2_backing_is_the_compensated_model_output_and_lead_the_difference() -> None:
    mix = tone(1.0, 0.3, freq=440.0)
    calls: list[tuple[int, ...]] = []

    def session(spec: np.ndarray) -> np.ndarray:
        calls.append(spec.shape)
        return spec * 0.5

    fractions: list[float] = []
    outputs = Kara2Model(session).separate(mix, lambda f, s: fractions.append(f))
    assert COMPENSATE == 1.065
    assert calls[0] == (1, 4, mdx_net.DIM_F, mdx_net.DIM_T)
    assert relative_db(outputs["backing"] - 0.5 * COMPENSATE * mix, mix) < -40
    assert np.allclose(outputs["lead"], mix - outputs["backing"], atol=1e-6)
    assert fractions[-1] == 1.0
    assert len(fractions) == len(calls)


def test_kara2_handles_input_shorter_than_one_chunk() -> None:
    mix = tone(0.1, 0.3)
    outputs = Kara2Model(lambda spec: spec).separate(mix, lambda f, s: None)
    assert outputs["backing"].shape == mix.shape


def test_kara2_wraps_runtime_failures() -> None:
    def broken(spec: np.ndarray) -> np.ndarray:
        raise RuntimeError("onnx failure")

    with pytest.raises(DetailSeparationError):
        Kara2Model(broken).separate(tone(0.1, 0.3), lambda f, s: None)


class _Downloader(ModelDownloader):
    def __init__(self, root: Path) -> None:
        super().__init__(root)
        self.ensured: list[str] = []

    def ensure(self, spec: ModelSpec) -> Path:
        self.ensured.append(spec.model_id)
        return Path("weights") / spec.file_name


class _Model:
    def separate(self, audio: DetailAudio, progress: object) -> dict[str, DetailAudio]:
        return {}


def test_provider_downloads_and_loads_each_model_once(tmp_path: Path) -> None:
    downloader = _Downloader(tmp_path)
    loaded: list[tuple[str, str]] = []

    def loader(path: Path, device: str) -> DetailModel:
        loaded.append((path.name, device))
        return _Model()  # type: ignore[return-value]

    loaders: dict[DetailModelId, runtimes.Loader] = {"uvr-mdxnet-kara-2": loader}
    provider = RealModelProvider(downloader, "cpu", loaders)
    assert isinstance(provider, DetailModelProvider)
    first = provider.get("uvr-mdxnet-kara-2")
    assert provider.get("uvr-mdxnet-kara-2") is first
    assert downloader.ensured == ["uvr-mdxnet-kara-2"]
    assert loaded == [("UVR_MDXNET_KARA_2.onnx", "cpu")]


def test_provider_uses_the_registry_loaders_by_default(tmp_path: Path) -> None:
    provider = RealModelProvider(ModelDownloader(tmp_path), "cpu")
    assert set(provider.loaders) == {"mdx23c-drumsep-6s", "uvr-mdxnet-kara-2", "mvsep-mega-53"}


@pytest.mark.parametrize(
    ("preference", "cuda", "device"),
    [("cpu", True, "cpu"), ("cuda", False, "cuda"), ("auto", True, "cuda"), ("auto", False, "cpu")],
)
def test_resolve_device(
    monkeypatch: pytest.MonkeyPatch, preference: str, cuda: bool, device: str
) -> None:
    monkeypatch.setattr(torch.cuda, "is_available", lambda: cuda)
    assert resolve_device(preference) == device


class _Tiny(torch.nn.Module):
    def __init__(self) -> None:
        super().__init__()
        self.scale = torch.nn.Parameter(torch.ones(1))

    def forward(self, batch: torch.Tensor) -> torch.Tensor:
        return torch.stack([batch * self.scale] * 6, dim=1)


def test_torch_weights_are_loaded_safely_into_the_network(tmp_path: Path) -> None:
    path = tmp_path / "w.ckpt"
    torch.save({"state_dict": {"scale": torch.tensor([0.5])}}, path)
    network = runtimes.load_network(_Tiny, path, "cpu")
    out = network(torch.ones(1, 2, 8))
    assert out.shape == (1, 6, 2, 8)
    assert float(out[0, 0, 0, 0]) == 0.5


def test_mdx23c_and_mega_loaders_wrap_the_vendored_networks(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    path = tmp_path / "w.ckpt"
    torch.save({"scale": torch.tensor([1.0])}, path)
    fake_vendor = SimpleNamespace(
        build_mdx23c=_Tiny, build_mega53=_Tiny,
        DRUMSEP_INSTRUMENTS=("kick", "snare", "toms", "hh", "ride", "crash"),
        MEGA_INSTRUMENTS=MEGA_OUTPUTS[:6],
    )  # fmt: skip
    monkeypatch.setattr(runtimes, "_vendor", lambda: fake_vendor)
    drums = runtimes.load_mdx23c(path, "cpu")
    assert isinstance(drums, MsstModel)
    assert drums.plan == runtimes.MDX23C_PLAN == DemixPlan(130_560, 2)
    mega = runtimes.load_mega53(path, "cpu")
    assert isinstance(mega, MsstModel)
    assert mega.plan == runtimes.MEGA_PLAN == DemixPlan(882_000, 2)
    assert sorted(mega.separate(noise(0.05, 0.1), lambda f, s: None)) == sorted(MEGA_OUTPUTS[:6])


def test_kara2_loader_runs_the_onnx_session(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    created: list[tuple[str, list[str]]] = []

    class Session:
        def __init__(self, path: str, providers: list[str]) -> None:
            created.append((path, providers))

        def get_inputs(self) -> list[SimpleNamespace]:
            return [SimpleNamespace(name="input")]

        def run(self, names: None, feeds: dict[str, np.ndarray]) -> list[np.ndarray]:
            return [feeds["input"]]

    monkeypatch.setitem(sys.modules, "onnxruntime", SimpleNamespace(InferenceSession=Session))
    model = runtimes.load_kara2(tmp_path / "k.onnx", "cpu")
    outputs = model.separate(tone(0.2, 0.3), lambda f, s: None)
    assert created == [(str(tmp_path / "k.onnx"), ["CPUExecutionProvider"])]
    assert sorted(outputs) == ["backing", "lead"]


def test_importing_the_runtimes_does_not_import_onnxruntime_or_the_vendored_code() -> None:
    code = (
        "import sys, stemstudio_worker.detail.runtimes, stemstudio_worker.detail;"
        "print('onnxruntime' in sys.modules, 'stemstudio_worker.detail.vendor.msst' in sys.modules)"
    )
    result = subprocess.run(  # noqa: S603 - fixed argument list
        [sys.executable, "-c", code], capture_output=True, text=True, check=True
    )
    assert result.stdout.split() == ["False", "False"]
    assert demix_module.__name__.endswith("demix")
