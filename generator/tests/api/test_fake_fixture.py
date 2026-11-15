"""The fake stack behaves as ``tests/fixtures/generative/fake-generator.json`` says (qa, E2E)."""

import json
import re
from functools import cache
from pathlib import Path
from typing import Any, get_args

import pytest

from stemstudio_generator.config import FakeGeneratorMode
from stemstudio_generator.engines.fake_pitch import FakePitchTracker
from stemstudio_generator.ports import voiced_ratio

from .stack import Stack, multipart, open_stack, restyle_params, settings_for, stem

FIXTURES = Path(__file__).resolve().parents[3] / "tests" / "fixtures"


@cache
def _table() -> dict[str, Any]:
    loaded: dict[str, Any] = json.loads(
        (FIXTURES / "generative" / "fake-generator.json").read_text("utf-8")
    )
    return loaded


def _restyle_modes() -> list[str]:
    return [mode for mode, spec in _table()["modes"].items() if "restyle" in spec]


def _run_restyle(stack: Stack, tmp_path: Path) -> dict[str, Any]:
    files = multipart(restyle_params(), [stem(tmp_path, "a.wav")])
    created = stack.client.post("/v1/generations", files=files)
    assert created.status_code == 202, created.text
    stack.run_all()
    return stack.get(created.json()["generationId"])


def test_every_mode_of_the_table_is_a_configurable_mode() -> None:
    assert set(_table()["modes"]) == set(get_args(FakeGeneratorMode))


@pytest.mark.parametrize("mode", _restyle_modes())
def test_restyle_ends_as_the_table_says(tmp_path: Path, mode: str) -> None:
    expected = _table()["modes"][mode]["restyle"]
    for stack in open_stack(settings_for(tmp_path, mode)):  # type: ignore[arg-type]
        job = _run_restyle(stack, tmp_path)

        assert job["status"] == expected["terminal"]
        if "errorCode" in expected:
            assert job["error"]["code"] == expected["errorCode"]
            assert re.search(expected["messagePattern"], job["error"]["message"])


def test_no_gpu_mode_is_unavailable_with_a_gpu_reason(tmp_path: Path) -> None:
    pattern = _table()["modes"]["no-gpu"]["reasonPattern"]
    for stack in open_stack(settings_for(tmp_path, "no-gpu")):
        health = stack.client.get("/health").json()

        assert health["ready"] is False
        assert re.search(pattern, health["reason"])


def test_take_files_carry_the_take_index_tag(stack: Stack, tmp_path: Path) -> None:
    job = _run_restyle(stack, tmp_path)

    assert [label.take_index for label in stack.writer.labels] == [1]
    assert job["takes"][0]["sampleRate"] == _table()["takeFile"]["sampleRate"]


@pytest.mark.parametrize(
    ("name", "voiced"), [("own-voice-20s.wav", True), ("unvoiced-20s.wav", False)]
)
def test_fake_pitch_tracker_hears_the_own_voice_fixture(name: str, voiced: bool) -> None:
    path = FIXTURES / "audio" / name
    if not path.is_file():
        pytest.skip("run tests/fixtures/generate_voice_fixtures.py to create the voice fixtures")

    ratio = voiced_ratio(FakePitchTracker().f0(path))

    assert (ratio >= 0.5) is voiced
