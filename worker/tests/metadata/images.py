"""Images generated in the tests themselves (never third-party pictures)."""

import io
import random
import struct
import zlib

from PIL import Image

Colour = tuple[int, int, int]


def encode(image: Image.Image, image_format: str, **options: object) -> bytes:
    """Encode ``image`` as ``image_format`` bytes."""
    buffer = io.BytesIO()
    image.save(buffer, image_format, **options)
    return buffer.getvalue()


def solid(size: tuple[int, int], colour: Colour = (200, 40, 40), image_format: str = "JPEG") -> bytes:
    """A solid-colour picture."""
    return encode(Image.new("RGB", size, colour), image_format)


def halves(size: tuple[int, int], left: Colour, right: Colour) -> Image.Image:
    """A picture whose left half is ``left`` and right half ``right``."""
    image = Image.new("RGB", size, right)
    image.paste(left, (0, 0, size[0] // 2, size[1]))
    return image


def noise(size: tuple[int, int], seed: int = 5) -> bytes:
    """A high-entropy PNG (worst case for JPEG size limits)."""
    rng = random.Random(seed)
    data = bytes(rng.getrandbits(8) for _ in range(size[0] * size[1] * 3))
    return encode(Image.frombytes("RGB", size, data), "PNG")


def animated(image_format: str, colours: list[Colour]) -> bytes:
    """An animation whose frames are solid ``colours`` (first frame first)."""
    frames = [Image.new("RGB", (64, 64), colour) for colour in colours]
    return encode(frames[0], image_format, save_all=True, append_images=frames[1:], duration=100)


def png_with_header_size(width: int, height: int) -> bytes:
    """A PNG whose IHDR claims ``width`` x ``height`` (valid CRC) but holds a tiny body."""
    data = bytearray(solid((4, 4), image_format="PNG"))
    ihdr = 8
    fields = struct.pack(">II", width, height) + bytes(data[ihdr + 16 : ihdr + 21])
    data[ihdr + 8 : ihdr + 21] = fields
    crc = zlib.crc32(bytes(data[ihdr + 4 : ihdr + 21]))
    data[ihdr + 21 : ihdr + 25] = struct.pack(">I", crc)
    return bytes(data)


def pixel(jpeg: bytes, xy: tuple[int, int]) -> Colour:
    """The RGB value at ``xy`` of an encoded picture."""
    with Image.open(io.BytesIO(jpeg)) as image:
        value = image.convert("RGB").getpixel(xy)
    assert isinstance(value, tuple)
    return (int(value[0]), int(value[1]), int(value[2]))


def close_to(actual: Colour, expected: Colour, tolerance: int = 40) -> bool:
    """True when every channel is within ``tolerance`` (JPEG is lossy)."""
    return all(abs(a - e) <= tolerance for a, e in zip(actual, expected, strict=True))
