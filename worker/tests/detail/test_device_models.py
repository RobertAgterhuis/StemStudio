"""Detail models per device and parent steps through the ``DeviceRunner`` (T025, ADR-012 D4-D6)."""

import sys
from pathlib import Path
from types import SimpleNamespace

import numpy as np
import pytest

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.detail import create_detail_pipeline, runtimes
from stemstudio_worker.detail.download import ModelDownloader
from stemstudio_worker.detail.fake import FAKE_DETAIL_NEED, FakeSource
from stemstudio_worker.detail.folding import DRUMSEP_TABLE, VOCAL_TABLE, FoldTable
from stemstudio_worker.detail.mdx_net import Kara2Model
from stemstudio_worker.detail.registry import GPU_NEEDS, ModelSpec
from stemstudio_worker.detail.runtimes import ONNX_NO_CUDA, RealModelProvider
from stemstudio_worker.detail.steps import (
    PARTITION,
    DetailPipeline,
    ModelChoice,
    ModelLoader,
    RegistrySource,
)
from stemstudio_worker.ports import DetailAudio, DetailModel, DeviceUse, GpuNeed, ProgressCallback
from stemstudio_worker.schemas import DetailModelId, ParentStemName, StemName

from .models import NEED, RecordingRunner, ScaledModel, Source, band, choice, write_stems

KARA_REASON = "ONNX Runtime has no usable CUDA provider; UVR MDX-Net KARA 2 ran on the CPU."


class _Downloader(ModelDownloader):
    def ensure(self, spec: ModelSpec) -> Path:
        return Path("weights") / spec.file_name


def _provider(tmp_path: Path, loaded: list[tuple[str, str]]) -> RealModelProvider:
    def loader(path: Path, device: str) -> DetailModel:
        loaded.append((path.name, device))
        return ScaledModel({})

    loaders: dict[DetailModelId, runtimes.Loader] = {
        "uvr-mdxnet-kara-2": loader, "mvsep-mega-53": loader,
    }  # fmt: skip
    return RealModelProvider(_Downloader(tmp_path), loaders)


def test_the_provider_caches_one_instance_per_model_and_device(tmp_path: Path) -> None:
    loaded: list[tuple[str, str]] = []
    provider = _provider(tmp_path, loaded)

    gpu = provider.load("uvr-mdxnet-kara-2", "cuda")
    assert provider.load("uvr-mdxnet-kara-2", "cuda") is gpu
    cpu = provider.load("uvr-mdxnet-kara-2", "cpu")
    assert cpu is not gpu
    assert provider.get("uvr-mdxnet-kara-2") is cpu
    assert loaded == [("UVR_MDXNET_KARA_2.onnx", "cuda"), ("UVR_MDXNET_KARA_2.onnx", "cpu")]


def test_release_gpu_drops_only_the_gpu_instances(tmp_path: Path) -> None:
    loaded: list[tuple[str, str]] = []
    provider = _provider(tmp_path, loaded)
    cpu = provider.load("mvsep-mega-53", "cpu")
    provider.load("mvsep-mega-53", "cuda")

    provider.release_gpu()

    assert provider.load("mvsep-mega-53", "cpu") is cpu
    provider.load("mvsep-mega-53", "cuda")
    assert [device for _, device in loaded] == ["cpu", "cuda", "cuda"]


def test_the_registry_needs_are_the_measured_ones() -> None:
    assert {
        "mdx23c-drumsep-6s": GpuNeed("MDX23C DrumSep", 4_400),
        "uvr-mdxnet-kara-2": GpuNeed("UVR MDX-Net KARA 2", 3_600),
        "mvsep-mega-53": GpuNeed("Mega 53-stem", 10_600),
    } == GPU_NEEDS


class _Session:
    """Fake onnxruntime session; ``active`` is the provider list it reports."""

    created: list[list[str]] = []  # noqa: RUF012 - test record
    active: list[str] = ["CUDAExecutionProvider", "CPUExecutionProvider"]  # noqa: RUF012

    def __init__(self, path: str, providers: list[str]) -> None:
        self.created.append(providers)

    def get_inputs(self) -> list[SimpleNamespace]:
        return [SimpleNamespace(name="input")]

    def get_providers(self) -> list[str]:
        return self.active

    def run(self, names: None, feeds: dict[str, np.ndarray]) -> list[np.ndarray]:
        return [feeds["input"]]


@pytest.fixture
def onnx(monkeypatch: pytest.MonkeyPatch) -> list[str]:
    calls: list[str] = []
    _Session.created = []
    _Session.active = ["CUDAExecutionProvider", "CPUExecutionProvider"]
    module = SimpleNamespace(
        InferenceSession=_Session, preload_dlls=lambda: calls.append("preload")
    )
    monkeypatch.setitem(sys.modules, "onnxruntime", module)
    return calls


def test_kara2_on_cuda_preloads_the_cuda_libraries_and_asks_for_the_cuda_provider(
    tmp_path: Path, onnx: list[str]
) -> None:
    model = runtimes.load_kara2(tmp_path / "k.onnx", "cuda")

    assert onnx == ["preload"]
    assert _Session.created == [["CUDAExecutionProvider", "CPUExecutionProvider"]]
    assert isinstance(model, Kara2Model)
    assert model.fallback_reason is None


def test_kara2_without_a_usable_cuda_provider_reports_the_fallback(
    tmp_path: Path, onnx: list[str]
) -> None:
    _Session.active = ["CPUExecutionProvider"]
    model = runtimes.load_kara2(tmp_path / "k.onnx", "cuda")
    assert isinstance(model, Kara2Model)
    assert model.fallback_reason == KARA_REASON == ONNX_NO_CUDA.format(label="UVR MDX-Net KARA 2")


