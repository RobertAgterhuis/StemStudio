"""Shared access to the worker contract documents of the skeleton contract tests (006/008)."""

import copy
from functools import cache
from pathlib import Path
from typing import Any

import yaml
from jsonschema import Draft202012Validator
from pydantic import BaseModel

SPECS = Path(__file__).resolve().parents[3] / "specs"


@cache
def _load(feature: str) -> dict[str, Any]:
    path = SPECS / feature / "contracts" / "worker-api.openapi.yaml"
    loaded: dict[str, Any] = yaml.safe_load(path.read_text(encoding="utf-8"))
    return loaded


class WorkerDocument:
    """One feature's ``worker-api.openapi.yaml``, parsed once."""

    def __init__(self, feature: str) -> None:
        self.feature = feature

    @property
    def root(self) -> dict[str, Any]:
        """The parsed document (shared; do not mutate)."""
        return _load(self.feature)

    @property
    def schemas(self) -> dict[str, Any]:
        """``components/schemas``."""
        found: dict[str, Any] = self.root["components"]["schemas"]
        return found

    def example(self, name: str) -> Any:
        """A deep copy of ``components/examples/<name>/value``."""
        return copy.deepcopy(self.root["components"]["examples"][name]["value"])

    def health_examples(self) -> dict[str, Any]:
        """The ``GET /health`` examples by name (deep copies)."""
        content = self.root["paths"]["/health"]["get"]["responses"]["200"]["content"]
        return {
            k: copy.deepcopy(v["value"]) for k, v in content["application/json"]["examples"].items()
        }

    def errors(self, schema: str, instance: object) -> list[str]:
        """Validation messages of ``instance`` against ``components/schemas/<schema>``."""
        components = self.root["components"]
        validator = Draft202012Validator(
            {**components["schemas"][schema], "components": components}
        )
        return [error.message for error in validator.iter_errors(instance)]

    def section(self, name: str) -> dict[str, Any]:
        """``paths`` or a ``components`` section."""
        holder = self.root if name == "paths" else self.root["components"]
        found: dict[str, Any] = holder.get(name, {})
        return found

    def assert_round_trip(self, example: str, model: type[BaseModel]) -> None:
        """The example parses into ``model``, dumps back unchanged and validates."""
        dumped = model.model_validate(self.example(example)).model_dump(mode="json", by_alias=True)

        assert dumped == self.example(example)
        assert self.errors(model.__name__, dumped) == []


def changed_entries(older: WorkerDocument, newer: WorkerDocument, section: str) -> set[str]:
    """Keys of ``older``'s section whose value differs (or is missing) in ``newer``."""
    later = newer.section(section)
    return {key for key, value in older.section(section).items() if later.get(key) != value}
