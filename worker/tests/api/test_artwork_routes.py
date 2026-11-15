"""Tests of ``GET /v1/jobs/{jobId}/artwork/{variant}`` and ``Job.artwork`` (005 T024)."""

import hashlib

import pytest

from stemstudio_worker.main import AppOverrides

from .conftest import Api, ApiFactory
from .contract import assert_conforms
from .fakes import COVER_JPEG, THUMB_JPEG, THUMBNAIL, StaticNormalizer

PATH = "/v1/jobs/{jobId}/artwork/{variant}"
SHA = hashlib.sha256(COVER_JPEG).hexdigest()


@pytest.fixture
def normalizer() -> StaticNormalizer:
    return StaticNormalizer()


@pytest.fixture
def art_api(make_api: ApiFactory, normalizer: StaticNormalizer) -> Api:
    """The default app with a fake normaliser; downloads offer a YouTube thumbnail."""
    api = make_api(overrides=AppOverrides(image_normalizer=normalizer))
    api.fakes.artwork = THUMBNAIL
    return api


def test_a_completed_job_reports_its_artwork(art_api: Api) -> None:
    job_id = art_api.completed_job()

    response = art_api.client.get(f"/v1/jobs/{job_id}")

    assert_conforms(response, "/v1/jobs/{jobId}")
    artwork = response.json()["artwork"]
    assert artwork == {
        "source": "youtube", "sha256": SHA, "coverBytes": len(COVER_JPEG),
        "thumbBytes": len(THUMB_JPEG), "originUrl": THUMBNAIL.url,
    }  # fmt: skip


@pytest.mark.parametrize(("variant", "body"), [("cover", COVER_JPEG), ("thumb", THUMB_JPEG)])
def test_both_variants_are_served_as_jpeg_with_the_cover_hash_as_etag(
    art_api: Api, variant: str, body: bytes
) -> None:
    job_id = art_api.completed_job()

    response = art_api.client.get(f"/v1/jobs/{job_id}/artwork/{variant}")

    assert response.status_code == 200
    assert_conforms(response, PATH)
    assert response.headers["content-type"] == "image/jpeg"
    assert response.headers["etag"] == f'"{SHA}"'
    assert response.content == body


def test_a_job_without_artwork_answers_404(make_api: ApiFactory) -> None:
    api = make_api()
    job_id = api.completed_job()

    response = api.client.get(f"/v1/jobs/{job_id}/artwork/cover")

    assert response.status_code == 404
    assert_conforms(response, PATH)
    assert api.client.get(f"/v1/jobs/{job_id}").json()["artwork"] is None


def test_a_failed_normalisation_leaves_the_job_without_artwork(
    art_api: Api, normalizer: StaticNormalizer
) -> None:
    normalizer.error = RuntimeError("decoder crash")
    job_id = art_api.completed_job()

    job = art_api.client.get(f"/v1/jobs/{job_id}").json()

    assert (job["status"], job["artwork"]) == ("completed", None)
    assert art_api.client.get(f"/v1/jobs/{job_id}/artwork/thumb").status_code == 404


@pytest.mark.parametrize(
    "path",
    [
        "/v1/jobs/{job}/artwork/poster",
        "/v1/jobs/0123456789abcdef0123456789abcdef/artwork/cover",
        "/v1/jobs/not-an-id/artwork/cover",
    ],
)
def test_unknown_jobs_and_variants_answer_404(art_api: Api, path: str) -> None:
    job_id = art_api.completed_job()

    response = art_api.client.get(path.format(job=job_id))

    assert response.status_code == 404
    assert response.headers["content-type"] == "application/problem+json"


def test_a_cached_result_keeps_its_artwork(art_api: Api, normalizer: StaticNormalizer) -> None:
    first = art_api.completed_job()
    art_api.fakes.artwork = None
    upload = art_api.upload(data=b"youtube:dQw4w9WgXcQ")  # same normalised audio as the video
    art_api.wait()

    job = art_api.client.get(f"/v1/jobs/{upload.json()['jobId']}").json()

    assert job["artwork"]["sha256"] == SHA
    assert len(normalizer.seen) == 1
    assert first != upload.json()["jobId"]


def test_the_duration_is_reported_after_normalisation(art_api: Api) -> None:
    job_id = art_api.completed_job()

    metadata = art_api.client.get(f"/v1/jobs/{job_id}").json()["metadata"]

    assert metadata["durationSeconds"] == 0.2
