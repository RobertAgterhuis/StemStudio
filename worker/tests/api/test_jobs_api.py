"""Contract tests of the worker HTTP API: every response is validated against the YAML (T067)."""

import asyncio
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.ports import (
    AudioDecodeError,
    EmptyUploadError,
    InvalidSourceError,
    SourceInfo,
    UnsupportedMediaError,
    UploadTooLargeError,
)

from .conftest import Api, ApiFactory
from .contract import assert_conforms
from .fakes import FETCHED_ARTIST, FETCHED_TITLE, SPOTIFY, YOUTUBE, FourStemSeparator

JOBS = "/v1/jobs"
JOB = "/v1/jobs/{jobId}"
RESULT = "/v1/jobs/{jobId}/result"
STEM = "/v1/jobs/{jobId}/stems/{stem}"
UNKNOWN = "f" * 32


# ---------------------------------------------------------------- health


def test_health_reports_fake_separator(api: Api) -> None:
    response = api.client.get("/health")

    assert_conforms(response, "/health")
    assert response.json() == {
        "status": "ok",
        "separator": "fake",
        "device": "cpu",
        "deviceInfo": {
            "kind": "cpu",
            "name": None,
            "computeCapability": None,
            "totalMemoryMiB": None,
            "freeMemoryMiB": None,
            "reason": None,
        },
        "onlineLookups": True,
        "library": {"readable": True, "reason": None},
        "lanes": {
            "render": {"queued": 0, "running": False},
            "transcribe": {"queued": 0, "running": False},
        },
        "lyricsAlignment": {
            "available": True,
            "model": "large-v3-turbo",
            "modelCached": True,
            "auto": "gpu",
            "reason": None,
        },
        "transcription": {
            "models": {
                "basic-pitch-icassp2022": "not-downloaded",
                "piano-crnn-note-pedal": "not-downloaded",
            },
            "font": {"leipzig": False},
        },
        "contractVersion": "1.6.0",
    }


# ---------------------------------------------------------------- create from a URL


@pytest.mark.parametrize(("url", "source_type"), [(YOUTUBE, "youtube"), (SPOTIFY, "spotify")])
def test_create_url_job_returns_202_with_location(api: Api, url: str, source_type: str) -> None:
    api.fakes.gate.clear()

    response = api.post_url(url, source_type)

    assert response.status_code == 202
    assert_conforms(response, JOBS, "post")
    body = response.json()
    assert response.headers["location"] == f"/v1/jobs/{body['jobId']}"
    assert (body["status"], body["stage"], body["progress"]) == ("queued", "Queued", 0)
    assert body["sourceType"] == source_type
    assert body["metadata"]["sourceUrl"] == YOUTUBE
    api.fakes.gate.set()


def test_create_same_source_again_returns_200_with_the_existing_job(api: Api) -> None:
    first = api.post_url().json()

    again = api.post_url()

    assert again.status_code == 200
    assert_conforms(again, JOBS, "post")
    assert again.json()["jobId"] == first["jobId"]
    assert "location" not in again.headers


@pytest.mark.parametrize(
    "body",
    [
        {"sourceType": "youtube", "url": "https://example.com/video"},
        {"sourceType": "spotify", "url": "https://open.spotify.com/track/nomatch"},
        {"sourceType": "spotify", "url": YOUTUBE},
        {"sourceType": "upload", "url": YOUTUBE},
        {"sourceType": "youtube"},
        {"sourceType": "youtube", "url": YOUTUBE, "extra": 1},
        {"sourceType": "youtube", "url": ""},
        {"sourceType": "youtube", "url": "https://www.youtube.com/watch?v=" + "x" * 2048},
    ],
)
def test_create_with_invalid_json_body_returns_400_problem(
    api: Api, body: dict[str, object]
) -> None:
    response = api.client.post(JOBS, json=body)

    assert response.status_code == 400
    assert_conforms(response, JOBS, "post")
    assert response.headers["content-type"] == "application/problem+json"
    assert response.json()["detail"]


def test_create_with_malformed_json_returns_400(api: Api) -> None:
    response = api.client.post(
        JOBS, content=b"{not json", headers={"content-type": "application/json"}
    )

    assert response.status_code == 400
    assert_conforms(response, JOBS, "post")


