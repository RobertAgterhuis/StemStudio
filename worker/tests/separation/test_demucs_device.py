"""Demucs and the fake separator run through the ``DeviceRunner`` (T023, FR-002, FR-004)."""

from pathlib import Path

import pytest

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import GpuNeed, SeparationError
from stemstudio_worker.schemas import JobDevice
from stemstudio_worker.separation import HTDEMUCS_NEED_MIB, create_separator
from stemstudio_worker.separation.demucs_separator import DemucsSeparator
from stemstudio_worker.separation.fake import FAKE_NEED, FakeSeparator

from .fakes import GPU_NAME, RETRY_REASON, FakeRuntime, ScriptedRunner

GPU_RECORD = JobDevice(kind="gpu", name=GPU_NAME)


def _separate(fake: FakeRuntime, runner: ScriptedRunner, mix_wav: Path, out: Path) -> JobDevice:
    separator = DemucsSeparator(("htdemucs_6s", "htdemucs"), fake.as_runtime(), runner)
    device = separator.separate(mix_wav, out, lambda f, s: None).device
    assert device is not None
    return device


def test_the_separation_runs_through_the_runner_with_the_measured_need(
    mix_wav: Path, tmp_path: Path
) -> None:
    fake, runner = FakeRuntime(), ScriptedRunner(first="cuda")

    device = _separate(fake, runner, mix_wav, tmp_path)

    assert HTDEMUCS_NEED_MIB == 1_700
    assert runner.needs == [GpuNeed("htdemucs_6s", 1_700)]
    assert fake.devices == ["cuda"]
    assert device == GPU_RECORD


def test_a_cpu_runner_records_the_cpu(mix_wav: Path, tmp_path: Path) -> None:
    fake = FakeRuntime()
    assert _separate(fake, ScriptedRunner(), mix_wav, tmp_path) == JobDevice(kind="cpu")
    assert fake.devices == ["cpu"]


def test_the_fallback_model_is_named_in_the_need(mix_wav: Path, tmp_path: Path) -> None:
    fake, runner = FakeRuntime(fail_loading={"htdemucs_6s"}), ScriptedRunner()
    _separate(fake, runner, mix_wav, tmp_path)
    assert runner.needs == [GpuNeed("htdemucs", 1_700)]


def test_a_cpu_retry_applies_the_model_again_on_the_cpu(mix_wav: Path, tmp_path: Path) -> None:
    oom = RuntimeError("CUDA out of memory")
    fake = FakeRuntime(apply_error=oom, failing_devices={"cuda"})
    runner = ScriptedRunner(first="cuda")

    device = _separate(fake, runner, mix_wav, tmp_path)

    assert fake.devices == ["cuda", "cpu"]
    assert runner.errors == [oom]  # the runner sees the raw error, not a SeparationError
    assert device == JobDevice(kind="cpu", cpu_fallback=True, fallback_reason=RETRY_REASON)


def test_an_error_of_the_last_attempt_becomes_a_readable_separation_error(
    mix_wav: Path, tmp_path: Path
) -> None:
    fake = FakeRuntime(apply_error=RuntimeError("CUDA out of memory"))

    with pytest.raises(SeparationError, match=r"^Stem separation failed\.$"):
        _separate(fake, ScriptedRunner(first="cuda"), mix_wav, tmp_path)
    assert fake.devices == ["cuda", "cpu"]


def test_the_fake_separator_reports_the_runner_device(mix_wav: Path, tmp_path: Path) -> None:
    runner = ScriptedRunner(first="cuda")

    result = FakeSeparator(runner).separate(mix_wav, tmp_path, lambda f, s: None)

    assert runner.needs == [FAKE_NEED]
    assert result.device == GPU_RECORD


def test_the_fake_separator_without_a_runner_reports_no_device(
    mix_wav: Path, tmp_path: Path
) -> None:
    assert FakeSeparator().separate(mix_wav, tmp_path, lambda f, s: None).device is None


def test_the_factory_hands_the_runner_to_both_separators(mix_wav: Path, tmp_path: Path) -> None:
    runner = ScriptedRunner(first="cuda")
    fake = create_separator(WorkerSettings(data_dir=tmp_path, separator="fake"), runner)

    assert fake.separate(mix_wav, tmp_path, lambda f, s: None).device == GPU_RECORD
    demucs = create_separator(WorkerSettings(data_dir=tmp_path), runner)
    assert isinstance(demucs, DemucsSeparator)
