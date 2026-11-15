"""Tests of the transcription model store: pinned URL, SHA-256, first-use download (008 T022)."""

import hashlib
import io
from contextlib import AbstractContextManager, nullcontext
from pathlib import Path
from typing import IO

import pytest

from stemstudio_worker.ports import WeightsFile
from stemstudio_worker.transcription.models import (
    MODEL_DOWNLOAD_TEXT,
    ModelStore,
)

DATA = b"model bytes"
WEIGHTS = WeightsFile(
    model_id="basic-pitch-icassp2022", url="https://example.invalid/saved/nmp.onnx",
    sha256=hashlib.sha256(DATA).hexdigest(), size_bytes=len(DATA), licence="Apache-2.0",
)  # fmt: skip


class Opener:
    """Serves ``data`` (or raises ``error``) and records the opened URLs."""

    def __init__(self, data: bytes = DATA, error: Exception | None = None) -> None:
        self.data = data
        self.error = error
        self.urls: list[str] = []

    def __call__(self, url: str) -> AbstractContextManager[IO[bytes]]:
        self.urls.append(url)
        if self.error is not None:
            raise self.error
        return nullcontext(io.BytesIO(self.data))


def test_the_first_use_downloads_into_the_model_dir(tmp_path: Path) -> None:
    opener = Opener()

    path = ModelStore(tmp_path, opener).ensure(WEIGHTS)

    assert path == tmp_path / "transcription" / "basic-pitch-icassp2022" / "nmp.onnx"
    assert path.read_bytes() == DATA
    assert opener.urls == [WEIGHTS.url]
    assert list(path.parent.iterdir()) == [path]


def test_a_verified_file_is_not_downloaded_again(tmp_path: Path) -> None:
    opener = Opener()
    store = ModelStore(tmp_path, opener)
    store.ensure(WEIGHTS)

    store.ensure(WEIGHTS)

    assert len(opener.urls) == 1
    assert store.ready(WEIGHTS)


def test_a_corrupt_file_is_replaced(tmp_path: Path) -> None:
    store = ModelStore(tmp_path, Opener())
    target = tmp_path / "transcription" / "basic-pitch-icassp2022" / "nmp.onnx"
    target.parent.mkdir(parents=True)
    target.write_bytes(b"tampered")

    assert not store.ready(WEIGHTS)
    assert store.ensure(WEIGHTS).read_bytes() == DATA


@pytest.mark.parametrize("opener", [Opener(b"other bytes"), Opener(error=OSError("offline"))])
def test_a_failed_or_mismatching_download_leaves_nothing(tmp_path: Path, opener: Opener) -> None:
    store = ModelStore(tmp_path, opener)

    with pytest.raises(Exception, match=MODEL_DOWNLOAD_TEXT):
        store.ensure(WEIGHTS)

    assert list((tmp_path / "transcription" / "basic-pitch-icassp2022").iterdir()) == []
    assert not store.ready(WEIGHTS)
