"""Fixtures of the API tests."""

from collections.abc import Iterator
from pathlib import Path

import pytest

from .stack import Stack, open_stack, settings_for


@pytest.fixture
def stack(tmp_path: Path) -> Iterator[Stack]:
    """The fake stack in mode ``ready``."""
    yield from open_stack(settings_for(tmp_path))
