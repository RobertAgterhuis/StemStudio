"""Deterministic artwork fixtures for feature 005 (qa, 005:T009): tagged audio and own pictures.

Run from the repository root with the worker environment, which provides numpy, soundfile and
Pillow::

    uv run --project worker python tests/fixtures/generate_artwork_fixtures.py

The files go to ``tests/fixtures/audio/`` (git-ignored, next to the 001/002 fixtures) and their
facts to ``tests/fixtures/artwork-manifest.json`` (committed). Every picture is generated here
(gradients and solid colours), never taken from a third party:

* ``cover_embedded_10s.mp3``: 10 s of synthetic music with an ID3v2.3 tag (title, artist, album)
  and a 500 x 499 PNG front cover (``APIC`` type 3; not square, so the worker must centre-crop).
* ``cover_embedded_10s.flac``: other music with Vorbis comments and a 640 x 640 JPEG
  ``PICTURE`` block (type 3).
* ``own_picture_800x600.png`` / ``own_picture_640x640.jpg``: valid own pictures (US3, 200).
* ``not_a_picture.png``: text bytes with a PNG name (400 "Not an image").
* ``unsupported_picture.gif``: a valid GIF (415 "Unsupported picture type").

Reruns are byte-identical on the same machine (fixed seeds, no timestamps in any file). The
manifest ``sha256`` of a picture is taken over its decoded pixels (``pixel_digest``), because the
PNG encoders of the Windows and Linux Pillow wheels compress the same pixels to different bytes.
"""

from __future__ import annotations

import argparse
import hashlib
import io
import struct
import sys
from collections.abc import Callable, Sequence
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
import soundfile as sf  # type: ignore[import-untyped]
from PIL import Image

from generate_fixtures import (
    SAMPLE_RATE,
    Audio,
    Rhythm,
    cli_args,
    encode_mp3,
    render_music,
    write_manifest,
)

MANIFEST_VERSION = 1
AUDIO_SECONDS = 10.0
FRONT_COVER = 3  # ID3 APIC / FLAC PICTURE type "Cover (front)"
FLAC_PICTURE_BLOCK = 6
ID3_TEXT_LATIN1 = b"\x00"


class ArtworkFixtureError(RuntimeError):
    """Raised when a written file does not have the structure the generator expects."""


@dataclass(frozen=True)
class Picture:
    """A generated picture.

    Attributes:
        width: Width in pixels.
        height: Height in pixels.
        image_format: Pillow format name (``PNG``, ``JPEG``, ``GIF``).
        seed: Selects the colours, so two pictures never share bytes.
    """

    width: int
    height: int
    image_format: str
    seed: int

    @property
    def media_type(self) -> str:
        """IANA media type of the encoded picture."""
        return f"image/{self.image_format.lower()}"


@dataclass(frozen=True)
class Tags:
    """Title, artist and album written into an audio fixture."""

    title: str
    artist: str
    album: str


@dataclass(frozen=True)
class ArtworkFixture:
    """One catalogue entry.

    Attributes:
        name: File name inside the output directory.
        kind: ``audio-with-cover``, ``picture`` or ``invalid-picture``.
        picture: The generated picture (the embedded cover for audio), None for invalid files.
        tags: Tags of an audio fixture, None otherwise.
        seed: Music seed of an audio fixture.
        upload_status: Expected HTTP status of ``PUT /api/tracks/{id}/artwork`` with this file.
    """

    name: str
    kind: str
    picture: Picture | None
    tags: Tags | None = None
    seed: int = 0
    upload_status: int | None = None


CATALOGUE: tuple[ArtworkFixture, ...] = (
    ArtworkFixture(
        "cover_embedded_10s.mp3",
        "audio-with-cover",
        Picture(500, 499, "PNG", 51),
        Tags("Embedded Cover Song", "StemStudio Fixtures", "Synthetic Covers"),
        seed=20_261_151,
    ),
    ArtworkFixture(
        "cover_embedded_10s.flac",
        "audio-with-cover",
        Picture(640, 640, "JPEG", 52),
        Tags("Embedded Cover Song (FLAC)", "StemStudio Fixtures", "Synthetic Covers"),
        seed=20_261_152,
    ),
    ArtworkFixture(
        "own_picture_800x600.png", "picture", Picture(800, 600, "PNG", 53), upload_status=200
    ),
    ArtworkFixture(
        "own_picture_640x640.jpg", "picture", Picture(640, 640, "JPEG", 54), upload_status=200
    ),
    ArtworkFixture("not_a_picture.png", "invalid-picture", None, upload_status=400),
    ArtworkFixture(
        "unsupported_picture.gif", "invalid-picture", Picture(64, 64, "GIF", 55), upload_status=415
    ),
)


