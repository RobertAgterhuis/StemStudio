"""The two JPEG files of a normalised picture: ``<dir>/cover.jpg`` and ``<dir>/thumb.jpg``."""

import shutil
from pathlib import Path

from stemstudio_worker.ports import ArtworkImages
from stemstudio_worker.schemas import ArtworkVariant


def artwork_file(directory: Path, variant: ArtworkVariant) -> Path:
    """Path of one variant below ``directory`` (fixed file names, never remote data)."""
    return directory / f"{variant}.jpg"


def write_artwork(directory: Path, images: ArtworkImages) -> None:
    """Write both JPEGs into ``directory``: into a sibling temporary directory, renamed last."""
    temp = directory.with_name(directory.name + ".tmp")
    shutil.rmtree(temp, ignore_errors=True)
    temp.mkdir(parents=True)
    artwork_file(temp, "cover").write_bytes(images.cover)
    artwork_file(temp, "thumb").write_bytes(images.thumb)
    shutil.rmtree(directory, ignore_errors=True)
    temp.replace(directory)
