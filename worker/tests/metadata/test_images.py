"""005:T012: Pillow normaliser with generated images (FR-007, research R-03, ADR-014 D5)."""

import hashlib
import io

import httpx
import pytest
from PIL import Image

from stemstudio_worker.metadata.images import (
    COVER_SIZE,
    MAX_PIXELS,
    THUMB_SIZE,
    PillowImageNormalizer,
)
from stemstudio_worker.ports import (
    ArtworkCandidate,
    ArtworkFetchError,
    ImageNormalizer,
    ImageTooLargeError,
    NotAnImageError,
    UnsupportedImageTypeError,
)
from stemstudio_worker.schemas import COVER_MAX_BYTES, THUMB_MAX_BYTES

from .fakes import online_harness
from .images import (
    animated,
    close_to,
    encode,
    halves,
    noise,
    pixel,
    png_with_header_size,
    solid,
)

RED = (220, 30, 30)
GREEN = (30, 200, 30)
BLUE = (30, 30, 220)
BLACK = (0, 0, 0)
THUMB_URL = "https://i.ytimg.com/vi/dQw4w9WgXcQ/maxresdefault.jpg"


def _embedded(data: bytes) -> ArtworkCandidate:
    return ArtworkCandidate(source="embedded", data=data)


def _normalise(data: bytes) -> tuple[Image.Image, Image.Image, bytes, bytes, str]:
    images = PillowImageNormalizer().normalize(_embedded(data))
    cover = Image.open(io.BytesIO(images.cover))
    thumb = Image.open(io.BytesIO(images.thumb))
    return cover, thumb, images.cover, images.thumb, images.sha256


def test_is_an_image_normalizer() -> None:
    normalizer: ImageNormalizer = PillowImageNormalizer()

    assert normalizer is not None
    assert (COVER_SIZE, THUMB_SIZE, MAX_PIXELS) == (600, 160, 40_000_000)


@pytest.mark.parametrize("image_format", ["JPEG", "PNG", "WEBP"])
def test_jpeg_png_and_webp_become_two_square_jpegs(image_format: str) -> None:
    cover, thumb, cover_bytes, thumb_bytes, sha = _normalise(solid((800, 800), RED, image_format))

    assert (cover.format, cover.size) == ("JPEG", (600, 600))
    assert (thumb.format, thumb.size) == ("JPEG", (160, 160))
    assert cover.mode == thumb.mode == "RGB"
    assert len(cover_bytes) <= COVER_MAX_BYTES
    assert len(thumb_bytes) <= THUMB_MAX_BYTES
    assert sha == hashlib.sha256(cover_bytes).hexdigest()
    assert close_to(pixel(cover_bytes, (300, 300)), RED)


def test_high_entropy_pictures_still_fit_the_size_limits() -> None:
    _, _, cover_bytes, thumb_bytes, _ = _normalise(noise((700, 700)))

    assert len(cover_bytes) <= COVER_MAX_BYTES
    assert len(thumb_bytes) <= THUMB_MAX_BYTES


def test_tiny_picture_is_scaled_up() -> None:
    cover, thumb, *_ = _normalise(solid((1, 1), GREEN, "PNG"))

    assert cover.size == (600, 600)
    assert thumb.size == (160, 160)


def test_output_is_deterministic() -> None:
    data = solid((300, 300), BLUE)

    assert _normalise(data)[4] == _normalise(data)[4]


def test_exif_orientation_is_applied_then_stripped() -> None:
    picture = halves((400, 200), RED, BLUE)
    exif = Image.Exif()
    exif[0x0112] = 6  # rotate 90 degrees clockwise for display
    exif[0x010F] = "CameraMaker"
    data = encode(picture, "JPEG", exif=exif.tobytes())

    cover, thumb, cover_bytes, *_ = _normalise(data)

    assert close_to(pixel(cover_bytes, (300, 40)), RED)
    assert close_to(pixel(cover_bytes, (300, 560)), BLUE)
    for image in (cover, thumb):
        assert len(image.getexif()) == 0
        assert "exif" not in image.info
        assert "icc_profile" not in image.info


def test_icc_profile_and_text_chunks_are_not_copied() -> None:
    data = encode(Image.new("RGB", (64, 64), RED), "PNG", icc_profile=b"profile")

    cover, *_ = _normalise(data)

    assert "icc_profile" not in cover.info


@pytest.mark.parametrize(
    "image",
    [
        Image.new("RGBA", (64, 64), (255, 0, 0, 0)),
        Image.new("LA", (64, 64), (255, 0)),
    ],
    ids=["RGBA", "LA"],
)
def test_transparency_is_flattened_on_black(image: Image.Image) -> None:
    _, _, cover_bytes, *_ = _normalise(encode(image, "PNG"))

    assert close_to(pixel(cover_bytes, (300, 300)), BLACK, tolerance=8)


def test_palette_transparency_is_flattened_on_black() -> None:
    image = Image.new("P", (64, 64), 1)
    image.putpalette([0, 0, 0, 255, 0, 0])
    data = encode(image, "PNG", transparency=1)

    _, _, cover_bytes, *_ = _normalise(data)

    assert close_to(pixel(cover_bytes, (300, 300)), BLACK, tolerance=8)


