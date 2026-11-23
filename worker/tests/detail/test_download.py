"""Tests of the first-use model download with SHA-256 verification (T018, FR-018)."""

import hashlib
import io
import urllib.request
from collections.abc import Iterator
from contextlib import contextmanager
from pathlib import Path
from typing import BinaryIO

import pytest

from stemstudio_worker.detail import download, registry
from stemstudio_worker.detail.download import (
    DOWNLOAD_CORRUPT,
    DOWNLOAD_FAILED,
    ModelDownloader,
    open_url,
)
from stemstudio_worker.detail.registry import MODELS, ModelSpec, spec_of
from stemstudio_worker.ports import DetailModelUnavailableError

PAYLOAD = b"weights" * 1000
SPEC = ModelSpec(
    model_id="uvr-mdxnet-kara-2",
    file_name="model.onnx",
    url="https://example.invalid/model.onnx",
    size_bytes=len(PAYLOAD),
    sha256=hashlib.sha256(PAYLOAD).hexdigest(),
    licence="MIT",
)


class FakeOpener:
    """Serves ``payload`` for every URL, or raises ``error``; records the URLs."""

    def __init__(self, payload: bytes = PAYLOAD, error: Exception | None = None) -> None:
        self.payload = payload
        self.error = error
        self.urls: list[str] = []

    @contextmanager
    def __call__(self, url: str) -> Iterator[BinaryIO]:
        self.urls.append(url)
        if self.error is not None:
            raise self.error
        yield io.BytesIO(self.payload)


def _target(tmp_path: Path) -> Path:
    return tmp_path / "detail" / "uvr-mdxnet-kara-2" / "model.onnx"


def test_first_use_downloads_verifies_and_renames(tmp_path: Path) -> None:
    opener = FakeOpener()
    path = ModelDownloader(tmp_path, opener).ensure(SPEC)
    assert path == _target(tmp_path)
    assert path.read_bytes() == PAYLOAD
    assert not path.with_name("model.onnx.part").exists()
    assert opener.urls == [SPEC.url]


def test_an_existing_verified_file_skips_the_network(tmp_path: Path) -> None:
    _target(tmp_path).parent.mkdir(parents=True)
    _target(tmp_path).write_bytes(PAYLOAD)
    opener = FakeOpener(error=AssertionError("network used"))
    assert ModelDownloader(tmp_path, opener).ensure(SPEC) == _target(tmp_path)
    assert opener.urls == []


def test_an_existing_damaged_file_is_downloaded_again(tmp_path: Path) -> None:
    _target(tmp_path).parent.mkdir(parents=True)
    _target(tmp_path).write_bytes(b"truncated")
    opener = FakeOpener()
    assert ModelDownloader(tmp_path, opener).ensure(SPEC).read_bytes() == PAYLOAD
    assert opener.urls == [SPEC.url]


def test_a_checksum_mismatch_deletes_the_download(tmp_path: Path) -> None:
    downloader = ModelDownloader(tmp_path, FakeOpener(payload=b"evil"))
    with pytest.raises(DetailModelUnavailableError) as raised:
        downloader.ensure(SPEC)
    assert str(raised.value) == DOWNLOAD_CORRUPT == "Detail model download was corrupt; try again."
    assert list(_target(tmp_path).parent.iterdir()) == []


@pytest.mark.parametrize("error", [OSError("offline"), TimeoutError("slow")])
def test_a_network_error_is_readable(tmp_path: Path, error: Exception) -> None:
    downloader = ModelDownloader(tmp_path, FakeOpener(error=error))
    with pytest.raises(DetailModelUnavailableError) as raised:
        downloader.ensure(SPEC)
    assert str(raised.value) == DOWNLOAD_FAILED
    assert DOWNLOAD_FAILED == "Detail model could not be downloaded; try again when online."
    assert not _target(tmp_path).exists()


def test_a_broken_stream_removes_the_partial_file(tmp_path: Path) -> None:
    class Broken(io.BytesIO):
        def read(self, size: int | None = -1) -> bytes:
            raise ConnectionResetError("reset")

    @contextmanager
    def opener(url: str) -> Iterator[BinaryIO]:
        yield Broken()

    with pytest.raises(DetailModelUnavailableError, match="try again when online"):
        ModelDownloader(tmp_path, opener).ensure(SPEC)
    assert list(_target(tmp_path).parent.iterdir()) == []


def test_a_download_larger_than_the_pinned_size_is_aborted(tmp_path: Path) -> None:
    """Hostile server: an endless stream stops at the pinned size instead of filling the disk."""
    reads: list[int] = []

    class Endless(io.RawIOBase):
        def read(self, size: int | None = -1) -> bytes:
            reads.append(1)
            if len(reads) > 64:
                raise AssertionError("kept reading past the pinned size")
            return b"x" * (size if size is not None and size > 0 else download._CHUNK)

    @contextmanager
    def opener(url: str) -> Iterator[BinaryIO]:
        yield Endless()  # type: ignore[misc]

    with pytest.raises(DetailModelUnavailableError) as raised:
        ModelDownloader(tmp_path, opener).ensure(SPEC)
    assert str(raised.value) == DOWNLOAD_FAILED
    assert len(reads) <= 2
    assert list(_target(tmp_path).parent.iterdir()) == []


def test_one_byte_over_the_pinned_size_fails_and_the_exact_size_is_kept(tmp_path: Path) -> None:
    small = ModelSpec(
        model_id=SPEC.model_id,
        file_name=SPEC.file_name,
        url=SPEC.url,
        size_bytes=len(PAYLOAD) - 1,
        sha256=SPEC.sha256,
        licence=SPEC.licence,
    )
    with pytest.raises(DetailModelUnavailableError, match="try again when online"):
        ModelDownloader(tmp_path, FakeOpener()).ensure(small)
    assert ModelDownloader(tmp_path, FakeOpener()).ensure(SPEC).read_bytes() == PAYLOAD


def test_open_url_uses_urllib_with_a_timeout(monkeypatch: pytest.MonkeyPatch) -> None:
    seen: list[tuple[str, float]] = []

    def fake_urlopen(url: str, timeout: float) -> io.BytesIO:
        seen.append((url, timeout))
        return io.BytesIO(b"x")

    monkeypatch.setattr(urllib.request, "urlopen", fake_urlopen)
    with open_url("https://example.invalid/m") as stream:
        assert stream.read() == b"x"
    assert seen == [("https://example.invalid/m", download.TIMEOUT_SECONDS)]


def test_registry_lists_the_three_models_of_research_d07() -> None:
    assert [m.model_id for m in MODELS] == [
        "mdx23c-drumsep-6s", "uvr-mdxnet-kara-2", "mvsep-mega-53",
    ]  # fmt: skip
    mega = spec_of("mvsep-mega-53")
    assert mega.size_bytes == 1_368_919_887
    assert mega.sha256 == "c62820893bbf86d4e734f966bd142d9157cfc8bb8e79e9d8f9ea553f3ff3519f"
    assert spec_of("mdx23c-drumsep-6s").file_name == "MDX23C-DrumSep-aufr33-jarredou.ckpt"
    assert spec_of("uvr-mdxnet-kara-2").sha256.startswith("bf32e151")
    assert all(m.url.startswith("https://github.com/") for m in MODELS)
    assert sum(m.size_bytes for m in MODELS) == 1_859_359_312
    assert registry.SEPARATOR_VERSION == "1"


def test_registry_rejects_unknown_ids() -> None:
    with pytest.raises(KeyError):
        spec_of("fake-detail")
