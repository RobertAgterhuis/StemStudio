"""Tests of the fontconfig check for the Leipzig music font (SC-004); fc-list is patched."""

import subprocess
from collections.abc import Sequence
from typing import Any

import pytest

from stemstudio_worker.engraving import fonts

FC_LIST = "/usr/bin/fc-list"


class FakeRun:
    """Records the ``subprocess.run`` call and returns a fixed result (or raises)."""

    def __init__(self, stdout: str = "", returncode: int = 0, error: Exception | None = None):
        self.stdout = stdout
        self.returncode = returncode
        self.error = error
        self.calls: list[tuple[Sequence[str], dict[str, Any]]] = []

    def __call__(self, args: Sequence[str], **kwargs: Any) -> subprocess.CompletedProcess[str]:
        self.calls.append((args, kwargs))
        if self.error is not None:
            raise self.error
        return subprocess.CompletedProcess(args, self.returncode, self.stdout, "")


@pytest.fixture
def fc_list(monkeypatch: pytest.MonkeyPatch) -> FakeRun:
    run = FakeRun()
    monkeypatch.setattr(fonts.shutil, "which", lambda name: FC_LIST if name == "fc-list" else None)
    monkeypatch.setattr(fonts.subprocess, "run", run)
    return run


def test_lists_the_leipzig_family_with_an_argument_list_and_no_shell(fc_list: FakeRun) -> None:
    fc_list.stdout = "Leipzig\n"

    assert fonts.fontconfig_families() == frozenset({"Leipzig"})
    args, kwargs = fc_list.calls[0]
    assert args == [FC_LIST, ":family=Leipzig", "family"]
    assert kwargs["timeout"] == fonts.FC_LIST_TIMEOUT_SECONDS
    assert kwargs["check"] is False
    assert kwargs["stdin"] == subprocess.DEVNULL
    assert "shell" not in kwargs


def test_splits_comma_separated_family_names_per_line(fc_list: FakeRun) -> None:
    fc_list.stdout = "Leipzig Medium, Leipzig\n\nOther\n"

    assert fonts.fontconfig_families() == frozenset({"Leipzig Medium", "Leipzig", "Other"})


def test_a_failing_fc_list_lists_nothing(fc_list: FakeRun) -> None:
    fc_list.stdout = "Leipzig\n"
    fc_list.returncode = 1

    assert fonts.fontconfig_families() == frozenset()


@pytest.mark.parametrize(
    "error", [OSError("exec format error"), subprocess.TimeoutExpired("fc-list", 10)]
)
def test_an_fc_list_that_cannot_run_lists_nothing(fc_list: FakeRun, error: Exception) -> None:
    fc_list.error = error

    assert fonts.fontconfig_families() == frozenset()


def test_without_fontconfig_nothing_is_listed_and_nothing_runs(
    monkeypatch: pytest.MonkeyPatch, fc_list: FakeRun
) -> None:
    monkeypatch.setattr(fonts.shutil, "which", lambda name: None)

    assert fonts.fontconfig_families() == frozenset()
    assert fc_list.calls == []


@pytest.mark.parametrize(
    ("families", "expected"),
    [
        (frozenset({"Leipzig"}), True),
        (frozenset({"DejaVu Sans", "Leipzig"}), True),
        (frozenset({"Leipzig Medium"}), False),
        (frozenset(), False),
    ],
)
def test_leipzig_is_available_only_when_fontconfig_lists_the_exact_family(
    families: frozenset[str], expected: bool
) -> None:
    assert fonts.leipzig_available(lambda: families) is expected


def test_leipzig_available_asks_fontconfig_by_default(fc_list: FakeRun) -> None:
    fc_list.stdout = "Leipzig\n"

    assert fonts.leipzig_available() is True
    assert fonts.FONT_FAMILY == "Leipzig"