def test_cmyk_jpeg_is_converted_to_rgb() -> None:
    data = encode(Image.new("CMYK", (64, 64), (0, 255, 255, 0)), "JPEG")

    cover, *_ = _normalise(data)

    assert cover.mode == "RGB"


@pytest.mark.parametrize("image_format", ["PNG", "WEBP"])
def test_first_frame_of_an_animation_is_used(image_format: str) -> None:
    _, _, cover_bytes, *_ = _normalise(animated(image_format, [GREEN, RED, BLUE]))

    assert close_to(pixel(cover_bytes, (300, 300)), GREEN)


def test_almost_square_picture_is_centre_cropped() -> None:
    cover, *_ = _normalise(solid((500, 499), GREEN))

    assert cover.size == (600, 600)


def test_wide_picture_keeps_its_centre() -> None:
    image = Image.new("RGB", (320, 180), RED)
    image.paste(GREEN, (70, 0, 250, 180))

    _, _, cover_bytes, *_ = _normalise(encode(image, "PNG"))

    assert close_to(pixel(cover_bytes, (5, 300)), GREEN)
    assert close_to(pixel(cover_bytes, (594, 300)), GREEN)


def test_tall_picture_keeps_its_centre() -> None:
    image = Image.new("RGB", (180, 320), RED)
    image.paste(GREEN, (0, 70, 180, 250))

    _, _, cover_bytes, *_ = _normalise(encode(image, "PNG"))

    assert close_to(pixel(cover_bytes, (300, 5)), GREEN)
    assert close_to(pixel(cover_bytes, (300, 594)), GREEN)


def test_more_than_40_megapixels_is_rejected_before_decoding() -> None:
    with pytest.raises(ImageTooLargeError):
        _normalise(png_with_header_size(8000, 5001))


def test_exactly_40_megapixels_passes_the_pixel_check() -> None:
    with pytest.raises(NotAnImageError):
        _normalise(png_with_header_size(8000, 5000))


def test_truncated_picture_is_rejected() -> None:
    data = solid((300, 300))

    with pytest.raises(NotAnImageError):
        _normalise(data[: len(data) // 2])


@pytest.mark.parametrize("data", [b"", b"not an image at all", b"\x89PNG\r\n\x1a\n"])
def test_garbage_is_not_an_image(data: bytes) -> None:
    with pytest.raises(NotAnImageError):
        _normalise(data)


@pytest.mark.parametrize("image_format", ["GIF", "BMP", "TIFF"])
def test_other_formats_are_unsupported(image_format: str) -> None:
    with pytest.raises(UnsupportedImageTypeError):
        _normalise(solid((32, 32), RED, image_format))


def test_polyglot_payload_does_not_survive_re_encoding() -> None:
    payload = b"PK\x03\x04<script>alert(1)</script>"
    cover, _, cover_bytes, thumb_bytes, _ = _normalise(solid((64, 64)) + payload)

    assert payload not in cover_bytes
    assert payload not in thumb_bytes
    assert cover.format == "JPEG"


def test_url_candidate_is_fetched_with_the_online_client() -> None:
    harness = online_harness(
        [
            httpx.Response(
                200, content=solid((480, 360), BLUE), headers={"content-type": "image/jpeg"}
            )
        ]
    )
    normalizer = PillowImageNormalizer(harness.client)

    images = normalizer.normalize(ArtworkCandidate(source="youtube", url=THUMB_URL))

    assert close_to(pixel(images.cover, (300, 300)), BLUE)
    assert str(harness.recorder.requests[0].url) == THUMB_URL


@pytest.mark.parametrize(
    "reply",
    [httpx.Response(404), httpx.Response(500), httpx.Response(200, content=b"x" * (11 << 20))],
    ids=["404", "500", "too-big"],
)
def test_failed_downloads_raise_artwork_fetch_error(reply: httpx.Response) -> None:
    harness = online_harness([reply])

    with pytest.raises(ArtworkFetchError):
        PillowImageNormalizer(harness.client).normalize(
            ArtworkCandidate(source="youtube", url=THUMB_URL)
        )


def test_url_on_a_refused_host_is_not_requested() -> None:
    harness = online_harness()

    with pytest.raises(ArtworkFetchError):
        PillowImageNormalizer(harness.client).normalize(
            ArtworkCandidate(source="youtube", url="https://example.org/cover.jpg")
        )

    assert harness.recorder.requests == []


def test_url_candidate_without_a_client_cannot_be_fetched() -> None:
    with pytest.raises(ArtworkFetchError):
        PillowImageNormalizer().normalize(ArtworkCandidate(source="spotify", url=THUMB_URL))


def test_downloaded_garbage_is_not_an_image() -> None:
    harness = online_harness([httpx.Response(200, content=b"<html>")])

    with pytest.raises(NotAnImageError):
        PillowImageNormalizer(harness.client).normalize(
            ArtworkCandidate(source="youtube", url=THUMB_URL)
        )
