"""Tests of ``POST /v1/artwork`` and ``GET /v1/artwork/{id}/{variant}`` (005 T044, FR-013)."""

import hashlib
import sys

import pytest

from stemstudio_worker import main
from stemstudio_worker.main import AppOverrides
from stemstudio_worker.ports import (
    ImageTooLargeError,
    NotAnImageError,
    UnsupportedImageTypeError,
)

from .conftest import Api, ApiFactory
from .contract import assert_conforms
from .fakes import COVER_JPEG, THUMB_JPEG, StaticNormalizer

POST = "/v1/artwork"
GET = "/v1/artwork/{artworkId}/{variant}"
PICTURE = b"\x89PNG\r\n\x1a\n picture"
LIMIT = 10 * 1024 * 1024


@pytest.fixture
def normalizer() -> StaticNormalizer:
    return StaticNormalizer()


@pytest.fixture
def art_api(make_api: ApiFactory, normalizer: StaticNormalizer) -> Api:
    return make_api(overrides=AppOverrides(image_normalizer=normalizer))


def test_a_picture_is_normalised_and_answered_201(
    art_api: Api, normalizer: StaticNormalizer
) -> None:
    response = art_api.client.post(POST, files={"file": ("me.png", PICTURE, "image/png")})

    assert response.status_code == 201
    assert_conforms(response, POST, "post")
    body = response.json()
    assert response.headers["location"] == f"/v1/artwork/{body['artworkId']}/cover"
    assert body["info"] == {
        "source": "user", "sha256": hashlib.sha256(COVER_JPEG).hexdigest(),
        "coverBytes": len(COVER_JPEG), "thumbBytes": len(THUMB_JPEG), "originUrl": None,
    }  # fmt: skip
    assert [(c.source, c.data) for c in normalizer.seen] == [("user", PICTURE)]


@pytest.mark.parametrize(("variant", "content"), [("cover", COVER_JPEG), ("thumb", THUMB_JPEG)])
def test_the_normalised_variants_are_served(art_api: Api, variant: str, content: bytes) -> None:
    artwork_id = art_api.client.post(POST, files={"file": ("a.jpg", PICTURE)}).json()["artworkId"]

    response = art_api.client.get(f"/v1/artwork/{artwork_id}/{variant}")

    assert response.status_code == 200
    assert_conforms(response, GET)
    assert response.content == content
    assert response.headers["etag"] == f'"{hashlib.sha256(COVER_JPEG).hexdigest()}"'


@pytest.mark.parametrize(
    "case",
    [
        (NotAnImageError("The file could not be read as a picture."), 400, "Not an image"),
        (
            UnsupportedImageTypeError("Only JPEG, PNG and WebP pictures are supported."),
            415,
            "Unsupported picture type",
        ),
        (ImageTooLargeError("The picture has more than 40 megapixels."), 422, "Picture too large"),
    ],
)
def test_normaliser_rejections_map_to_their_contract_status(
    art_api: Api, normalizer: StaticNormalizer, case: tuple[Exception, int, str]
) -> None:
    error, status, title = case
    normalizer.error = error

    response = art_api.client.post(POST, files={"file": ("x.png", PICTURE, "image/png")})

    assert response.status_code == status
    assert_conforms(response, POST, "post")
    assert (response.json()["title"], response.json()["detail"]) == (title, str(error))


def test_a_picture_over_10_mib_is_413_and_never_normalised(
    art_api: Api, normalizer: StaticNormalizer
) -> None:
    response = art_api.client.post(POST, files={"file": ("big.png", b"x" * (LIMIT + 1))})

    assert response.status_code == 413
    assert_conforms(response, POST, "post")
    assert response.json()["detail"] == "The picture is larger than 10 MiB."
    assert normalizer.seen == []


def test_exactly_10_mib_is_accepted(art_api: Api) -> None:
    response = art_api.client.post(POST, files={"file": ("max.png", b"x" * LIMIT)})

    assert response.status_code == 201


def test_a_declared_length_far_over_the_limit_is_413_before_reading(art_api: Api) -> None:
    headers = {"content-type": "multipart/form-data; boundary=x", "content-length": str(LIMIT * 2)}

    response = art_api.client.post(POST, content=b"--x--\r\n", headers=headers)

    assert response.status_code == 413


@pytest.mark.parametrize(
    ("kwargs", "detail"),
    [
        ({"files": {"other": ("a.png", PICTURE)}}, "Send the picture as multipart part 'file'."),
        ({"files": {"file": ("a.png", b"")}}, "The file is empty."),
        ({"json": {"file": "x"}}, "Send the picture as multipart part 'file'."),
        (
            {"content": b"garbage", "headers": {"content-type": "multipart/form-data"}},
            None,
        ),
    ],
)
def test_requests_without_a_picture_are_400(
    art_api: Api, kwargs: dict[str, object], detail: str | None
) -> None:
    response = art_api.client.post(POST, **kwargs)  # type: ignore[arg-type]

    assert response.status_code == 400
    assert response.headers["content-type"] == "application/problem+json"
    if detail is not None:
        assert response.json()["detail"] == detail


@pytest.mark.parametrize(
    "path", ["/v1/artwork/{id}/poster", f"/v1/artwork/{'0' * 32}/cover", "/v1/artwork/nope/cover"]
)
def test_unknown_pictures_and_variants_answer_404(art_api: Api, path: str) -> None:
    artwork_id = art_api.client.post(POST, files={"file": ("a.png", PICTURE)}).json()["artworkId"]

    response = art_api.client.get(path.format(id=artwork_id))

    assert response.status_code == 404
    assert_conforms(response, GET)


def test_without_an_image_normaliser_uploads_are_refused(
    make_api: ApiFactory, monkeypatch: pytest.MonkeyPatch
) -> None:
    monkeypatch.setitem(sys.modules, main.METADATA_PACKAGE, None)
    api = make_api()

    response = api.client.post(POST, files={"file": ("a.png", PICTURE)})

    assert response.status_code == 503
    assert response.headers["content-type"] == "application/problem+json"


def test_the_upload_store_is_swept_by_the_runtime(art_api: Api) -> None:
    lanes = art_api.client.app.state.runtime.lanes  # type: ignore[attr-defined]

    assert lanes.artwork.sweep() == []
