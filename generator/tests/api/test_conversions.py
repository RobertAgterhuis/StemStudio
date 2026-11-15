"""``kind: voiceConversion`` on the fake singer (010 T041 job part, FR-008, FR-011, FR-015)."""

import json
from collections.abc import Iterator
from pathlib import Path
from typing import Any

import pytest
import soundfile as sf

from stemstudio_generator.schemas import UNVOICED_TARGET_TEXT

from ..contract.docs import V110
from ..signals import noise, stereo, tone, write
from .stack import Stack, open_stack, settings_for

PARAMS = {
    "kind": "voiceConversion",
    "voiceConversion": {"pitchShift": 0, "autoShift": True, "steps": 32, "cfg": 3.0,
                        "vocalGainDb": 0, "profileName": "My\tvoice"},
}  # fmt: skip


@pytest.fixture
def singer(tmp_path: Path) -> Iterator[Stack]:
    yield from open_stack(settings_for(tmp_path, kinds=("voiceConversion",)))


def _part(name: str, path: Path) -> tuple[str, tuple[str, bytes, str]]:
    return (name, (path.name, path.read_bytes(), "audio/flac"))


def _files(tmp_path: Path, target: Path | None = None, accompaniment: int = 2) -> list[Any]:
    target = target or write(tmp_path / "vocals.flac", stereo(tone(12.0, 44_100, 220.0)), 44_100)
    prompt = write(tmp_path / "prompt.flac", tone(20.0, 48_000, 330.0), 48_000)
    parts = [("params", ("params.json", json.dumps(PARAMS).encode(), "application/json"))]
    parts += [_part("target", target), _part("prompt", prompt)]
    for number in range(accompaniment):
        stem = write(tmp_path / f"acc{number}.flac", stereo(tone(12.0, 44_100, 110.0, 0.1)), 44_100)
        parts.append(_part("accompaniment", stem))
    return parts


def _create(stack: Stack, files: list[Any]) -> Any:
    return stack.client.post("/v1/generations", files=files)


def test_a_conversion_completes_with_a_converted_take_and_a_vocal_stem(
    singer: Stack, tmp_path: Path
) -> None:
    created = _create(singer, _files(tmp_path))
    assert created.status_code == 202, created.text
    assert V110.errors("Generation", created.json()) == []
    generation_id = created.json()["generationId"]

    singer.run_all()

    job = singer.get(generation_id)
    assert job["status"] == "completed"
    [take] = job["takes"]
    assert (take["index"], take["seed"], take["vocal"], take["hasVocalStem"]) == (
        1,
        0,
        "converted",
        True,
    )
    assert take["model"]["id"] == "soulx-singer-svc"
    assert take["durationSeconds"] == pytest.approx(12.0, abs=0.01)
    assert [(label.voice, label.voice_profile) for label in singer.writer.labels] == [
        ("converted", "My\tvoice"), ("converted", "My\tvoice"),
    ]  # fmt: skip
    assert singer.writer.labels[0].generator.startswith("SoulX-Singer-SVC (")


def test_the_vocal_stem_is_served_with_ranges(singer: Stack, tmp_path: Path) -> None:
    generation_id = _create(singer, _files(tmp_path)).json()["generationId"]
    singer.run_all()
    url = f"/v1/generations/{generation_id}/takes/1/vocal"

    whole = singer.client.get(url)
    part = singer.client.get(url, headers={"range": "bytes=0-3"})

    assert (whole.status_code, whole.headers["content-type"]) == (200, "audio/flac")
    assert (part.status_code, part.content) == (206, whole.content[:4])
    vocal = tmp_path / "vocal.flac"
    vocal.write_bytes(whole.content)
    assert sf.info(vocal).samplerate == 48_000


def test_prompt_and_target_copies_are_deleted_when_the_job_ends(
    singer: Stack, tmp_path: Path
) -> None:
    generation_id = _create(singer, _files(tmp_path)).json()["generationId"]

    singer.run_all()

    root = singer.settings.data_dir / "jobs" / generation_id
    assert sorted(path.name for path in root.iterdir()) == ["takes"]


def test_an_unvoiced_target_fails_with_unvoiced_target(singer: Stack, tmp_path: Path) -> None:
    target = write(tmp_path / "noise.flac", stereo(noise(12.0, 44_100)), 44_100)
    generation_id = _create(singer, _files(tmp_path, target)).json()["generationId"]

    singer.run_all()

    assert singer.get(generation_id)["error"] == {
        "code": "unvoicedTarget", "message": UNVOICED_TARGET_TEXT,
    }  # fmt: skip


def test_a_conversion_without_accompaniment_is_the_vocal_alone(
    singer: Stack, tmp_path: Path
) -> None:
    generation_id = _create(singer, _files(tmp_path, accompaniment=0)).json()["generationId"]

    singer.run_all()

    assert singer.get(generation_id)["status"] == "completed"


@pytest.mark.parametrize(
    ("drop", "detail"),
    [("target", "one target"), ("prompt", "one prompt")],
)
def test_target_and_prompt_are_required(
    singer: Stack, tmp_path: Path, drop: str, detail: str
) -> None:
    files = [part for part in _files(tmp_path) if part[0] != drop]

    response = _create(singer, files)

    assert response.status_code == 400
    assert detail in response.json()["detail"]


def test_more_than_five_accompaniment_stems_are_400(singer: Stack, tmp_path: Path) -> None:
    response = _create(singer, _files(tmp_path, accompaniment=6))

    assert response.status_code == 400
    assert response.json()["detail"] == "A voice conversion takes 0 to 5 accompaniment stems."


def test_a_target_longer_than_the_limit_is_400(tmp_path: Path) -> None:
    settings = settings_for(tmp_path, kinds=("voiceConversion",), svc_max_target_seconds=10.0)
    for stack in open_stack(settings):
        response = _create(stack, _files(tmp_path))

        assert response.status_code == 400
        assert response.json()["detail"] == "The vocal stem is longer than 10 s (12.0 s)."


def test_mode_oom_retries_the_conversion_once(tmp_path: Path) -> None:
    for stack in open_stack(settings_for(tmp_path, "oom", kinds=("voiceConversion",))):
        generation_id = _create(stack, _files(tmp_path)).json()["generationId"]

        stack.run_all()

        assert stack.get(generation_id)["status"] == "completed"


def test_the_vocal_route_is_404_for_takes_without_a_vocal_stem(
    stack: Stack, tmp_path: Path
) -> None:
    song = {"caption": "c", "lyrics": "[verse]\nla", "instrumental": False,
            "vocalLanguage": "en", "durationSeconds": 10.0, "seeds": [1]}  # fmt: skip
    body = {"kind": "song", "song": song}
    generation_id = stack.client.post("/v1/generations", json=body).json()["generationId"]
    stack.run_all()

    assert stack.client.get(f"/v1/generations/{generation_id}/takes/1/vocal").status_code == 404


def test_a_conversion_sent_to_the_generator_is_409(stack: Stack, tmp_path: Path) -> None:
    response = _create(stack, _files(tmp_path))

    assert response.status_code == 409
    assert response.json()["detail"] == (
        "This service does not convert voices; it serves restyle, song."
    )
