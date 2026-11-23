"""Skeleton tests for the detail-separation contract code (worker API 1.1.0+, ports, settings)."""

from datetime import UTC, datetime
from pathlib import Path
from typing import Any, get_args

import numpy as np
import pytest
import yaml
from pydantic import ValidationError

from stemstudio_worker.config import load_settings
from stemstudio_worker.detail import create_detail_adapters
from stemstudio_worker.ports import (
    DetailAdapters,
    DetailCancelledError,
    DetailModel,
    DetailModelProvider,
    DetailModelUnavailableError,
    DetailSeparationError,
    ProgressCallback,
    WorkerError,
)
from stemstudio_worker.schemas import (
    CONTRACT_VERSION,
    CreateDetailJobRequest,
    DetailJob,
    DetailJobState,
    DetailModelId,
    DetailResult,
    DetailStemName,
    ParentOutcomeKind,
    ParentStemName,
)

_SPEC = (
    Path(__file__).resolve().parents[2]
    / "specs"
    / "020-insights-handouts"
    / "contracts"
    / "worker-api.openapi.yaml"
)


def _spec() -> dict[str, Any]:
    loaded: dict[str, Any] = yaml.safe_load(_SPEC.read_text(encoding="utf-8"))
    return loaded


def _schema_enum(name: str) -> list[Any]:
    values: list[Any] = _spec()["components"]["schemas"][name]["enum"]
    return values


def _example(name: str) -> Any:
    return _spec()["components"]["examples"][name]["value"]


def test_contract_version_matches_the_binding_document() -> None:
    assert CONTRACT_VERSION == _spec()["info"]["version"] == "1.11.0"


@pytest.mark.parametrize(
    ("literal", "schema"),
    [
        (ParentStemName, "ParentStemName"),
        (DetailStemName, "DetailStemName"),
    ],
)
def test_name_literals_match_the_contract(literal: Any, schema: str) -> None:
    assert list(get_args(literal)) == _schema_enum(schema)


def test_model_ids_match_the_contract() -> None:
    contract = [v for v in _spec()["components"]["schemas"]["ParentOutcome"]["properties"]
                ["model"]["enum"] if v is not None]  # fmt: skip
    assert list(get_args(DetailModelId)) == contract


def test_enums_match_the_contract() -> None:
    assert [s.value for s in DetailJobState] == _schema_enum("DetailJobState")
    assert [s.value for s in ParentOutcomeKind] == _schema_enum("ParentOutcomeKind")


def test_terminal_detail_states() -> None:
    terminal = {s for s in DetailJobState if s.is_terminal}

    assert terminal == {DetailJobState.COMPLETED, DetailJobState.FAILED, DetailJobState.CANCELLED}


@pytest.mark.parametrize(
    "name", ["QueuedDetailJob", "RunningDetailJob", "FailedDetailJob", "CompletedDetailJob"]
)
def test_detail_job_examples_round_trip(name: str) -> None:
    example = _example(name)

    job = DetailJob.model_validate(example)

    assert job.model_dump(mode="json", by_alias=True) == example


def test_detail_result_example_round_trips() -> None:
    example = _example("DetailResultExample")

    result = DetailResult.model_validate(example)

    assert result.model_dump(mode="json", by_alias=True) == example
    assert result.parents[1].stems[-1].name == "rest"


def test_create_request_requires_a_job_id() -> None:
    assert CreateDetailJobRequest(standard_job_id="a" * 32).standard_job_id == "a" * 32
    with pytest.raises(ValidationError):
        CreateDetailJobRequest.model_validate({"standardJobId": "nope"})


def test_detail_job_rejects_unknown_fields() -> None:
    now = datetime(2026, 11, 15, tzinfo=UTC).isoformat()
    body = {
        "detailJobId": "a" * 32, "standardJobId": "b" * 32, "audioSha256": "c" * 64,
        "status": "queued", "stage": "Queued", "progress": 0, "createdAt": now,
        "updatedAt": now, "extra": 1,
    }  # fmt: skip
    with pytest.raises(ValidationError):
        DetailJob.model_validate(body)


def test_detail_errors_are_worker_errors() -> None:
    for error in (DetailModelUnavailableError, DetailSeparationError, DetailCancelledError):
        assert issubclass(error, WorkerError)


class _FakeModel:
    def separate(
        self, audio: np.ndarray[Any, Any], progress: ProgressCallback
    ) -> dict[str, np.ndarray[Any, Any]]:
        progress(1.0, "Splitting")
        return {"kick": audio * 0.5}


class _FakeProvider:
    def get(self, model_id: DetailModelId) -> DetailModel:
        return _FakeModel()


def test_detail_adapters_compose_fakes() -> None:
    adapters = DetailAdapters(models=_FakeProvider())
    audio = np.ones((2, 4), dtype=np.float32)

    out = adapters.models.get("mdx23c-drumsep-6s").separate(audio, lambda f, s: None)

    assert isinstance(adapters.models, DetailModelProvider)
    assert np.allclose(out["kick"], 0.5)


def test_detail_settings_defaults() -> None:
    settings = load_settings({})

    assert settings.model_dir == Path("data/models")
    assert settings.fake_detail_step_seconds == 1.0


def test_detail_settings_overrides() -> None:
    env = {"STEMSTUDIO_MODEL_DIR": "/models", "STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS": "2.5"}

    settings = load_settings(env)

    assert settings.model_dir == Path("/models")
    assert settings.fake_detail_step_seconds == 2.5


@pytest.mark.parametrize("value", ["-1", "abc"])
def test_invalid_fake_step_seconds_raise(value: str) -> None:
    with pytest.raises(ValueError, match="STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS"):
        load_settings({"STEMSTUDIO_FAKE_DETAIL_STEP_SECONDS": value})


def test_detail_factory_returns_a_model_provider() -> None:
    adapters = create_detail_adapters(load_settings({}))
    assert isinstance(adapters, DetailAdapters)
    assert isinstance(adapters.models, DetailModelProvider)
