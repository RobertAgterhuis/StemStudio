"""Shared helpers of the analyzer tests: input kinds and result lookup."""

import importlib.util
from pathlib import Path
from typing import Any, cast

from stemstudio_worker.schemas import QualityInputKind, QualityResult, StemQuality


def kind_of(channel: str) -> QualityInputKind:
    """``standard`` = no dot, ``rest`` = ``<parent>.rest``, else ``detail`` (data-model 2.3)."""
    if "." not in channel:
        return "standard"
    return cast("QualityInputKind", "rest" if channel.endswith(".rest") else "detail")


def stem_of(result: QualityResult, channel: str) -> StemQuality:
    """The estimate of one channel."""
    return next(stem for stem in result.stems if stem.channel == channel)


def conforms(schema: str, payload: Any) -> None:
    """Validate ``payload`` against ``components/schemas/<schema>`` of the 020 worker contract.

    Reuses the OpenAPI validator of ``tests/api/contract.py`` (loaded by path: the test folders
    are not one package).
    """
    location = Path(__file__).resolve().parents[1] / "api" / "contract.py"
    spec = importlib.util.spec_from_file_location("insights_contract_helper", location)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.validate_schema(module.component(schema), payload)