def test_invalid_source_message_is_passed_through(api: Api) -> None:
    response = api.post_url("https://example.com/video")

    assert response.json()["title"] == "Invalid source"
    assert response.json()["detail"] == "Only YouTube video and Spotify track URLs are supported."


def test_create_with_other_content_type_returns_415(api: Api) -> None:
    response = api.client.post(JOBS, content=b"url", headers={"content-type": "text/plain"})

    assert response.status_code == 415
    assert_conforms(response, JOBS, "post")


# ---------------------------------------------------------------- create from an upload


def test_upload_returns_202_and_completes(api: Api) -> None:
    response = api.upload()

    assert response.status_code == 202
    assert_conforms(response, JOBS, "post")
    body = response.json()
    assert (body["sourceType"], body["metadata"]["title"]) == ("upload", "My Song")
    assert body["sourceKey"].startswith("upload:")
    api.wait()
    done = api.client.get(f"/v1/jobs/{body['jobId']}")
    assert done.json()["status"] == "completed"
    assert not (api.settings.data_dir / "uploads" / body["jobId"]).exists()


def test_duplicate_upload_returns_existing_job_and_discards_the_copy(api: Api) -> None:
    api.fakes.gate.clear()
    first = api.upload().json()

    again = api.upload(name="Copy.MP3")

    assert again.status_code == 200
    assert again.json()["jobId"] == first["jobId"]
    uploads = sorted(p.name for p in (api.settings.data_dir / "uploads").iterdir())
    assert uploads == [first["jobId"]]
    assert (api.settings.data_dir / "uploads" / first["jobId"] / "input.mp3").is_file()


@pytest.mark.parametrize("name", ["song.ogg", "song", "song.mp3.exe", ".mp3x"])
def test_upload_with_wrong_type_returns_415(api: Api, name: str) -> None:
    response = api.upload(name=name)

    assert response.status_code == 415
    assert_conforms(response, JOBS, "post")
    assert not any((api.settings.data_dir / "uploads").glob("*/*"))


@pytest.mark.parametrize(
    ("error", "status"),
    [
        (EmptyUploadError, 400),
        (UploadTooLargeError, 413),
        (UnsupportedMediaError, 415),
        (InvalidSourceError, 400),
    ],
)
def test_upload_rejected_by_intake_maps_to_the_contract_status(
    api: Api, monkeypatch: pytest.MonkeyPatch, error: type[InvalidSourceError], status: int
) -> None:
    def reject(file_name: str, path: Path) -> SourceInfo:
        raise error("Not audio.")

    monkeypatch.setattr(api.fakes, "accept", reject)

    response = api.upload()

    assert response.status_code == status
    assert_conforms(response, JOBS, "post")
    assert response.json()["detail"] == "Not audio."
    assert not any((api.settings.data_dir / "uploads").iterdir())


def test_upload_over_the_limit_returns_413(api: Api) -> None:
    response = api.upload(data=b"x" * 1_001)

    assert response.status_code == 413
    assert_conforms(response, JOBS, "post")
    assert not any((api.settings.data_dir / "uploads").iterdir())


def test_upload_at_the_limit_is_accepted(api: Api) -> None:
    assert api.upload(data=b"x" * 1_000).status_code == 202


def test_upload_with_huge_content_length_is_rejected_before_reading(api: Api) -> None:
    response = api.client.post(
        JOBS,
        content=b"--b\r\n",
        headers={"content-type": "multipart/form-data; boundary=b", "content-length": "999999"},
    )

    assert response.status_code == 413


def test_upload_without_file_part_returns_400(api: Api) -> None:
    response = api.client.post(JOBS, files={"other": ("a.mp3", b"x")})

    assert response.status_code == 400
    assert_conforms(response, JOBS, "post")


def test_form_urlencoded_body_returns_415(api: Api) -> None:
    response = api.client.post(JOBS, data={"url": YOUTUBE})

    assert response.status_code == 415
    assert_conforms(response, JOBS, "post")


def test_empty_upload_returns_400(api: Api) -> None:
    response = api.upload(data=b"")

    assert response.status_code == 400
    assert_conforms(response, JOBS, "post")
    assert "empty" in response.json()["detail"]
    assert not any((api.settings.data_dir / "uploads").iterdir())


