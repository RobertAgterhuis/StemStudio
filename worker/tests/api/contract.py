"""Validates HTTP responses against ``contracts/worker-api.openapi.yaml`` (OpenAPI 3.1)."""

from collections.abc import Mapping
from functools import cache
from pathlib import Path
from typing import Any, Protocol

import yaml
from jsonschema import Draft202012Validator
from referencing import Registry, Resource
from referencing.jsonschema import DRAFT202012

SPEC_PATH = (
    Path(__file__).resolve().parents[3]
    / "specs"
    / "014-practice-flow"
    / "contracts"
    / "worker-api.openapi.yaml"
)
_URI = "urn:stemstudio:worker-api"


class Response(Protocol):
    """The parts of a test-client response the checks need (any httpx flavour)."""

    @property
    def status_code(self) -> int: ...

    @property
    def headers(self) -> Mapping[str, str]: ...

    def json(self) -> Any: ...


@cache
def spec() -> dict[str, Any]:
    """The parsed OpenAPI document."""
    loaded: dict[str, Any] = yaml.safe_load(SPEC_PATH.read_text(encoding="utf-8"))
    return loaded


@cache
def _registry() -> Registry[Any]:
    resource: Resource[Any] = Resource.from_contents(spec(), default_specification=DRAFT202012)
    registry: Registry[Any] = Registry().with_resource(_URI, resource)
    return registry


def _resolve(node: dict[str, Any]) -> dict[str, Any]:
    """Follow a local ``$ref`` (e.g. to ``components/responses``)."""
    ref = node.get("$ref")
    if ref is None:
        return node
    target: Any = spec()
    for part in ref.removeprefix("#/").split("/"):
        target = target[part]
    return _resolve(target)


def _rebase(schema: Any) -> Any:
    """Rewrite local ``#/...`` refs so they resolve against the registered document."""
    if isinstance(schema, dict):
        return {k: (f"{_URI}{v}" if k == "$ref" else _rebase(v)) for k, v in schema.items()}
    if isinstance(schema, list):
        return [_rebase(item) for item in schema]
    return schema


def validate_schema(schema: dict[str, Any], instance: Any) -> None:
    """Validate ``instance`` against an (OpenAPI 3.1 = JSON Schema 2020-12) schema."""
    validator = Draft202012Validator(
        _rebase(schema),
        registry=_registry(),
        format_checker=Draft202012Validator.FORMAT_CHECKER,
    )
    validator.validate(instance)


def assert_conforms(response: Response, path: str, method: str = "get") -> None:
    """Assert the status, media type, required headers and JSON body are declared for ``path``.

    Args:
        response: The response to check.
        path: Path template as written in the contract, e.g. ``/v1/jobs/{jobId}``.
        method: Lower-case HTTP method.
    """
    operation = spec()["paths"][path][method]
    declared = _resolve(operation["responses"][str(response.status_code)])
    media = response.headers["content-type"].split(";")[0].strip()
    content = declared["content"][media]
    for header in declared.get("headers", {}):
        assert header.lower() in response.headers, f"missing header {header}"
    if media.endswith("json"):
        validate_schema(content["schema"], response.json())


def component(name: str) -> dict[str, Any]:
    """A ``$ref`` schema to ``components/schemas/<name>``."""
    return {"$ref": f"#/components/schemas/{name}"}
