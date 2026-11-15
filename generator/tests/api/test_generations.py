"""``POST/GET/DELETE /v1/generations`` and cancel with the fake stack (009 T032, FR-001, FR-007)."""

import json
from pathlib import Path
from typing import Any

import pytest

from stemstudio_generator.api.uploads import UploadLimits
from stemstudio_generator.schemas import GPU_REQUIRED_TITLE

from ..contract.docs import V110
from .stack import Stack, multipart, open_stack, restyle_params, settings_for, silence, stem


def _post(stack: Stack, files: Any) -> Any:
    return stack.client.post("/v1/generations", files=files)


def _problem(response: Any, status: int, code: str | None) -> dict[str, Any]:
    assert response.status_code == status, response.text
    assert response.headers["content-type"].startswith("application/problem+json")
    body: dict[str, Any] = response.json()
    assert V110.errors("GeneratorProblem", body) == []
    assert body.get("code") == code
    return body


def test_a_restyle_is_queued_with_location_and_a_contract_body(
    stack: Stack, tmp_path: Path
) -> None:
    files = multipart(restyle_params(), [stem(tmp_path, "a.wav"), stem(tmp_path, "b.wav")])

    response = _post(stack, files)

    assert response.status_code == 202, response.text
    body = response.json()
    assert V110.errors("Generation", body) == []
    assert body["status"] == "queued"
    assert body["kind"] == "restyle"
    assert "song" not in body
    assert response.headers["location"] == f"/v1/generations/{body['generationId']}"
    inputs = stack.composition.files.inputs(body["generationId"])
    assert sorted(path.name for path in inputs.iterdir()) == ["stem-1.wav", "stem-2.wav"]


def test_flac_stems_and_octet_stream_parts_are_accepted(stack: Stack, tmp_path: Path) -> None:
    files = multipart(restyle_params(), [stem(tmp_path, "a.flac")], "application/octet-stream")

    assert _post(stack, files).status_code == 202


def test_get_returns_the_job_and_404_for_unknown_or_malformed_ids(
    stack: Stack, tmp_path: Path
) -> None:
    created = _post(stack, multipart(restyle_params(), [stem(tmp_path, "a.wav")])).json()

    assert stack.get(created["generationId"])["status"] == "queued"
    unknown = stack.client.get(f"/v1/generations/{'f' * 32}")
    assert _problem(unknown, 404, None)["detail"] == f"Generation {'f' * 32} does not exist."
    _problem(stack.client.get("/v1/generations/not-an-id"), 404, None)


@pytest.mark.parametrize(
    ("params", "detail"),
    [
        (restyle_params(rangeEndSeconds=9.9), "The range must be between 10 and 480 seconds long."),
        (restyle_params(rangeStartSeconds=0.0, rangeEndSeconds=480.5), "between 10 and 480"),
        (restyle_params(seeds=[1, 2, 3]), "seeds"),
        (restyle_params(seeds=[]), "seeds"),
        (restyle_params(audioCoverStrength=1.5), "audioCoverStrength"),
        ({"kind": "restyle"}, "restyle"),
        (b"{not json", "params"),
    ],
)
def test_invalid_params_are_400_invalid_input(
    stack: Stack, tmp_path: Path, params: Any, detail: str
) -> None:
    response = _post(stack, multipart(params, [stem(tmp_path, "a.wav")]))

    assert detail in _problem(response, 400, "invalidInput")["detail"]


def test_a_missing_params_part_is_400(stack: Stack, tmp_path: Path) -> None:
    files = multipart(restyle_params(), [stem(tmp_path, "a.wav")])[1:]

    assert "params" in _problem(_post(stack, files), 400, "invalidInput")["detail"]


@pytest.mark.parametrize("count", [0, 7])
def test_one_to_six_stems_are_required(stack: Stack, tmp_path: Path, count: int) -> None:
    stems = [stem(tmp_path, f"s{index}.wav", 12.0) for index in range(count)]

    response = _post(stack, multipart(restyle_params(rangeEndSeconds=10.0), stems))

    assert _problem(response, 400, "invalidInput")["detail"] == "A restyle needs 1 to 6 stems."


def test_unreadable_audio_is_400(stack: Stack, tmp_path: Path) -> None:
    broken = tmp_path / "broken.wav"
    broken.write_bytes(b"RIFF not really a wave file")

    response = _post(stack, multipart(restyle_params(), [broken]))

    _problem(response, 400, "invalidInput")
    assert stack.composition.store.list_all() == []


def test_stems_with_different_sample_rates_are_400(stack: Stack, tmp_path: Path) -> None:
    stems = [stem(tmp_path, "a.wav"), stem(tmp_path, "b.wav", rate=44_100)]

    response = _post(stack, multipart(restyle_params(), stems))

    assert _problem(response, 400, "invalidInput")["detail"] == (
        "All stems must have the same sample rate."
    )