def test_ogg_upload_returns_415(api: Api) -> None:
    response = api.upload(name="song.ogg", data=b"OggS audio")

    assert response.status_code == 415
    assert_conforms(response, JOBS, "post")


def test_malformed_multipart_body_returns_400(api: Api) -> None:
    response = api.client.post(
        JOBS, content=b"no parts", headers={"content-type": "multipart/form-data"}
    )

    assert response.status_code == 400
    assert_conforms(response, JOBS, "post")


def test_chunked_upload_over_the_limit_is_aborted_while_streaming(api: Api) -> None:
    """Without Content-Length the body is counted while it streams; reading stops at the limit."""
    chunk, count = b"x" * 4096, 100  # 400 KiB, far over 1000 bytes + the multipart allowance
    head = (
        b'--b\r\nContent-Disposition: form-data; name="file"; filename="big.mp3"\r\n'
        b"Content-Type: audio/mpeg\r\n\r\n"
    )
    body = [head, *([chunk] * count), b"\r\n--b--\r\n"]

    status, pulled = asyncio.run(_send_chunked(api, body))

    assert status == 413
    assert pulled < count // 2
    assert list(api.settings.data_dir.glob("uploads/*")) == []


async def _send_chunked(api: Api, body: list[bytes]) -> tuple[int, int]:
    """Call the ASGI app with a chunked body (no Content-Length); return (status, chunks read)."""
    pulled = 0
    statuses: list[int] = []

    async def receive() -> dict[str, Any]:
        nonlocal pulled
        if pulled >= len(body):
            return {"type": "http.disconnect"}
        pulled += 1
        return {"type": "http.request", "body": body[pulled - 1], "more_body": pulled < len(body)}

    async def send(message: dict[str, Any]) -> None:
        if message["type"] == "http.response.start":
            statuses.append(message["status"])

    scope = {
        "type": "http", "asgi": {"version": "3.0"}, "http_version": "1.1", "method": "POST",
        "scheme": "http", "path": JOBS, "raw_path": JOBS.encode(), "query_string": b"",
        "root_path": "", "client": ("test", 1), "server": ("test", 80),
        "headers": [(b"content-type", b"multipart/form-data; boundary=b"),
                    (b"transfer-encoding", b"chunked")],
        "app": api.client.app, "state": {},
    }  # fmt: skip
    await api.client.app(scope, receive, send)  # type: ignore[arg-type]
    return statuses[0], pulled


# ---------------------------------------------------------------- get job


def test_get_job_reports_completed_state(api: Api) -> None:
    job_id = api.completed_job()

    response = api.client.get(f"/v1/jobs/{job_id}")

    assert_conforms(response, JOB)
    assert (response.json()["status"], response.json()["progress"]) == ("completed", 100)


def test_get_job_reports_failure(api: Api) -> None:
    api.fakes.normalize_error = AudioDecodeError("The file could not be decoded.")
    job_id = api.completed_job()

    response = api.client.get(f"/v1/jobs/{job_id}")

    assert_conforms(response, JOB)
    body = response.json()
    assert (body["status"], body["stage"], body["error"]) == (
        "failed",
        "Failed",
        "The file could not be decoded.",
    )


@pytest.mark.parametrize("job_id", [UNKNOWN, "not-a-job", "A" * 32])
def test_get_unknown_job_returns_404(api: Api, job_id: str) -> None:
    response = api.client.get(f"/v1/jobs/{job_id}")

    assert response.status_code == 404
    assert_conforms(response, JOB)


# ---------------------------------------------------------------- result


def test_result_of_completed_job(api: Api) -> None:
    job_id = api.completed_job()

    response = api.client.get(f"/v1/jobs/{job_id}/result")

    assert response.status_code == 200
    assert_conforms(response, RESULT)
    body = response.json()
    assert (body["jobId"], body["model"], body["sampleRate"], body["channels"]) == (
        job_id,
        "fake",
        44_100,
        2,
    )
    assert [s["name"] for s in body["stems"]] == [
        "vocals", "drums", "bass", "guitar", "piano", "other"
    ]  # fmt: skip
    assert body["beatGrid"]["bpm"] == 100


