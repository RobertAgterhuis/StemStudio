"""Shared access to the generator contract documents of the skeleton tests (009/010 Phase 0)."""

import copy
from functools import cache
from pathlib import Path
from typing import Any

import yaml
from jsonschema import Draft202012Validator

SPECS = Path(__file__).resolve().parents[3] / "specs"


@cache
def _load(feature: str) -> dict[str, Any]:
    path = SPECS / feature / "contracts" / "generator-api.openapi.yaml"
    loaded: dict[str, Any] = yaml.safe_load(path.read_text(encoding="utf-8"))
    return loaded


class GeneratorDocument:
    """One feature's ``generator-api.openapi.yaml``, parsed once."""

    def __init__(self, feature: str) -> None:
        self.feature = feature

    @property
    def root(self) -> dict[str, Any]:
        """The parsed document (shared; do not mutate)."""
        return _load(self.feature)

    @property
    def examples(self) -> dict[str, Any]:
        """``components/examples``."""
        found: dict[str, Any] = self.root["components"]["examples"]
        return found

    def example(self, name: str) -> Any:
        """A deep copy of ``components/examples/<name>/value``."""
        return copy.deepcopy(self.examples[name]["value"])

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

    def problem_examples(self) -> list[Any]:
        """Every problem example of ``components/responses`` (single and named examples)."""
        found: list[Any] = []
        for response in self.section("responses").values():
            media = response.get("content", {}).get("application/problem+json", {})
            if "example" in media:
                found.append(copy.deepcopy(media["example"]))
            found.extend(copy.deepcopy(e["value"]) for e in media.get("examples", {}).values())
        return found


V100 = GeneratorDocument("009-style-transfer")
V110 = GeneratorDocument("010-lyrics-to-song")


def changed_entries(older: GeneratorDocument, newer: GeneratorDocument, section: str) -> set[str]:
    """Keys of ``older``'s section whose value differs (or is missing) in ``newer``."""
    later = newer.section(section)
    return {key for key, value in older.section(section).items() if later.get(key) != value}
