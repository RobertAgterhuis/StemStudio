"""``ImageNormalizer`` implementation with Pillow (FR-007, research R-03, ADR-014 D5).

Every picture is decoded and re-encoded, so nothing of the original file survives except its
pixels: JPEG/PNG/WebP only, at most 40 megapixels (checked from the header, before decoding),
first frame of an animation, EXIF orientation applied, alpha flattened on black, centre-cropped
to a square and written as a 600 px cover and a 160 px thumbnail JPEG without EXIF or ICC data.
The JPEG quality is lowered step by step when a picture would exceed the size limits.
"""

import hashlib
import io
import logging
from collections.abc import Iterator
from contextlib import contextmanager
from typing import Final

from PIL import Image, ImageOps, UnidentifiedImageError

from stemstudio_worker.metadata.http import OnlineClient, OnlineRequestError
from stemstudio_worker.ports import (
    ArtworkCandidate,
    ArtworkFetchError,
    ArtworkImages,
    ImageTooLargeError,
    NotAnImageError,
    UnsupportedImageTypeError,
)
from stemstudio_worker.schemas import COVER_MAX_BYTES, THUMB_MAX_BYTES

COVER_SIZE: Final = 600
"""Edge of the cover JPEG in pixels."""

THUMB_SIZE: Final = 160
"""Edge of the thumbnail JPEG in pixels."""

COVER_QUALITY: Final = 85
"""Starting JPEG quality of the cover."""

THUMB_QUALITY: Final = 80
"""Starting JPEG quality of the thumbnail."""

MIN_QUALITY: Final = 20
"""Lowest JPEG quality tried before giving up on the size limit."""

MAX_PIXELS: Final = 40_000_000
"""Largest accepted picture (width x height)."""

ALLOWED_FORMATS: Final = frozenset({"JPEG", "PNG", "WEBP"})
"""Pillow format names that are accepted."""

_QUALITY_STEP: Final = 10
_FETCH_FAILED: Final = "The picture could not be downloaded."
_NOT_AN_IMAGE: Final = "The file is not a readable picture."
_UNSUPPORTED: Final = "Only JPEG, PNG and WebP pictures are supported."
_TOO_LARGE: Final = "The picture has more than 40 megapixels."
_LOG = logging.getLogger(__name__)


class PillowImageNormalizer:
    """Fetches (URL candidates) and re-encodes pictures into the cover and thumbnail JPEGs."""

    def __init__(self, client: OnlineClient | None = None) -> None:
        """Create the normaliser.

        Args:
            client: Allow-listed online client used for URL candidates; without one, URL
                candidates fail with ``ArtworkFetchError``.
        """
        self._client = client

    def normalize(self, candidate: ArtworkCandidate) -> ArtworkImages:
        """Normalise ``candidate`` (see ``ports.ImageNormalizer.normalize``).

        Raises:
            ArtworkFetchError: A remote picture could not be downloaded.
            NotAnImageError: The bytes cannot be decoded.
            UnsupportedImageTypeError: The picture is not JPEG, PNG or WebP.
            ImageTooLargeError: The picture has more than 40 megapixels.
        """
        data = candidate.data if candidate.data is not None else self._download(candidate.url)
        with _opened(data) as image:
            square = _square(_flattened(image))
        cover = _jpeg(square, COVER_SIZE, COVER_QUALITY, COVER_MAX_BYTES)
        thumb = _jpeg(square, THUMB_SIZE, THUMB_QUALITY, THUMB_MAX_BYTES)
        return ArtworkImages(cover=cover, thumb=thumb, sha256=hashlib.sha256(cover).hexdigest())

    def _download(self, url: str | None) -> bytes:
        """Download a URL candidate through the allow-listed client."""
        if self._client is None or url is None:
            raise ArtworkFetchError(_FETCH_FAILED)
        try:
            response = self._client.get(url)
        except OnlineRequestError as exc:
            _LOG.info("Picture download failed: %s", exc)
            raise ArtworkFetchError(_FETCH_FAILED) from None
        if response is None:
            raise ArtworkFetchError(_FETCH_FAILED)
        return response.content


@contextmanager
def _opened(data: bytes) -> Iterator[Image.Image]:
    """Open ``data`` lazily, check format and pixel count, then decode the first frame."""
    try:
        image = Image.open(io.BytesIO(data))
    except (UnidentifiedImageError, OSError, ValueError):
        raise NotAnImageError(_NOT_AN_IMAGE) from None
    with image:
        _check_header(image)
        _decode_first_frame(image)
        yield image


def _check_header(image: Image.Image) -> None:
    """Reject other formats and pictures over ``MAX_PIXELS`` before any pixel is decoded."""
    if image.format not in ALLOWED_FORMATS:
        raise UnsupportedImageTypeError(_UNSUPPORTED)
    if image.width * image.height > MAX_PIXELS:
        raise ImageTooLargeError(_TOO_LARGE)


def _decode_first_frame(image: Image.Image) -> None:
    """Decode frame 0; truncated or corrupt data raises ``NotAnImageError``."""
    try:
        image.seek(0)
        image.load()
    except (OSError, ValueError, SyntaxError, Image.DecompressionBombError):
        raise NotAnImageError(_NOT_AN_IMAGE) from None


def _flattened(image: Image.Image) -> Image.Image:
    """Apply the EXIF orientation and return an RGB copy with alpha flattened on black."""
    oriented = ImageOps.exif_transpose(image)
    if not _has_alpha(oriented):
        return oriented.convert("RGB")
    rgba = oriented.convert("RGBA")
    background = Image.new("RGB", rgba.size, (0, 0, 0))
    background.paste(rgba, mask=rgba.getchannel("A"))
    return background


def _has_alpha(image: Image.Image) -> bool:
    """True for modes with an alpha channel and palette pictures with a transparent colour."""
    return image.mode in ("RGBA", "LA", "PA") or "transparency" in image.info


def _square(image: Image.Image) -> Image.Image:
    """Centre-crop ``image`` to a square."""
    edge = min(image.size)
    left = (image.width - edge) // 2
    top = (image.height - edge) // 2
    return image.crop((left, top, left + edge, top + edge))


def _jpeg(square: Image.Image, edge: int, quality: int, max_bytes: int) -> bytes:
    """Resize to ``edge`` px and encode as JPEG, lowering the quality until it fits."""
    resized = square.resize((edge, edge), Image.Resampling.LANCZOS)
    resized.info = {}
    encoded = _encode(resized, quality)
    while len(encoded) > max_bytes and quality > MIN_QUALITY:
        quality -= _QUALITY_STEP
        encoded = _encode(resized, quality)
    return encoded


def _encode(image: Image.Image, quality: int) -> bytes:
    """Encode ``image`` as an optimised baseline JPEG without metadata."""
    buffer = io.BytesIO()
    image.save(buffer, "JPEG", quality=quality, optimize=True, exif=b"")
    return buffer.getvalue()
