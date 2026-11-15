"""Tests of the store of normalised user pictures, kept for one hour (005 T044, FR-013)."""

import json
from datetime import datetime, timedelta
from pathlib import Path

from stemstudio_worker.jobs.paths import DataPaths
from stemstudio_worker.jobs.user_artwork import UserArtworkStore

from .artwork_fakes import images_for
from .conftest import T0


class Clock:
    def __init__(self) -> None:
        self.now = T0

    def __call__(self) -> datetime:
        return self.now


def _store(tmp_path: Path) -> tuple[UserArtworkStore, Clock, DataPaths]:
    clock, paths = Clock(), DataPaths(tmp_path)
    return UserArtworkStore(paths, clock), clock, paths


def test_add_stores_both_files_and_returns_a_user_upload(tmp_path: Path) -> None:
    store, _, paths = _store(tmp_path)

    upload = store.add(images_for("user"))

    assert upload.info == images_for("user").info("user")
    folder = paths.user_artwork(upload.artwork_id)
    assert (folder / "cover.jpg").read_bytes() == b"cover:user"
    assert store.info(upload.artwork_id) == upload.info
    assert store.directory(upload.artwork_id) == folder
    assert json.loads((folder / "info.json").read_text())["createdAt"].startswith("2026-11-14")


def test_unknown_damaged_or_invalid_ids_have_no_info(tmp_path: Path) -> None:
    store, _, paths = _store(tmp_path)
    upload = store.add(images_for("user"))
    (paths.user_artwork(upload.artwork_id) / "info.json").write_text("{", encoding="utf-8")

    assert store.info(upload.artwork_id) is None
    assert store.info("0" * 32) is None
    assert store.info("../jobs") is None


def test_sweep_removes_pictures_older_than_one_hour(tmp_path: Path) -> None:
    store, clock, paths = _store(tmp_path)
    old = store.add(images_for("user")).artwork_id
    clock.now = T0 + timedelta(minutes=30)
    young = store.add(images_for("user")).artwork_id
    clock.now = T0 + timedelta(hours=1, seconds=1)

    assert store.sweep() == [old]

    assert not paths.user_artwork(old).exists()
    assert store.info(young) is not None


def test_sweep_removes_damaged_entries_and_ignores_stray_files(tmp_path: Path) -> None:
    store, _, paths = _store(tmp_path)
    damaged = store.add(images_for("user")).artwork_id
    (paths.user_artwork(damaged) / "info.json").unlink()
    (paths.user_artwork_root / "stray.txt").write_text("x", encoding="utf-8")

    assert store.sweep() == [damaged]
    assert (paths.user_artwork_root / "stray.txt").exists()


def test_sweep_of_an_empty_store_is_a_no_op(tmp_path: Path) -> None:
    store, _, _ = _store(tmp_path)

    assert store.sweep() == []