def test_result_of_unknown_job_returns_404(api: Api) -> None:
    response = api.client.get(f"/v1/jobs/{UNKNOWN}/result")

    assert response.status_code == 404
    assert_conforms(response, RESULT)


def test_result_of_running_job_returns_409(api: Api) -> None:
    api.fakes.gate.clear()
    job_id = api.post_url().json()["jobId"]

    response = api.client.get(f"/v1/jobs/{job_id}/result")

    assert response.status_code == 409
    assert_conforms(response, RESULT)
    assert response.json()["detail"].startswith("Job is in state ")
    api.fakes.gate.set()


# ---------------------------------------------------------------- stems


def test_stem_download_is_flac(api: Api) -> None:
    job_id = api.completed_job()
    result = api.client.get(f"/v1/jobs/{job_id}/result").json()

    response = api.client.get(f"/v1/jobs/{job_id}/stems/vocals")

    assert response.status_code == 200
    assert_conforms(response, STEM)
    assert response.headers["content-type"] == "audio/flac"
    assert int(response.headers["content-length"]) == len(response.content)
    assert len(response.content) == result["stems"][0]["sizeBytes"]
    assert response.content[:4] == b"fLaC"


@pytest.mark.parametrize("stem", ["kazoo", "vocals.flac", "VOCALS"])
def test_unknown_stem_name_returns_404(api: Api, stem: str) -> None:
    job_id = api.completed_job()

    response = api.client.get(f"/v1/jobs/{job_id}/stems/{stem}")

    assert response.status_code == 404
    assert_conforms(response, STEM)


def test_stem_not_produced_by_the_model_returns_404(make_api: ApiFactory) -> None:
    api = make_api(separator=FourStemSeparator())
    job_id = api.completed_job()

    assert api.client.get(f"/v1/jobs/{job_id}/stems/bass").status_code == 200
    response = api.client.get(f"/v1/jobs/{job_id}/stems/guitar")

    assert response.status_code == 404
    assert_conforms(response, STEM)


def test_stem_file_missing_on_disk_returns_404(api: Api) -> None:
    job_id = api.completed_job()
    sha = api.client.get(f"/v1/jobs/{job_id}/result").json()["audioSha256"]
    (api.settings.data_dir / "cache" / "by-audio" / sha / "stems" / "drums.flac").unlink()

    response = api.client.get(f"/v1/jobs/{job_id}/stems/drums")

    assert response.status_code == 404
    assert_conforms(response, STEM)


def test_stem_of_unknown_job_returns_404(api: Api) -> None:
    response = api.client.get(f"/v1/jobs/{UNKNOWN}/stems/vocals")

    assert response.status_code == 404
    assert_conforms(response, STEM)


def test_stem_of_running_job_returns_409(api: Api) -> None:
    api.fakes.gate.clear()
    job_id = api.post_url().json()["jobId"]

    response = api.client.get(f"/v1/jobs/{job_id}/stems/vocals")

    assert response.status_code == 409
    assert_conforms(response, STEM)
    api.fakes.gate.set()


def test_unknown_route_returns_problem_json(api: Api) -> None:
    response = api.client.get("/v2/nothing")

    assert response.status_code == 404
    assert response.headers["content-type"] == "application/problem+json"
    assert response.json()["status"] == 404


# ---------------------------------------------------------------- metadata from the download


def test_youtube_job_and_result_carry_the_downloaded_title(api: Api) -> None:
    job_id = api.completed_job()

    job = api.client.get(f"/v1/jobs/{job_id}").json()
    result = api.client.get(f"/v1/jobs/{job_id}/result").json()

    expected = {
        "title": FETCHED_TITLE,
        "artist": FETCHED_ARTIST,
        "sourceUrl": YOUTUBE,
        "durationSeconds": 0.2,
        **dict.fromkeys(("album", "isrc", "lookupTitle", "lookupArtist")),
    }
    assert job["metadata"] == result["metadata"] == expected


def test_spotify_job_keeps_the_spotify_title(api: Api) -> None:
    job_id = api.post_url(SPOTIFY, "spotify").json()["jobId"]
    api.wait()

    metadata = api.client.get(f"/v1/jobs/{job_id}").json()["metadata"]

    assert (metadata["title"], metadata["artist"]) == ("Track", "Band")
