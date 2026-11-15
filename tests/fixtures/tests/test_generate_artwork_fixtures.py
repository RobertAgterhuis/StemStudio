"""Tests of the feature 005 artwork fixtures (tagged audio with covers, own pictures)."""

from __future__ import annotations

import hashlib
import io
from pathlib import Path
from typing import Any

import pytest
import soundfile as sf  # type: ignore[import-untyped]
from PIL import Image, UnidentifiedImageError
from tinytag import TinyTag

import generate_artwork_fixtures as gen

GENERATOR = gen

AUDIO = ["cover_embedded_10s.mp3", "cover_embedded_10s.flac"]


@pytest.mark.parametrize("name", AUDIO)
def test_audio_carries_its_tags_and_front_cover(
    generated: tuple[Path, dict[str, Any]], name: str
) -> None:
    out_dir, manifest = generated
    facts = manifest["fixtures"][name]
    tag = TinyTag.get(out_dir / name, image=True)
    cover = tag.images.front_cover
    assert (tag.title, tag.artist, tag.album) == tuple(facts["tags"].values())
    assert cover is not None
    assert cover.mime_type == facts["picture"]["mediaType"]
    assert hashlib.sha256(cover.data).hexdigest() == facts["picture"]["sha256"]
    assert tag.duration == pytest.approx(facts["durationSeconds"], abs=0.1)


@pytest.mark.parametrize("name", AUDIO)
def test_embedded_cover_has_the_listed_size(
    generated: tuple[Path, dict[str, Any]], name: str
) -> None:
    out_dir, manifest = generated
    cover = TinyTag.get(out_dir / name, image=True).images.front_cover
    assert cover is not None
    picture = manifest["fixtures"][name]["picture"]
    assert Image.open(io.BytesIO(cover.data)).size == (picture["width"], picture["height"])


def test_flac_still_decodes_after_the_picture_block(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, _ = generated
    info = sf.info(str(out_dir / "cover_embedded_10s.flac"))
    assert (info.samplerate, info.channels, info.duration) == (44_100, 2, 10.0)


def test_mp3_cover_is_not_square_so_the_worker_must_crop(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    _, manifest = generated
    picture = manifest["fixtures"]["cover_embedded_10s.mp3"]["picture"]
    assert picture["width"] != picture["height"]


@pytest.mark.parametrize(
    ("name", "size", "image_format"),
    [
        ("own_picture_800x600.png", (800, 600), "PNG"),
        ("own_picture_640x640.jpg", (640, 640), "JPEG"),
        ("unsupported_picture.gif", (64, 64), "GIF"),
    ],
)
def test_pictures_decode_with_their_size_and_format(
    generated: tuple[Path, dict[str, Any]], name: str, size: tuple[int, int], image_format: str
) -> None:
    out_dir, _ = generated
    with Image.open(out_dir / name) as image:
        assert (image.size, image.format) == (size, image_format)


def test_not_a_picture_cannot_be_decoded(generated: tuple[Path, dict[str, Any]]) -> None:
    out_dir, manifest = generated
    with pytest.raises(UnidentifiedImageError):
        Image.open(out_dir / "not_a_picture.png")
    assert manifest["fixtures"]["not_a_picture.png"]["picture"] is None


def test_upload_statuses_follow_the_backend_contract(
    generated: tuple[Path, dict[str, Any]],
) -> None:
    _, manifest = generated
    statuses = {name: f["uploadStatus"] for name, f in manifest["fixtures"].items()}
    assert statuses == {
        "cover_embedded_10s.mp3": None,
        "cover_embedded_10s.flac": None,
        "own_picture_800x600.png": 200,
        "own_picture_640x640.jpg": 200,
        "not_a_picture.png": 400,
        "unsupported_picture.gif": 415,
    }


def test_every_picture_has_its_own_bytes() -> None:
    pictures = [f.picture for f in gen.CATALOGUE if f.picture is not None]
    digests = {hashlib.sha256(gen.render_picture(p)).hexdigest() for p in pictures}
    assert len(digests) == len(pictures)


def test_syncsafe_size_uses_seven_bits_per_byte() -> None:
    assert gen._syncsafe(0x0FFFFFFF) == b"\x7f\x7f\x7f\x7f"
    assert gen._syncsafe(200) == b"\x00\x00\x01\x48"


def test_flac_metadata_round_trips() -> None:
    blocks = [(0, b"\x01" * 34), (4, b"vendor"), (1, b"\x00" * 8)]
    data = gen.join_flac_metadata(blocks, b"frames")
    assert gen.split_flac_metadata(data) == (blocks, b"frames")
    assert data[4] == 0 and data[4 + 4 + 34] == 4
    assert data[4 + 4 + 34 + 4 + 6] == 0x81


def test_split_refuses_a_non_flac_stream() -> None:
    with pytest.raises(gen.ArtworkFixtureError, match="not a FLAC stream"):
        gen.split_flac_metadata(b"RIFF....")


@pytest.mark.parametrize("writer", [gen._write_mp3, gen._write_flac])
def test_audio_writers_need_tags_and_a_cover(writer: Any, tmp_path: Path) -> None:
    bare = gen.ArtworkFixture("x.flac", "audio-with-cover", None)
    with pytest.raises(gen.ArtworkFixtureError, match="need tags and a cover"):
        writer(bare, None, tmp_path / "x.flac")


def test_media_type_follows_the_format() -> None:
    assert gen.Picture(1, 1, "JPEG", 0).media_type == "image/jpeg"
