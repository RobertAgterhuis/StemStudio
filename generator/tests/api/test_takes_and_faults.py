"""Completed restyles, take audio, fault modes and restart (009 T032, FR-012, SC-006)."""

from pathlib import Path
from typing import Any

import pytest
import soundfile as sf

from stemstudio_generator.jobs.progress import updated
from stemstudio_generator.schemas import CLIPPED_WARNING, INTERRUPTED_TEXT, OUT_OF_MEMORY_TEXT

from ..contract.docs import V110
from .stack import Ids, Stack, multipart, open_stack, restyle_params, settings_for, stem


def _create(stack: Stack, tmp_path: Path, **changes: Any) -> str:
    files = multipart(restyle_params(**changes), [stem(tmp_path, "a.wav"), stem(tmp_path, "b.wav")])
    response = stack.client.post("/v1/generations", files=files)
    assert response.status_code == 202, response.text
    generation_id: str = response.json()["generationId"]
    return generation_id


def _lease_free(stack: Stack) -> bool:
    lease = stack.composition.gate.lease  # type: ignore[attr-defined]
    return not lease.held


def test_a_range_restyle_completes_with_labelled_takes_of_the_range_length(
    stack: Stack, tmp_path: Path
) -> None:
    generation_id = _create(stack, tmp_path, seeds=[1, 2], rangeStartSeconds=5.0)

    stack.run_all()

    job = stack.get(generation_id)
    assert (job["status"], job["progress"], job["device"]["kind"]) == ("completed", 100, "gpu")
    assert [(take["index"], take["seed"]) for take in job["takes"]] == [(1, 1), (2, 2)]
    first = job["takes"][0]
    assert first["durationSeconds"] == pytest.approx(15.0)
    assert (first["model"]["id"], first["vocal"], job["warnings"]) == (
        "acestep-v15-turbo",
        None,
        [],
    )
    assert [label.take_index for label in stack.writer.labels] == [1, 2]
    assert stack.writer.labels[0].generator.startswith("ACE-Step 1.5 (")
    assert stack.writer.labels[0].generation_id == generation_id
    assert _lease_free(stack)


def test_inputs_and_engine_files_are_removed_after_the_job(stack: Stack, tmp_path: Path) -> None:
    generation_id = _create(stack, tmp_path)

    stack.run_all()

    root = stack.settings.data_dir / "jobs" / generation_id
    assert sorted(path.name for path in root.iterdir()) == ["takes"]


def test_clipping_stems_add_the_clipped_warning(stack: Stack, tmp_path: Path) -> None:
    loud = [stem(tmp_path, f"loud{index}.wav") for index in range(4)]
    files = multipart(restyle_params(), loud)
    generation_id = stack.client.post("/v1/generations", files=files).json()["generationId"]

    stack.run_all()

    assert stack.get(generation_id)["warnings"] == [CLIPPED_WARNING]


def _completed_take(stack: Stack, tmp_path: Path) -> tuple[str, dict[str, Any]]:
    generation_id = _create(stack, tmp_path)
    stack.run_all()
    take: dict[str, Any] = stack.get(generation_id)["takes"][0]
    return f"/v1/generations/{generation_id}/takes/1", take


def test_take_audio_is_flac_with_etag(stack: Stack, tmp_path: Path) -> None:
    url, take = _completed_take(stack, tmp_path)

    whole = stack.client.get(url)

    assert whole.status_code == 200
    assert whole.headers["content-type"] == "audio/flac"
    assert whole.headers["etag"] == f'"{take["sha256"]}"'
    assert whole.headers["accept-ranges"] == "bytes"
    assert len(whole.content) == take["bytes"]
    generation_id = url.split("/")[3]
    assert sf.info(stack.composition.files.take(generation_id, 1)).samplerate == 48_000


def test_take_audio_serves_single_byte_ranges(stack: Stack, tmp_path: Path) -> None:
    url, take = _completed_take(stack, tmp_path)
    whole = stack.client.get(url)

    part = stack.client.get(url, headers={"range": "bytes=0-9"})
    outside = stack.client.get(url, headers={"range": f"bytes={take['bytes'] + 10}-"})

    assert part.status_code == 206
    assert part.content == whole.content[:10]
    assert part.headers["content-range"] == f"bytes 0-9/{take['bytes']}"
    assert outside.status_code == 416
    assert outside.headers["content-range"] == f"bytes */{take['bytes']}"


@pytest.mark.parametrize("index", ["2", "3", "0", "x"])
def test_missing_takes_are_404(stack: Stack, tmp_path: Path, index: str) -> None:
    generation_id = _create(stack, tmp_path)
    stack.run_all()

    response = stack.client.get(f"/v1/generations/{generation_id}/takes/{index}")

    assert response.status_code == 404
    assert V110.errors("GeneratorProblem", response.json()) == []


def test_takes_of_a_job_that_is_not_completed_are_404(stack: Stack, tmp_path: Path) -> None:
    generation_id = _create(stack, tmp_path)

    assert stack.client.get(f"/v1/generations/{generation_id}/takes/1").status_code == 404
    assert stack.client.get(f"/v1/generations/{'a' * 32}/takes/1").status_code == 404


def test_mode_oom_retries_with_low_memory_and_completes(tmp_path: Path) -> None:
    for stack in open_stack(settings_for(tmp_path, "oom")):
        generation_id = _create(stack, tmp_path)

        stack.run_all()

        assert stack.get(generation_id)["status"] == "completed"
        assert _lease_free(stack)


def test_mode_oom_twice_fails_readably_and_releases_the_lease(tmp_path: Path) -> None:
    for stack in open_stack(settings_for(tmp_path, "oom-twice")):
        generation_id = _create(stack, tmp_path)

        stack.run_all()

        job = stack.get(generation_id)
        assert job["status"] == "failed"
        assert job["error"] == {"code": "outOfMemory", "message": OUT_OF_MEMORY_TEXT}
        assert _lease_free(stack)


def test_mode_busy_fails_with_gpu_busy_after_the_wait(tmp_path: Path) -> None:
    for stack in open_stack(settings_for(tmp_path, "busy")):
        generation_id = _create(stack, tmp_path)

        stack.run_all()

        job = stack.get(generation_id)
        assert job["error"]["code"] == "gpuBusy"
        assert job["error"]["message"].startswith("GPU busy: needs ")
        assert _lease_free(stack)


def test_a_restart_fails_running_jobs_and_resumes_queued_ones(tmp_path: Path) -> None:
    settings, ids = settings_for(tmp_path), Ids()
    for stack in open_stack(settings, ids):
        running = _create(stack, tmp_path)
        queued = _create(stack, tmp_path)
        store = stack.composition.store
        job = store.get(running)
        assert job is not None
        store.save(updated(job, {"status": "running", "stage": "Generating"}, stack.clock.now()))

    for stack in open_stack(settings, ids):
        stack.run_all()

        assert stack.get(running)["error"] == {"code": "interrupted", "message": INTERRUPTED_TEXT}
        assert stack.get(queued)["status"] == "completed"
