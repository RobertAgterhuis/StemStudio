"""The uvicorn factory named by the image CMD exists and takes no arguments (qa integration)."""

import importlib
import inspect
import re
from pathlib import Path

DOCKERFILE = Path(__file__).resolve().parents[2] / "docker" / "generator.Dockerfile"


def test_the_image_cmd_names_a_zero_argument_app_factory() -> None:
    """``uvicorn --factory module:name`` calls ``name()``; required parameters crash the image."""
    match = re.search(r'"--factory", "([\w.]+):(\w+)"', DOCKERFILE.read_text(encoding="utf-8"))
    assert match is not None
    factory = getattr(importlib.import_module(match.group(1)), match.group(2))
    parameters = inspect.signature(factory).parameters.values()
    required = [p for p in parameters if p.default is inspect.Parameter.empty]
    assert required == []
