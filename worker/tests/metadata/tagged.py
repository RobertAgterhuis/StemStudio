"""Tagged audio files built in the tests (ID3v2.3 MP3, FLAC with a PICTURE block, plain WAV)."""

import struct
from pathlib import Path

import numpy as np
import soundfile as sf

FRONT_COVER = 3
OTHER_PICTURE = 0


def _synchsafe(value: int) -> bytes:
    return bytes((value >> shift) & 0x7F for shift in (21, 14, 7, 0))


def _id3_frame(frame_id: str, data: bytes) -> bytes:
    return frame_id.encode() + struct.pack(">I", len(data)) + b"\x00\x00" + data


def _id3_text(frame_id: str, value: str) -> bytes:
    return _id3_frame(frame_id, b"\x01" + value.encode("utf-16"))


def write_mp3(
    path: Path, tags: dict[str, str], picture: bytes | None = None, kind: int = FRONT_COVER
) -> Path:
    """An MP3 file holding only an ID3v2.3 tag (title/artist/album, optional APIC)."""
    names = {"title": "TIT2", "artist": "TPE1", "album": "TALB"}
    body = b"".join(_id3_text(names[key], value) for key, value in tags.items())
    if picture is not None:
        body += _id3_frame("APIC", b"\x00image/jpeg\x00" + bytes([kind]) + b"\x00" + picture)
    path.write_bytes(b"ID3\x03\x00\x00" + _synchsafe(len(body)) + body + b"\x00" * 128)
    return path


def _flac_block(block_type: int, data: bytes, last: bool) -> bytes:
    header = (0x80 if last else 0) | block_type
    return bytes([header]) + len(data).to_bytes(3, "big") + data


def _vorbis_comment(tags: dict[str, str]) -> bytes:
    vendor = b"stemstudio-tests"
    entries = [f"{key.upper()}={value}".encode() for key, value in tags.items()]
    out = struct.pack("<I", len(vendor)) + vendor + struct.pack("<I", len(entries))
    return out + b"".join(struct.pack("<I", len(entry)) + entry for entry in entries)


def _picture(data: bytes, kind: int) -> bytes:
    mime = b"image/png"
    fields = struct.pack(">I", kind) + struct.pack(">I", len(mime)) + mime + struct.pack(">I", 0)
    return fields + struct.pack(">IIIII", 0, 0, 0, 0, len(data)) + data


def write_flac(
    path: Path, tags: dict[str, str], picture: bytes | None = None, kind: int = FRONT_COVER
) -> Path:
    """A short silent FLAC file with a VORBIS_COMMENT block and an optional PICTURE block."""
    sf.write(path, np.zeros((441, 2), dtype=np.float32), 44_100, format="FLAC")
    raw = path.read_bytes()
    streaminfo = raw[8:42]
    frames = _audio_frames(raw)
    blocks = [(4, _vorbis_comment(tags))]
    if picture is not None:
        blocks.append((6, _picture(picture, kind)))
    encoded = _flac_block(0, streaminfo, last=False)
    for index, (block_type, data) in enumerate(blocks):
        encoded += _flac_block(block_type, data, last=index == len(blocks) - 1)
    path.write_bytes(b"fLaC" + encoded + frames)
    return path


def _audio_frames(raw: bytes) -> bytes:
    """The audio frames after the metadata blocks of a FLAC file."""
    offset = 4
    while True:
        header = raw[offset]
        length = int.from_bytes(raw[offset + 1 : offset + 4], "big")
        offset += 4 + length
        if header & 0x80:
            return raw[offset:]


def write_wav(path: Path) -> Path:
    """A short silent WAV file (no tags)."""
    sf.write(path, np.zeros((441, 2), dtype=np.float32), 44_100, format="WAV")
    return path
