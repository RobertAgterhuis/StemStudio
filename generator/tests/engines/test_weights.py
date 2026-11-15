"""Pinned weights in the model cache: REVISION + SHA256SUMS, verification, download (009 T034)."""

import hashlib
import sys
from pathlib import Path

import pytest

from stemstudio_generator.engines.weights import (
    DOWNLOAD_STAGE,
    WeightsDir,
    file_sha256,
    hub_fetch,
    hub_listing,
    write_manifest,
)
from stemstudio_generator.ports import ModelDownloadError, WeightsPin

_A = b"alpha weights"
_B = b"beta weights"
PIN = WeightsPin(
    "org/model", "rev1", (("a/model.bin", hashlib.sha256(_A).hexdigest()), ("b.bin", ""))
)


def _cache(root: Path, revision: str = "rev1", a: bytes = _A) -> WeightsDir:
    (root / "a").mkdir(parents=True, exist_ok=True)
    (root / "a" / "model.bin").write_bytes(a)
    (root / "b.bin").write_bytes(_B)
    write_manifest(root, revision)
    return WeightsDir(root, PIN)


def test_a_cache_with_the_pinned_revision_and_hashes_is_present(tmp_path: Path) -> None:
    cache = _cache(tmp_path)

    assert cache.present()
    assert cache.file_present("a/model.bin")
    assert (tmp_path / "REVISION").read_text("utf-8").strip() == "rev1"
    assert f"{hashlib.sha256(_B).hexdigest()}  b.bin" in (tmp_path / "SHA256SUMS").read_text()


def test_another_revision_is_not_present(tmp_path: Path) -> None:
    assert not _cache(tmp_path, revision="rev0").present()


def test_a_missing_directory_is_not_present(tmp_path: Path) -> None:
    cache = WeightsDir(tmp_path / "none", PIN)

    assert not cache.present()
    assert not cache.file_present("a/model.bin")


def test_a_manifest_hash_other_than_the_pin_is_not_present(tmp_path: Path) -> None:
    assert not _cache(tmp_path, a=b"tampered").present()


def test_a_missing_file_is_not_present(tmp_path: Path) -> None:
    cache = _cache(tmp_path)
    (tmp_path / "b.bin").unlink()

    assert not cache.present()


def test_revision_files_with_one_line_per_file_are_accepted(tmp_path: Path) -> None:
    cache = _cache(tmp_path)
    (tmp_path / "REVISION").write_text("a/model.bin org/model rev1\nb.bin org/model rev1\n")

    assert cache.present()


def test_verify_hashes_the_pinned_files_once(tmp_path: Path) -> None:
    cache = _cache(tmp_path)

    cache.verify()
    (tmp_path / "a" / "model.bin").write_bytes(b"changed after the first check")
    cache.verify()


def test_verify_fails_on_a_changed_file(tmp_path: Path) -> None:
    cache = _cache(tmp_path)
    (tmp_path / "a" / "model.bin").write_bytes(b"corrupted")

    with pytest.raises(ModelDownloadError, match="checksum"):
        cache.verify()


def test_ensure_downloads_a_missing_cache_with_byte_progress(tmp_path: Path) -> None:
    root = tmp_path / "cache"
    calls: list[tuple[str, str, str]] = []

    def fetch(repo: str, revision: str, filename: str, dest: Path) -> None:
        calls.append((repo, revision, filename))
        (dest / filename).parent.mkdir(parents=True, exist_ok=True)
        (dest / filename).write_bytes(_A if filename == "a/model.bin" else _B)

    listing = [("a/model.bin", len(_A)), ("b.bin", len(_B))]
    cache = WeightsDir(root, PIN, fetch=fetch, listing=lambda repo, revision: listing)
    progress: list[tuple[float, str]] = []

    cache.ensure(lambda fraction, stage: progress.append((fraction, stage)))

    assert calls == [("org/model", "rev1", "a/model.bin"), ("org/model", "rev1", "b.bin")]
    assert cache.present()
    assert progress[0] == (0.0, DOWNLOAD_STAGE.format(done=0.0, total=0.0))
    assert progress[-1][0] == pytest.approx(1.0)


def test_ensure_does_nothing_when_present(tmp_path: Path) -> None:
    def fetch(repo: str, revision: str, filename: str, dest: Path) -> None:
        pytest.fail("must not download")

    cache = _cache(tmp_path)
    cache = WeightsDir(tmp_path, PIN, fetch=fetch, listing=lambda repo, revision: [])

    cache.ensure(lambda fraction, stage: None)


def test_a_failed_download_is_a_model_download_error(tmp_path: Path) -> None:
    def fetch(repo: str, revision: str, filename: str, dest: Path) -> None:
        raise OSError("connection reset")

    cache = WeightsDir(
        tmp_path / "c", PIN, fetch=fetch, listing=lambda repo, revision: [("b.bin", 1)]
    )

    with pytest.raises(ModelDownloadError, match="could not be downloaded"):
        cache.ensure(lambda fraction, stage: None)


def test_a_download_with_wrong_hashes_fails_verification(tmp_path: Path) -> None:
    def fetch(repo: str, revision: str, filename: str, dest: Path) -> None:
        (dest / filename).parent.mkdir(parents=True, exist_ok=True)
        (dest / filename).write_bytes(b"wrong")

    listing = [("a/model.bin", 5), ("b.bin", 5)]
    cache = WeightsDir(tmp_path / "c", PIN, fetch=fetch, listing=lambda repo, revision: listing)

    with pytest.raises(ModelDownloadError, match="checksum"):
        cache.ensure(lambda fraction, stage: None)


def test_file_sha256_streams_the_file(tmp_path: Path) -> None:
    path = tmp_path / "x.bin"
    path.write_bytes(b"x" * 3_000_000)

    assert file_sha256(path) == hashlib.sha256(b"x" * 3_000_000).hexdigest()


def test_the_default_hub_adapters_use_huggingface_hub(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    calls: list[object] = []

    class Sibling:
        def __init__(self, name: str, size: int | None) -> None:
            self.rfilename, self.size = name, size

    class Api:
        def model_info(self, repo: str, revision: str, files_metadata: bool) -> object:
            calls.append(("info", repo, revision, files_metadata))
            return type("Info", (), {"siblings": [Sibling("a.bin", 3), Sibling("b", None)]})()

    def hf_hub_download(**kwargs: object) -> str:
        calls.append(("download", kwargs))
        return "ignored"

    module = type(sys)("huggingface_hub")
    module.HfApi = Api  # type: ignore[attr-defined]
    module.hf_hub_download = hf_hub_download  # type: ignore[attr-defined]
    monkeypatch.setitem(sys.modules, "huggingface_hub", module)

    assert hub_listing("org/m", "r1") == [("a.bin", 3), ("b", 0)]
    hub_fetch("org/m", "r1", "a.bin", tmp_path)
    assert calls[-1] == (
        "download",
        {"repo_id": "org/m", "revision": "r1", "filename": "a.bin", "local_dir": str(tmp_path)},
    )
