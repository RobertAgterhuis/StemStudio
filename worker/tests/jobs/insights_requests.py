"""Quality and handout requests taken from the worker contract examples (consumer-driven)."""

from functools import cache
from pathlib import Path
from typing import Any

import yaml

from stemstudio_worker.schemas import HandoutJobRequest, QualityJobRequest

SPEC = (Path(__file__).resolve().parents[3] / "specs" / "020-insights-handouts" / "contracts"
        / "worker-api.openapi.yaml")  # fmt: skip


@cache
def example(name: str) -> Any:
    """The ``value`` of ``components/examples/<name>`` of the worker contract 1.11.0."""
    document = yaml.safe_load(SPEC.read_text(encoding="utf-8"))
    return document["components"]["examples"][name]["value"]


def quality_request() -> QualityJobRequest:
    """The ``QualityRequest`` example (six standard stems, a drums split)."""
    return QualityJobRequest.model_validate(example("QualityRequest"))


def handout_request() -> HandoutJobRequest:
    """The ``BassHandoutRequest`` example (one song, artwork, two notation PDFs)."""
    return HandoutJobRequest.model_validate(example("BassHandoutRequest"))
