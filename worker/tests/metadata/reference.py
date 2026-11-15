"""Routes for the qa reference artists (``tests/fixtures/metadata/reference/*.json``).

The reference files hold the MusicBrainz artist and release group, the Cover Art Archive,
Wikidata and Wikipedia exchanges and the expected facts. Image bodies are not committed: a JPEG
of the listed size is generated in their place. The recording search is not part of the files;
``search_reply`` builds one for the reference recording.
"""

import json
from pathlib import Path
from typing import Any
from urllib.parse import urlsplit

import httpx
from PIL import Image

from .fakes import Router
from .images import encode

REFERENCE = Path(__file__).resolve().parents[3] / "tests" / "fixtures" / "metadata" / "reference"
MB_HOST = "musicbrainz.org"


def load(name: str) -> dict[str, Any]:
    """The reference file ``name`` (``metallica`` or ``kevin-macleod``)."""
    data: dict[str, Any] = json.loads((REFERENCE / f"{name}.json").read_text(encoding="utf-8"))
    return data


def _reply(response: dict[str, Any], image: bytes | None = None) -> httpx.Response:
    headers = response.get("headers") or {}
    if response.get("body") is not None:
        return httpx.Response(response["status"], headers=headers, json=response["body"])
    return httpx.Response(response["status"], headers=headers, content=image or b"")


def router_for(reference: dict[str, Any]) -> Router:
    """A router serving every exchange of ``reference`` by host, path and ``props``."""
    router = Router()
    for exchange in reference["musicbrainz"]:
        router.add(MB_HOST, exchange["request"]["path"], _reply(exchange["response"]))
    _add_cover_art(router, reference["coverArtArchive"])
    for exchange in reference["wikidata"]:
        router.add_wikidata(_props(exchange["request"]["query"]), _reply(exchange["response"]))
    for exchange in reference["wikipedia"]:
        request = exchange["request"]
        router.add(request["host"], request["path"], _reply(exchange["response"]))
    return router


def _add_cover_art(router: Router, cover: dict[str, Any]) -> None:
    width, height = cover["imageSize"]
    image = encode(Image.new("RGB", (width, height), (90, 60, 30)), "JPEG")
    for exchange in cover["exchanges"]:
        request = exchange["request"]
        host, path = request.get("host"), request.get("path")
        if "url" in request:
            parts = urlsplit(request["url"])
            host, path = parts.hostname, parts.path
        router.add(str(host), str(path), _reply(exchange["response"], image))


def _props(query: str) -> str:
    fields = dict(part.split("=", 1) for part in query.split("&"))
    return fields.get("props", "")


def query_fields(query: str) -> dict[str, str]:
    """The ``a=b&c=d`` query of a reference request as a dict."""
    return dict(part.split("=", 1) for part in query.split("&"))


def search_reply(
    reference: dict[str, Any], recording_mbid: str, title: str, length_ms: int
) -> httpx.Response:
    """A recording search answer whose only hit is the reference recording."""
    artist = reference["expected"]["artist"]
    recording = {
        "id": recording_mbid,
        "score": 100,
        "title": title,
        "length": length_ms,
        "artist-credit": [
            {"name": artist["name"], "artist": {"id": artist["mbid"], "name": artist["name"]}}
        ],
        "releases": [_release(reference["expected"]["releaseGroup"])],
    }
    return httpx.Response(200, json={"recordings": [recording]})


def _release(group: dict[str, Any]) -> dict[str, Any]:
    """An official release of the reference release group (dated at the end of its year)."""
    release_group = {
        "id": group["mbid"],
        "title": group["title"],
        "primary-type": group["primaryType"],
    }
    return {
        "status": "Official",
        "date": f"{group['firstReleaseYear']}-12-31",
        "release-group": release_group,
    }
