"""The two JPEG files of a normalised picture: ``<dir>/cover.jpg`` and ``<dir>/thumb.jpg``."""

import shutil
from collections.abc import Mapping
from pathlib import Path

from stemstudio_worker.ports import ArtworkImages
from stemstudio_worker.schemas import ArtworkVariant


def artwork_file(directory: Path, variant: ArtworkVariant) -> Path:
    """Path of one variant below ``directory`` (fixed file names, never remote data)."""
    return directory / f"{variant}.jpg"


def write_artwork(
    directory: Path, images: ArtworkImages, extra: Mapping[str, str] | None = None
) -> None:
    """Write both JPEGs (and ``extra`` text files) into ``directory`` atomically.

    The files go into a sibling temporary directory that is renamed last, so readers never see
    a half-written picture.
    """
    temp = directory.with_name(directory.name + ".tmp")
    shutil.rmtree(temp, ignore_errors=True)
    temp.mkdir(parents=True)
    artwork_file(temp, "cover").write_bytes(images.cover)
    artwork_file(temp, "thumb").write_bytes(images.thumb)
    for name, text in (extra or {}).items():
        (temp / name).write_text(text, encoding="utf-8")
    shutil.rmtree(directory, ignore_errors=True)
    temp.replace(directory)
