"""005:T033: Cover Art Archive front of the matched release group (research F-03)."""

import io

import httpx
import pytest
from PIL import Image

from stemstudio_worker.metadata.coverart import CAA_ROOT, CoverArtArchive
from stemstudio_worker.metadata.http import HostNotAllowedError, OnlineRequestError
from stemstudio_worker.ports import NotAnImageError

from .fakes import Router, online_harness, routed_client
from .images import solid
from .reference import load, router_for

LOAD = "e389b7df-862d-3d91-a612-acca150f6e71"
FRONT = f"{CAA_ROOT}/release-group/{LOAD}/front-500"


def test_redirect_to_archive_org_is_followed_and_normalised() -> None:
    router = router_for(load("metallica"))
    client = routed_client(router)

    front = CoverArtArchive(client).front(LOAD)

    assert front is not None
    images, info = front
    assert (info.source, info.origin_url, info.sha256) == ("coverartarchive", FRONT, images.sha256)
    assert Image.open(io.BytesIO(images.cover)).size == (600, 600)
    assert [r.url.host for r in router.requests] == ["coverartarchive.org", "archive.org"]


def test_direct_answer_without_redirect() -> None:
    router = router_for(load("kevin-macleod"))

    front = CoverArtArchive(routed_client(router)).front("057247bb-7a93-4654-9a7d-4cb9abaf5c5c")

    assert front is not None
    assert front[1].cover_bytes == len(front[0].cover)


def test_no_front_cover_is_none() -> None:
    harness = online_harness([httpx.Response(404)])

    assert CoverArtArchive(harness.client).front(LOAD) is None
    assert str(harness.recorder.requests[0].url) == FRONT


def test_undecodable_picture_raises_an_artwork_error() -> None:
    harness = online_harness([httpx.Response(200, content=b"<html>")])

    with pytest.raises(NotAnImageError):
        CoverArtArchive(harness.client).front(LOAD)


def test_server_error_raises_a_request_error() -> None:
    harness = online_harness([httpx.Response(500)])

    with pytest.raises(OnlineRequestError):
        CoverArtArchive(harness.client).front(LOAD)


def test_redirect_to_a_host_outside_the_allow_list_is_refused() -> None:
    router = Router()
    path = f"/release-group/{LOAD}/front-500"
    router.add(
        "coverartarchive.org",
        path,
        httpx.Response(307, headers={"Location": "https://evil.example/x.jpg"}),
    )
    router.add("evil.example", "/x.jpg", httpx.Response(200, content=solid((10, 10))))

    with pytest.raises(HostNotAllowedError):
        CoverArtArchive(routed_client(router)).front(LOAD)

    assert [r.url.host for r in router.requests] == ["coverartarchive.org"]
