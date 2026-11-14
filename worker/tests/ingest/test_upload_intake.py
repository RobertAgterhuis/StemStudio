"""T049: upload intake (extension check, source key from bytes, title from file name)."""

import hashlib
from pathlib import Path

import pytest

from stemstudio_worker.ingest.upload import FileUploadIntake
from stemstudio_worker.ports import InvalidSourceError
from stemstudio_worker.schemas import SourceType

CONTENT = b"ID3 fake audio bytes"
DIGEST = hashlib.sha256(CONTENT).hexdigest()


def _stored(tmp_path: Path, content: bytes = CONTENT) -> Path:
    path = tmp_path / "input.bin"
    path.write_bytes(content)
    return path


@pytest.mark.parametrize(
    "file_name", ["song.mp3", "SONG.MP3", "song.Wav", "song.wav", "song.flac", "song.FLaC"]
)
def test_accepts_supported_extensions_case_insensitively(tmp_path: Path, file_name: str) -> None:
    info = FileUploadIntake().accept(file_name, _stored(tmp_path))

    assert info.source_type is SourceType.UPLOAD
    assert info.title == "song"


@pytest.mark.parametrize(
    "file_name",
    ["song.ogg", "song.m4a", "song", "song.mp3.exe", "song.", "mp3", "song.flac ", ""],
)
def test_rejects_other_extensions(tmp_path: Path, file_name: str) -> None:
    with pytest.raises(InvalidSourceError, match=r"MP3, WAV (and|or) FLAC"):
        FileUploadIntake().accept(file_name, _stored(tmp_path))


def test_source_key_is_sha256_of_the_stored_bytes(tmp_path: Path) -> None:
    info = FileUploadIntake().accept("My Song.mp3", _stored(tmp_path))

    assert info.source_key == f"upload:{DIGEST}"
    assert info.artist is None
    assert info.youtube_url is None


def test_same_bytes_under_another_name_give_the_same_key(tmp_path: Path) -> None:
    intake = FileUploadIntake()
    first = intake.accept("a.mp3", _stored(tmp_path))
    second = intake.accept("b.FLAC", _stored(tmp_path))

    assert first.source_key == second.source_key


@pytest.mark.parametrize(
    ("file_name", "title"),
    [
        ("My Song.mp3", "My Song"),
        ("my.song.v2.wav", "my.song.v2"),
        ("../../etc/My Song.mp3", "My Song"),
        ("C:\\fakepath\\My Song.flac", "My Song"),
        ("  spaced  .mp3", "spaced"),
        ("bad\x00na\nme\r.mp3", "badname"),
        (".mp3", "Untitled upload"),
        ("..\\.flac", "Untitled upload"),
    ],
)
def test_title_is_the_base_name_without_extension(
    tmp_path: Path, file_name: str, title: str
) -> None:
    assert FileUploadIntake().accept(file_name, _stored(tmp_path)).title == title


def test_title_is_truncated_to_the_metadata_limit(tmp_path: Path) -> None:
    info = FileUploadIntake().accept("x" * 600 + ".mp3", _stored(tmp_path))

    assert info.title == "x" * 500


def test_file_name_is_never_used_as_a_path(tmp_path: Path) -> None:
    decoy = tmp_path / "decoy.mp3"
    decoy.write_bytes(b"other bytes")
    before = sorted(tmp_path.iterdir())

    info = FileUploadIntake().accept(str(decoy), _stored(tmp_path))

    assert info.source_key == f"upload:{DIGEST}"
    assert sorted(tmp_path.iterdir()) == sorted([*before, tmp_path / "input.bin"])
    assert decoy.read_bytes() == b"other bytes"


def test_rejects_empty_files(tmp_path: Path) -> None:
    with pytest.raises(InvalidSourceError, match="empty"):
        FileUploadIntake().accept("song.mp3", _stored(tmp_path, b""))


def test_rejects_files_over_the_configured_limit(tmp_path: Path) -> None:
    intake = FileUploadIntake(max_upload_bytes=len(CONTENT) - 1)

    with pytest.raises(InvalidSourceError, match="larger than"):
        intake.accept("song.mp3", _stored(tmp_path))


def test_accepts_files_exactly_at_the_limit(tmp_path: Path) -> None:
    intake = FileUploadIntake(max_upload_bytes=len(CONTENT))

    assert intake.accept("song.mp3", _stored(tmp_path)).source_key == f"upload:{DIGEST}"


def test_default_limit_is_200_mib() -> None:
    assert FileUploadIntake().max_upload_bytes == 209_715_200
