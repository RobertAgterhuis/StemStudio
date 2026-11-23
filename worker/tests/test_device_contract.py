"""Skeleton tests for the GPU contract code (feature 003 additions of worker API 1.2.0)."""

from collections.abc import Callable
from pathlib import Path
from typing import Any, get_args

import pytest
import yaml
from jsonschema import Draft202012Validator
from pydantic import ValidationError

from stemstudio_worker.config import FakeDeviceKind, WorkerSettings, load_settings
from stemstudio_worker.ports import DeviceRunner, DeviceUse, GpuNeed, SeparationResult, merge_device
from stemstudio_worker.schemas import (
    CONTRACT_VERSION,
    CPU_DEVICE,
    DetailJob,
    DeviceInfo,
    Health,
    Job,
    JobDevice,
    ProcessingDeviceKind,
)

_SPEC = (
    Path(__file__).resolve().parents[2]
    / "specs"
    / "020-insights-handouts"
    / "contracts"
    / "worker-api.openapi.yaml"
)
_GPU = "NVIDIA GeForce RTX 5070 Ti"


def _spec() -> dict[str, Any]:
    loaded: dict[str, Any] = yaml.safe_load(_SPEC.read_text(encoding="utf-8"))
    return loaded


def _validate(schema_name: str, instance: object) -> list[str]:
    spec = _spec()
    schema = {**spec["components"]["schemas"][schema_name], "components": spec["components"]}
    validator = Draft202012Validator(schema)
    return [error.message for error in validator.iter_errors(instance)]


def _health_examples() -> dict[str, Any]:
    response = _spec()["paths"]["/health"]["get"]["responses"]["200"]
    examples: dict[str, Any] = response["content"]["application/json"]["examples"]
    return examples


def test_contract_version_is_the_current_document() -> None:
    assert CONTRACT_VERSION == _spec()["info"]["version"] == "1.11.0"


def test_device_kind_matches_the_contract() -> None:
    enum = _spec()["components"]["schemas"]["ProcessingDeviceKind"]["enum"]
    assert list(get_args(ProcessingDeviceKind)) == enum


@pytest.mark.parametrize("name", ["cpu", "gpu", "gpuUnavailable"])
def test_health_examples_round_trip(name: str) -> None:
    example = _health_examples()[name]["value"]

    health = Health.model_validate(example)

    assert health.model_dump(mode="json", by_alias=True) == example
    assert _validate("Health", example) == []


def test_default_health_reports_the_cpu_device() -> None:
    body = Health(separator="fake", device="cpu").model_dump(mode="json", by_alias=True)

    assert body["deviceInfo"] == CPU_DEVICE.model_dump(mode="json", by_alias=True)
    assert body["deviceInfo"]["kind"] == "cpu"
    assert _validate("Health", body) == []


@pytest.mark.parametrize(("example", "model"), [("SeparatingJob", Job), ("CompletedJob", Job)])
def test_job_examples_carry_a_device(example: str, model: type[Job]) -> None:
    value = _spec()["components"]["examples"][example]["value"]

    job = model.model_validate(value)

    assert job.device is not None
    assert job.model_dump(mode="json", by_alias=True) == value


def test_detail_job_example_carries_a_fallback() -> None:
    value = _spec()["components"]["examples"]["RunningDetailJob"]["value"]

    job = DetailJob.model_validate(value)

    assert job.device == JobDevice(
        kind="gpu", name=_GPU, cpu_fallback=True, fallback_reason=value["device"]["fallbackReason"]
    )


def test_jobs_stored_before_1_2_0_load_without_a_device() -> None:
    value = dict(_spec()["components"]["examples"]["QueuedJob"]["value"])
    value.pop("device", None)

    assert Job.model_validate(value).device is None


def test_device_info_uses_mib_aliases_and_validates_capability() -> None:
    info = DeviceInfo(kind="gpu", name=_GPU, compute_capability="12.0", total_memory_mib=16303)

    assert info.model_dump(by_alias=True)["totalMemoryMiB"] == 16303
    with pytest.raises(ValidationError):
        DeviceInfo(kind="gpu", compute_capability="sm_120")


def _use(kind: ProcessingDeviceKind, reason: str | None = None) -> DeviceUse:
    gpu = kind == "gpu"
    return DeviceUse(kind, "cuda" if gpu else "cpu", _GPU if gpu else None, reason)


@pytest.mark.parametrize(
    ("uses", "expected"),
    [
        ([_use("cpu")], JobDevice(kind="cpu")),
        ([_use("gpu")], JobDevice(kind="gpu", name=_GPU)),
        ([_use("gpu"), _use("cpu", "oom")], JobDevice(kind="gpu", name=_GPU, cpu_fallback=True,
                                                      fallback_reason="oom")),
        ([_use("cpu", "low"), _use("gpu")], JobDevice(kind="gpu", name=_GPU, cpu_fallback=True,
                                                      fallback_reason="low")),
        ([_use("cpu", "a"), _use("cpu", "b")], JobDevice(kind="cpu", cpu_fallback=True,
                                                         fallback_reason="b")),
    ],
)  # fmt: skip
def test_merge_device_folds_steps(uses: list[DeviceUse], expected: JobDevice) -> None:
    record: JobDevice | None = None
    for use in uses:
        record = merge_device(record, use)

    assert record == expected


class _CpuRunner:
    """Minimal structural implementation of ``DeviceRunner``."""

    @property
    def info(self) -> DeviceInfo:
        return CPU_DEVICE

    def run[T](self, work: Callable[[str], T], need: GpuNeed) -> tuple[T, DeviceUse]:
        return work("cpu"), DeviceUse("cpu", "cpu")


def test_device_runner_protocol_is_structural() -> None:
    runner = _CpuRunner()

    value, use = runner.run(lambda device: device.upper(), GpuNeed("htdemucs_6s", 1500))

    assert isinstance(runner, DeviceRunner)
    assert (value, use.kind, runner.info.kind) == ("CPU", "cpu", "cpu")


def test_separation_result_device_defaults_to_none() -> None:
    assert SeparationResult(model="fake", stems={}).device is None


def test_fake_device_setting() -> None:
    assert WorkerSettings(data_dir=Path("d")).fake_device == "cpu"
    assert load_settings({"STEMSTUDIO_FAKE_DEVICE": "GPU-OOM"}).fake_device == "gpu-oom"
    assert list(get_args(FakeDeviceKind)) == ["cpu", "gpu", "gpu-oom"]
    with pytest.raises(ValueError, match="STEMSTUDIO_FAKE_DEVICE"):
        load_settings({"STEMSTUDIO_FAKE_DEVICE": "tpu"})