def test_a_range_beyond_the_source_is_422_source_too_short(stack: Stack, tmp_path: Path) -> None:
    params = restyle_params(rangeStartSeconds=10.0, rangeEndSeconds=40.0)

    response = _post(stack, multipart(params, [silence(tmp_path, "a.wav", 30.0)]))

    assert _problem(response, 422, "sourceTooShort")["detail"] == (
        "The range ends at 40.0 s but the source is 30.0 s long."
    )


def test_a_stem_of_another_type_is_415(stack: Stack, tmp_path: Path) -> None:
    files = multipart(restyle_params(), [stem(tmp_path, "a.wav")], "audio/mpeg")

    response = _post(stack, files)

    assert _problem(response, 415, "invalidInput")["detail"] == (
        "Only FLAC and WAV stems are supported."
    )


def test_a_body_that_is_not_multipart_is_415(stack: Stack) -> None:
    response = stack.client.post(
        "/v1/generations", content=b"x", headers={"content-type": "text/plain"}
    )

    _problem(response, 415, "invalidInput")


def test_a_stem_above_the_size_limit_is_413(tmp_path: Path) -> None:
    for stack in open_stack(settings_for(tmp_path)):
        stack.composition.app.state.context.limits = UploadLimits(stem_bytes=1_000)

        response = _post(stack, multipart(restyle_params(), [stem(tmp_path, "a.wav")]))

        assert _problem(response, 413, "invalidInput")["detail"] == (
            "Stem 1 is larger than 1,000 bytes."
        )


def test_without_a_gpu_every_create_is_409_gpu_required(tmp_path: Path) -> None:
    for stack in open_stack(settings_for(tmp_path, "no-gpu")):
        response = _post(stack, multipart(restyle_params(), [stem(tmp_path, "a.wav")]))

        body = _problem(response, 409, "gpuRequired")
        assert body["title"] == GPU_REQUIRED_TITLE
        assert body["detail"].startswith("No usable NVIDIA GPU")


def test_a_kind_the_instance_does_not_serve_is_409_kind_not_served(tmp_path: Path) -> None:
    settings = settings_for(tmp_path, kinds=("voiceConversion",))
    for stack in open_stack(settings):
        response = _post(stack, multipart(restyle_params(), [stem(tmp_path, "a.wav")]))

        assert _problem(response, 409, "kindNotServed")["detail"] == (
            "This service does not restyle songs; it serves voiceConversion."
        )


def test_cancel_of_a_queued_job_is_immediate(stack: Stack, tmp_path: Path) -> None:
    created = _post(stack, multipart(restyle_params(), [stem(tmp_path, "a.wav")])).json()

    response = stack.client.post(f"/v1/generations/{created['generationId']}/cancel")

    assert response.status_code == 200
    assert V110.errors("Generation", response.json()) == []
    assert response.json()["status"] == "cancelled"
    stack.run_all()
    assert stack.get(created["generationId"])["status"] == "cancelled"


def test_cancel_of_a_terminal_job_returns_it_unchanged(stack: Stack, tmp_path: Path) -> None:
    created = _post(stack, multipart(restyle_params(), [stem(tmp_path, "a.wav")])).json()
    stack.run_all()

    response = stack.client.post(f"/v1/generations/{created['generationId']}/cancel")

    assert response.json()["status"] == "completed"


def test_cancel_and_delete_of_unknown_jobs_are_404(stack: Stack) -> None:
    _problem(stack.client.post(f"/v1/generations/{'e' * 32}/cancel"), 404, None)
    _problem(stack.client.delete(f"/v1/generations/{'e' * 32}"), 404, None)
    _problem(stack.client.post("/v1/generations/xyz/cancel"), 404, None)


def test_delete_removes_the_job_and_its_files(stack: Stack, tmp_path: Path) -> None:
    created = _post(stack, multipart(restyle_params(), [stem(tmp_path, "a.wav")])).json()
    stack.run_all()
    generation_id = created["generationId"]

    assert stack.client.delete(f"/v1/generations/{generation_id}").status_code == 204

    _problem(stack.client.get(f"/v1/generations/{generation_id}"), 404, None)
    assert not (stack.settings.data_dir / "jobs" / generation_id).exists()


def test_params_part_may_be_sent_as_a_form_field(stack: Stack, tmp_path: Path) -> None:
    path = stem(tmp_path, "a.wav")
    response = stack.client.post(
        "/v1/generations",
        data={"params": json.dumps(restyle_params())},
        files=[("stem", (path.name, path.read_bytes(), "audio/wav"))],
    )

    assert response.status_code == 202, response.text
