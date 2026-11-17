"""Tests for scripts/api_image_size_checks.py (023 T069)."""

import subprocess
from collections.abc import Sequence

import pytest

import api_image_size_checks as sizes

_MB = 1_000_000


def _docker(*, dictionaries: int, images: dict[str, int]) -> sizes.Docker:
    def docker(arguments: Sequence[str]) -> str:
        if arguments[0] == "run":
            assert list(arguments[-2:]) == ["-sb", sizes.DICTIONARIES_DIR]
            return f"{dictionaries}\t{sizes.DICTIONARIES_DIR}\n"
        return f"{images[arguments[-1]]}\n"

    return docker


def test_du_output_is_parsed() -> None:
    assert sizes.parse_du("7745000\t/app/LyricAssist/Dictionaries\n") == 7_745_000


def test_dictionaries_below_the_budget_pass() -> None:
    check = sizes.check_dictionaries(7_700_000, sizes.BUDGET_BYTES)

    assert check.ok
    assert check.line() == (
        "ok   vendored dictionaries: 7.7 MB in /app/LyricAssist/Dictionaries (budget 15.0 MB)"
    )


def test_dictionaries_at_the_budget_fail() -> None:
    check = sizes.check_dictionaries(sizes.BUDGET_BYTES, sizes.BUDGET_BYTES)

    assert not check.ok
    assert check.line().startswith("FAIL vendored dictionaries")


@pytest.mark.parametrize(("growth", "expected"), [(7 * _MB, True), (15 * _MB, False)])
def test_growth_against_the_baseline(growth: int, expected: bool) -> None:
    check = sizes.check_growth(440 * _MB + growth, 440 * _MB, sizes.BUDGET_BYTES)

    assert check.ok is expected
    assert f"{growth / _MB:+.1f} MB" in check.message


def test_without_a_baseline_only_the_dictionaries_are_checked() -> None:
    docker = _docker(dictionaries=7 * _MB, images={})

    checks = sizes.run_checks("api:x", None, sizes.BUDGET_BYTES, docker)

    assert [c.name for c in checks] == ["vendored dictionaries"]


def test_with_a_baseline_the_growth_is_checked_too() -> None:
    docker = _docker(dictionaries=7 * _MB, images={"api:x": 450 * _MB, "api:base": 440 * _MB})

    checks = sizes.run_checks("api:x", "api:base", sizes.BUDGET_BYTES, docker)

    assert [c.ok for c in checks] == [True, True]
    assert checks[1].message.startswith("450.0 MB against baseline 440.0 MB: +10.0 MB")


def test_main_exit_codes(capsys: pytest.CaptureFixture[str]) -> None:
    small = _docker(dictionaries=7 * _MB, images={})
    large = _docker(dictionaries=16 * _MB, images={})

    assert sizes.main(["--image", "api:x"], small) == 0
    assert sizes.main(["--image", "api:x"], large) == 1
    assert "FAIL vendored dictionaries" in capsys.readouterr().out


def test_budget_can_be_overridden() -> None:
    docker = _docker(dictionaries=7 * _MB, images={})

    assert sizes.main(["--image", "api:x", "--budget-bytes", str(5 * _MB)], docker) == 1


def test_docker_is_run_with_its_arguments(monkeypatch: pytest.MonkeyPatch) -> None:
    seen: list[list[str]] = []

    class Done:
        stdout = "out"

    def fake_run(command: list[str], **_kwargs: object) -> Done:
        seen.append(command)
        return Done()

    monkeypatch.setattr(subprocess, "run", fake_run)

    assert sizes.run_docker(["image", "inspect", "x"]) == "out"
    assert seen == [["docker", "image", "inspect", "x"]]


def test_a_failing_docker_command_is_one_fail_line(capsys: pytest.CaptureFixture[str]) -> None:
    def docker(arguments: Sequence[str]) -> str:
        raise subprocess.CalledProcessError(125, ["docker", *arguments], stderr="no such image")

    assert sizes.main(["--image", "api:missing"], docker) == 1
    assert "FAIL docker: docker run --rm" in capsys.readouterr().out