def test_kara2_on_the_cpu_needs_no_preload(tmp_path: Path, onnx: list[str]) -> None:
    model = runtimes.load_kara2(tmp_path / "k.onnx", "cpu")
    assert onnx == []
    assert _Session.created == [["CPUExecutionProvider"]]
    assert isinstance(model, Kara2Model)
    assert model.fallback_reason is None


def _single_step(
    tmp_path: Path, parent: StemName, source: Source, runner: RecordingRunner | None
) -> DeviceUse | None:
    """Plan a band where only ``parent`` is audible and run its single step."""
    silent = tuple(n for n in ("vocals", "drums", "guitar", "piano", "other") if n != parent)
    stems = write_stems(tmp_path / "stems", band(silent=silent))  # type: ignore[arg-type]
    (step,) = DetailPipeline(source, runner).plan(stems, tmp_path / "partial")
    return step.run(lambda f: None)


def _source_with(parent: ParentStemName, load: ModelLoader, table: FoldTable) -> Source:
    return Source({parent: ModelChoice("fake-detail", load, table, PARTITION, NEED)})


def _vocals_step(tmp_path: Path, model: object, runner: RecordingRunner) -> DeviceUse | None:
    return _single_step(tmp_path, "vocals", Source({"vocals": choice(model, VOCAL_TABLE)}), runner)


def test_every_parent_step_runs_through_the_runner_with_its_need(tmp_path: Path) -> None:
    runner = RecordingRunner()
    use = _vocals_step(tmp_path, ScaledModel({"lead": 0.7, "backing": 0.3}), runner)

    assert runner.needs == [NEED]
    assert use == DeviceUse(kind="gpu", torch_device="cuda", name="GPU")


class _NoCudaModel(ScaledModel):
    fallback_reason = KARA_REASON


def test_a_model_that_ran_on_the_cpu_turns_the_step_into_a_fallback(tmp_path: Path) -> None:
    use = _vocals_step(tmp_path, _NoCudaModel({"lead": 0.7, "backing": 0.3}), RecordingRunner())
    assert use == DeviceUse(kind="cpu", torch_device="cpu", fallback_reason=KARA_REASON)


def test_a_failing_gpu_attempt_loads_the_model_again_for_the_cpu(tmp_path: Path) -> None:
    devices: list[str] = []
    cpu_model = ScaledModel({"kick": 0.5, "snare": 0.5})

    class _GpuOom:
        def separate(
            self, audio: DetailAudio, progress: ProgressCallback
        ) -> dict[str, DetailAudio]:
            raise MemoryError("alloc")

    def load(device: str) -> DetailModel:
        devices.append(device)
        return _GpuOom() if device == "cuda" else cpu_model

    runner = RecordingRunner()
    use = _single_step(tmp_path, "drums", _source_with("drums", load, DRUMSEP_TABLE), runner)

    assert devices == ["cuda", "cpu"]
    assert isinstance(runner.errors[0], MemoryError)
    assert use is not None
    assert use.fallback_reason == "retried"
    assert (tmp_path / "partial" / "drums" / "outcome.json").is_file()


def test_a_pipeline_without_a_runner_runs_on_the_cpu_and_records_nothing(tmp_path: Path) -> None:
    devices: list[str] = []
    model = ScaledModel({"lead": 0.7, "backing": 0.3})

    def load(device: str) -> DetailModel:
        devices.append(device)
        return model

    source = _source_with("vocals", load, VOCAL_TABLE)
    assert _single_step(tmp_path, "vocals", source, None) is None
    assert devices == ["cpu"]


def test_the_registry_source_uses_the_model_needs() -> None:
    class _Provider:
        def load(self, model_id: DetailModelId, device: str) -> DetailModel:
            return ScaledModel({})

        def release_gpu(self) -> None: ...

    source = RegistrySource(_Provider())
    needs = [source.choose(p).need for p in ("drums", "vocals", "other")]
    assert needs == [GPU_NEEDS["mdx23c-drumsep-6s"], GPU_NEEDS["uvr-mdxnet-kara-2"],
                     GPU_NEEDS["mvsep-mega-53"]]  # fmt: skip


def test_release_gpu_reaches_the_model_source(tmp_path: Path) -> None:
    source = Source({})
    DetailPipeline(source, RecordingRunner()).release_gpu()
    assert source.released == 1
    FakeSource(0.0).release_gpu()


def test_fake_steps_run_through_the_runner_with_the_fake_need(tmp_path: Path) -> None:
    runner = RecordingRunner()
    settings = WorkerSettings(data_dir=tmp_path, separator="fake", fake_detail_step_seconds=0.0)
    stems = write_stems(tmp_path / "stems", band(silent=("vocals", "guitar", "piano", "other")))
    pipeline = create_detail_pipeline(settings, runner)

    uses = [step.run(lambda f: None) for step in pipeline.plan(stems, tmp_path / "partial")]

    assert runner.needs == [FAKE_DETAIL_NEED]
    assert uses == [DeviceUse(kind="gpu", torch_device="cuda", name="GPU")]


def test_the_real_pipeline_releases_through_its_provider(tmp_path: Path) -> None:
    settings = WorkerSettings(data_dir=tmp_path, model_dir=tmp_path / "m")
    create_detail_pipeline(settings, RecordingRunner()).release_gpu()
    assert not (tmp_path / "m").exists()
