"""Tests of the transcription model store: pinned URL, SHA-256, first-use download (008 T022)."""

import hashlib
import io
import urllib.request
from contextlib import AbstractContextManager, nullcontext
from pathlib import Path
from typing import IO

import pytest

from stemstudio_worker.ports import WeightsFile
from stemstudio_worker.transcription import models
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


class CountingStream(io.BytesIO):
    """A response body that records how many bytes were read from it."""

    def __init__(self, data: bytes) -> None:
        super().__init__(data)
        self.served = 0

    def read(self, size: int | None = -1) -> bytes:
        block = super().read(size)
        self.served += len(block)
        return block


def test_a_download_larger_than_the_pinned_size_is_aborted(tmp_path: Path) -> None:
    # Size cap (016 security review): a hostile or broken server cannot fill the disk.
    stream = CountingStream(DATA + b"x" * (5 * models._BLOCK))
    store = ModelStore(tmp_path, lambda _url: nullcontext(stream))

    with pytest.raises(Exception, match=MODEL_DOWNLOAD_TEXT):
        store.ensure(WEIGHTS)

    assert stream.served <= WEIGHTS.size_bytes + models._BLOCK
    assert list((tmp_path / "transcription" / "basic-pitch-icassp2022").iterdir()) == []


def test_a_download_of_exactly_the_pinned_size_is_kept(tmp_path: Path) -> None:
    assert ModelStore(tmp_path, Opener()).ensure(WEIGHTS).read_bytes() == DATA


def test_ready_hashes_an_unchanged_file_once(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    # /health and every job ask ready(); re-hashing two 12 MB checkpoints each time is waste.
    store = ModelStore(tmp_path, Opener())
    store.ensure(WEIGHTS)
    hashed: list[Path] = []
    digest = models._file_digest

    def counting(path: Path) -> str | None:
        hashed.append(path)
        return digest(path)

    monkeypatch.setattr(models, "_file_digest", counting)

    assert store.ready(WEIGHTS) and store.ready(WEIGHTS)

    assert len(hashed) <= 1


def test_ready_hashes_again_after_the_file_changed(tmp_path: Path) -> None:
    store = ModelStore(tmp_path, Opener())
    path = store.ensure(WEIGHTS)
    assert store.ready(WEIGHTS)

    path.write_bytes(b"tampered and longer")

    assert not store.ready(WEIGHTS)
    assert store.ensure(WEIGHTS).read_bytes() == DATA
    assert store.ready(WEIGHTS)


def test_an_unreadable_cache_entry_is_not_ready(tmp_path: Path) -> None:
    store = ModelStore(tmp_path, Opener())
    store.path(WEIGHTS).mkdir(parents=True)  # exists, but cannot be read as a file

    assert not store.ready(WEIGHTS)
    assert not store.ready(WEIGHTS)


def test_the_default_opener_uses_urllib_with_a_timeout(monkeypatch: pytest.MonkeyPatch) -> None:
    calls: list[tuple[str, float]] = []

    def urlopen(url: str, timeout: float) -> io.BytesIO:
        calls.append((url, timeout))
        return io.BytesIO(DATA)

    monkeypatch.setattr(urllib.request, "urlopen", urlopen)

    with models.https_open(WEIGHTS.url) as stream:
        assert stream.read() == DATA
    assert calls == [(WEIGHTS.url, 60.0)]