# --- pictures ------------------------------------------------------------------------------


def render_picture(picture: Picture) -> bytes:
    """A diagonal two-colour gradient with a solid square in the middle, encoded as asked."""
    rng = np.random.default_rng(picture.seed)
    start, end = rng.integers(0, 256, size=(2, 3))
    ramp = np.add.outer(np.arange(picture.height), np.arange(picture.width))
    t = (ramp / max(1, int(ramp.max())))[..., None]
    pixels = (start * (1 - t) + end * t).astype(np.uint8)
    rows = slice(picture.height // 3, 2 * picture.height // 3)
    cols = slice(picture.width // 3, 2 * picture.width // 3)
    pixels[rows, cols] = 255
    buffer = io.BytesIO()
    Image.fromarray(pixels).save(buffer, format=picture.image_format)
    return buffer.getvalue()


def pixel_digest(encoded: bytes) -> str:
    """SHA-256 of the decoded picture (mode, size and raw pixels); the same on every platform."""
    with Image.open(io.BytesIO(encoded)) as image:
        header = f"{image.mode} {image.width}x{image.height};".encode("ascii")
        return hashlib.sha256(header + image.tobytes()).hexdigest()


def _not_a_picture(_fixture: ArtworkFixture) -> bytes:
    return b"This file only has a picture name; it is plain text written for StemStudio tests.\n"


# --- ID3v2.3 (MP3) -------------------------------------------------------------------------


def _syncsafe(size: int) -> bytes:
    return bytes((size >> shift) & 0x7F for shift in (21, 14, 7, 0))


def _id3_frame(frame_id: str, payload: bytes) -> bytes:
    return frame_id.encode("ascii") + struct.pack(">I", len(payload)) + b"\x00\x00" + payload


def id3_tag(tags: Tags, cover: bytes, media_type: str) -> bytes:
    """An ID3v2.3 tag with TIT2/TPE1/TALB (Latin-1) and an APIC front cover."""
    texts = (("TIT2", tags.title), ("TPE1", tags.artist), ("TALB", tags.album))
    frames = b"".join(_id3_frame(i, ID3_TEXT_LATIN1 + t.encode("latin-1")) for i, t in texts)
    apic = ID3_TEXT_LATIN1 + media_type.encode("ascii") + b"\x00" + bytes([FRONT_COVER]) + b"\x00"
    frames += _id3_frame("APIC", apic + cover)
    return b"ID3\x03\x00\x00" + _syncsafe(len(frames)) + frames


def _write_mp3(fixture: ArtworkFixture, audio: Audio, target: Path) -> None:
    tags, picture = _require_audio(fixture)
    encode_mp3(audio, target)
    tag = id3_tag(tags, render_picture(picture), picture.media_type)
    target.write_bytes(tag + target.read_bytes())


# --- FLAC ----------------------------------------------------------------------------------


def flac_picture_block(cover: bytes, picture: Picture) -> bytes:
    """The body of a FLAC ``PICTURE`` block (front cover, 24-bit colour, no description)."""
    mime = picture.media_type.encode("ascii")
    head = struct.pack(">II", FRONT_COVER, len(mime)) + mime + struct.pack(">I", 0)
    dims = struct.pack(">IIIII", picture.width, picture.height, 24, 0, len(cover))
    return head + dims + cover


def split_flac_metadata(data: bytes) -> tuple[list[tuple[int, bytes]], bytes]:
    """The metadata blocks (type, body) of a FLAC file and the audio frames after them."""
    if data[:4] != b"fLaC":
        raise ArtworkFixtureError("not a FLAC stream")
    blocks: list[tuple[int, bytes]] = []
    offset, last = 4, False
    while not last:
        header = data[offset]
        size = int.from_bytes(data[offset + 1 : offset + 4], "big")
        blocks.append((header & 0x7F, data[offset + 4 : offset + 4 + size]))
        last, offset = bool(header & 0x80), offset + 4 + size
    return blocks, data[offset:]


def join_flac_metadata(blocks: Sequence[tuple[int, bytes]], frames: bytes) -> bytes:
    """Re-assemble a FLAC file; only the final block carries the "last" flag."""
    out = bytearray(b"fLaC")
    for index, (kind, body) in enumerate(blocks):
        flag = 0x80 if index == len(blocks) - 1 else 0
        out += bytes([flag | kind]) + len(body).to_bytes(3, "big") + body
    return bytes(out + frames)


def _write_flac(fixture: ArtworkFixture, audio: Audio, target: Path) -> None:
    tags, picture = _require_audio(fixture)
    with sf.SoundFile(str(target), "w", SAMPLE_RATE, 2, "PCM_16", format="FLAC") as handle:
        handle.title, handle.artist, handle.album = tags.title, tags.artist, tags.album
        handle.write(audio)
    blocks, frames = split_flac_metadata(target.read_bytes())
    block = (FLAC_PICTURE_BLOCK, flac_picture_block(render_picture(picture), picture))
    target.write_bytes(join_flac_metadata([blocks[0], block, *blocks[1:]], frames))


# --- catalogue -----------------------------------------------------------------------------


def _require_audio(fixture: ArtworkFixture) -> tuple[Tags, Picture]:
    if fixture.tags is None or fixture.picture is None:
        raise ArtworkFixtureError(f"{fixture.name}: audio fixtures need tags and a cover")
    return fixture.tags, fixture.picture


AUDIO_WRITERS: dict[str, Callable[[ArtworkFixture, Audio, Path], None]] = {
    ".mp3": _write_mp3,
    ".flac": _write_flac,
}


def write_fixture(fixture: ArtworkFixture, out_dir: Path) -> Path:
    """Write one fixture into ``out_dir``; returns the file path."""
    target = out_dir / fixture.name
    if fixture.kind == "audio-with-cover":
        audio = render_music(Rhythm(100, 4, AUDIO_SECONDS), seed=fixture.seed)
        AUDIO_WRITERS[target.suffix](fixture, audio, target)
    elif fixture.picture is None:
        target.write_bytes(_not_a_picture(fixture))
    else:
        target.write_bytes(render_picture(fixture.picture))
    return target


def _picture_facts(picture: Picture | None) -> dict[str, Any] | None:
    if picture is None:
        return None
    digest = pixel_digest(render_picture(picture))
    return {"mediaType": picture.media_type, "width": picture.width, "height": picture.height,
            "sha256": digest}  # fmt: skip


def manifest_entry(fixture: ArtworkFixture) -> dict[str, Any]:
    """The manifest facts of one fixture (camelCase, as read by the TypeScript E2E suite)."""
    audio = fixture.kind == "audio-with-cover"
    tags = None if fixture.tags is None else vars(fixture.tags)
    return {
        "kind": fixture.kind,
        "format": Path(fixture.name).suffix.lstrip("."),
        "durationSeconds": AUDIO_SECONDS if audio else None,
        "tags": tags,
        "picture": _picture_facts(fixture.picture),
        "expectedArtworkSource": "embedded" if audio else None,
        "uploadStatus": fixture.upload_status,
    }


def generate(out_dir: Path, manifest_path: Path) -> dict[str, Any]:
    """Write every catalogue file into ``out_dir`` and its facts to ``manifest_path``."""
    out_dir.mkdir(parents=True, exist_ok=True)
    entries: dict[str, Any] = {}
    for fixture in CATALOGUE:
        write_fixture(fixture, out_dir)
        entries[fixture.name] = manifest_entry(fixture)
    manifest = {"version": MANIFEST_VERSION, "sampleRate": SAMPLE_RATE, "fixtures": entries}
    return write_manifest(manifest, manifest_path)


def parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    """Parse ``--out-dir`` and ``--manifest`` (defaults: next to this script)."""
    return cli_args(argv, "artwork-manifest.json", __doc__)


def main(argv: Sequence[str] | None = None) -> int:
    """Command-line entry point; returns the process exit code."""
    args = parse_args(argv)
    count = len(generate(args.out_dir, args.manifest)["fixtures"])
    sys.stdout.write(f"wrote {count} artwork fixtures to {args.out_dir}\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
