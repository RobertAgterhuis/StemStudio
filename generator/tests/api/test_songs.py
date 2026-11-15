"""``kind: song`` over the API with the fake engine (010 T024, FR-001, FR-012)."""

from pathlib import Path
from typing import Any

import pytest

from ..contract.docs import V110
from .stack import Stack, open_stack, settings_for


def song_body(**changes: Any) -> dict[str, Any]:
    song = {
        "caption": "Pop rock, female lead vocal",
        "lyrics": "[verse]\nSing it loud\n\n[chorus]\nThis song is mine",
        "instrumental": False,
        "vocalLanguage": "en",
        "durationSeconds": 12.0,
        "seeds": [7, 8],
        "thinking": True,
        **changes,
    }
    return {"kind": "song", "song": song}


def _create(stack: Stack, body: dict[str, Any]) -> Any:
    return stack.client.post("/v1/generations", json=body)


def test_a_song_is_queued_from_a_json_body(stack: Stack) -> None:
    response = _create(stack, song_body())

    assert response.status_code == 202, response.text
    body = response.json()
    assert V110.errors("Generation", body) == []
    assert (body["kind"], body["status"]) == ("song", "queued")
    assert "restyle" not in body
    assert body["song"]["lyrics"].startswith("[verse]")


def test_a_song_completes_with_synthetic_voice_takes_and_labels(stack: Stack) -> None:
    generation_id = _create(stack, song_body()).json()["generationId"]

    stack.run_all()

    job = stack.get(generation_id)
    assert job["status"] == "completed"
    assert [take["vocal"] for take in job["takes"]] == ["synthetic", "synthetic"]
    assert [take["profile"] for take in job["takes"]] == ["planned", "planned"]
    assert job["takes"][0]["durationSeconds"] == pytest.approx(12.0)
    assert job["takes"][0]["hasVocalStem"] is False
    assert [label.voice for label in stack.writer.labels] == ["synthetic", "synthetic"]
    assert stack.writer.labels[0].generator.startswith("ACE-Step 1.5 (")


def test_an_instrumental_has_no_ai_voice_label(stack: Stack) -> None:
    body = song_body(instrumental=True, lyrics="[Instrumental]", seeds=[3])
    generation_id = _create(stack, body).json()["generationId"]

    stack.run_all()

    job = stack.get(generation_id)
    assert [take["vocal"] for take in job["takes"]] == ["none"]
    assert [label.voice for label in stack.writer.labels] == ["none"]


def test_a_song_without_planning_is_direct(stack: Stack) -> None:
    generation_id = _create(stack, song_body(thinking=False, bpm=100)).json()["generationId"]

    stack.run_all()

    assert [take["profile"] for take in stack.get(generation_id)["takes"]] == ["direct"] * 2


@pytest.mark.parametrize(
    ("changes", "field"),
    [
        ({"lyrics": ""}, "lyrics"),
        ({"lyrics": "x" * 4097}, "lyrics"),
        ({"durationSeconds": 9.0}, "durationSeconds"),
        ({"durationSeconds": 481.0}, "durationSeconds"),
        ({"bpm": 39}, "bpm"),
        ({"bpm": 221}, "bpm"),
        ({"keyScale": "H major"}, "keyScale"),
        ({"vocalLanguage": "xx"}, "vocalLanguage"),
        ({"timeSignature": "5"}, "timeSignature"),
        ({"seeds": [1, 2, 3]}, "seeds"),
    ],
)
def test_invalid_songs_are_400(stack: Stack, changes: dict[str, Any], field: str) -> None:
    response = _create(stack, song_body(**changes))

    assert response.status_code == 400
    assert response.json()["code"] == "invalidInput"
    assert field in response.json()["detail"]


def test_a_song_sent_to_the_singer_is_409_kind_not_served(tmp_path: Path) -> None:
    for stack in open_stack(settings_for(tmp_path, kinds=("voiceConversion",))):
        response = _create(stack, song_body())

        assert response.status_code == 409
        assert response.json()["detail"] == (
            "This service does not generate songs; it serves voiceConversion."
        )


def test_a_song_without_a_gpu_is_409_gpu_required(tmp_path: Path) -> None:
    for stack in open_stack(settings_for(tmp_path, "no-gpu")):
        assert _create(stack, song_body()).json()["code"] == "gpuRequired"


def test_a_song_job_in_mode_oom_retries_on_the_direct_low_memory_profile(tmp_path: Path) -> None:
    for stack in open_stack(settings_for(tmp_path, "oom")):
        generation_id = _create(stack, song_body()).json()["generationId"]

        stack.run_all()

        job = stack.get(generation_id)
        assert job["status"] == "completed"
        assert [take["profile"] for take in job["takes"]] == ["direct", "direct"]
