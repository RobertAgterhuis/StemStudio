"""Tests of the per-route request body limit (020 security fixes, ``POST /v1/handout-jobs``)."""

from collections.abc import Iterator

from fastapi import FastAPI, Request
from fastapi.testclient import TestClient

from stemstudio_worker.api.body_limit import BodyLimit

LIMIT = 100
TOO_LARGE = {
    "title": "Validation failed",
    "status": 422,
    "detail": "The request body is not valid.",
    "errors": {"body": ["The request body is larger than 100 bytes."]},
}


def _client() -> TestClient:
    app = FastAPI()

    @app.post("/limited")
    async def limited(request: Request) -> dict[str, int]:
        return {"size": len(await request.body())}

    @app.post("/other")
    async def other(request: Request) -> dict[str, int]:
        return {"size": len(await request.body())}

    app.add_middleware(BodyLimit, limits={"/limited": LIMIT})
    return TestClient(app)


def _problem(response: object) -> dict[str, object]:
    assert response.status_code == 422  # type: ignore[attr-defined]
    assert response.headers["content-type"] == "application/problem+json"  # type: ignore[attr-defined]
    body: dict[str, object] = response.json()  # type: ignore[attr-defined]
    return {key: body[key] for key in TOO_LARGE}


def test_a_body_within_the_limit_reaches_the_route_unchanged() -> None:
    assert _client().post("/limited", content=b"x" * LIMIT).json() == {"size": LIMIT}


def test_a_declared_length_over_the_limit_is_422() -> None:
    assert _problem(_client().post("/limited", content=b"x" * (LIMIT + 1))) == TOO_LARGE


def test_a_chunked_body_over_the_limit_is_422() -> None:
    def chunks() -> Iterator[bytes]:
        for _ in range(5):
            yield b"x" * 30

    assert _problem(_client().post("/limited", content=chunks())) == TOO_LARGE


def test_a_chunked_body_within_the_limit_reaches_the_route() -> None:
    def chunks() -> Iterator[bytes]:
        yield b"x" * 50
        yield b"y" * 50

    assert _client().post("/limited", content=chunks()).json() == {"size": 100}


def test_other_routes_and_methods_are_not_limited() -> None:
    client = _client()

    assert client.post("/other", content=b"x" * 1000).json() == {"size": 1000}
    assert client.get("/limited").status_code == 405
