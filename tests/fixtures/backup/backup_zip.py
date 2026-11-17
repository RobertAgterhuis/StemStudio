r"""Byte-level ZIP writer for the backup fixtures (qa, feature 013, T012).

Python's ``zipfile`` normalises names (``\`` becomes ``/`` on Windows, NUL truncates), refuses
nothing and cannot write lying sizes, so the hostile corpus needs a writer that puts exactly the
given bytes into the local and central headers (PKWARE APPNOTE 6.3.x): raw entry names, Unix mode
bits in the external attributes, declared sizes that differ from the data (zip bomb, "declares
1 PiB"), duplicate names, and an end record whose entry count lies. Zip64 extra fields and the
Zip64 end records are written whenever a value needs them, as ``System.IO.Compression`` does.

All timestamps are fixed, so the output depends only on the entries (and on zlib for deflated
data).
"""

from __future__ import annotations

import struct
import zlib
from collections.abc import Sequence
from dataclasses import dataclass

STORED = 0
DEFLATED = 8
UTF8_FLAG = 0x0800
MAX32 = 0xFFFFFFFF
MAX16 = 0xFFFF
VERSION_ZIP64 = 45
VERSION_DEFAULT = 20
UNIX_HOST = 3
DOS_TIME = (14 << 11) | (22 << 5) | (32 // 2)
DOS_DATE = ((2026 - 1980) << 9) | (11 << 5) | 16
"""2026-11-16 14:22:32, the fixed modification time of every entry."""

S_IFLNK = 0o120000
S_IFREG = 0o100000


@dataclass(frozen=True)
class RawEntry:
    """One entry exactly as it is written.

    Attributes:
        name: Raw name bytes (UTF-8 for ordinary names).
        data: Uncompressed content.
        method: ``STORED`` or ``DEFLATED``.
        declared_size: Uncompressed size written to the headers; ``None`` writes ``len(data)``.
        unix_mode: Unix mode (``S_IFLNK | 0o777`` for a link); ``None`` writes a DOS entry.
    """

    name: bytes
    data: bytes
    method: int = STORED
    declared_size: int | None = None
    unix_mode: int | None = None

    @property
    def size(self) -> int:
        """Uncompressed size as written to the headers."""
        return len(self.data) if self.declared_size is None else self.declared_size


@dataclass(frozen=True)
class _Packed:
    entry: RawEntry
    payload: bytes
    crc: int
    offset: int

    @property
    def zip64(self) -> bool:
        return max(self.entry.size, len(self.payload), self.offset) >= MAX32


def compress(data: bytes, method: int) -> bytes:
    """Returns the payload of ``data`` for ``method`` (raw deflate, level 9, or stored)."""
    if method == STORED:
        return data
    packer = zlib.compressobj(9, zlib.DEFLATED, -15)
    return packer.compress(data) + packer.flush()


def build_zip(entries: Sequence[RawEntry], declared_count: int | None = None) -> bytes:
    """Writes a complete archive.

    Args:
        entries: Entries in archive order (duplicates are written twice).
        declared_count: Entry count written to the end records; ``None`` writes the real count.

    Returns:
        The archive bytes.
    """
    body = bytearray()
    packed: list[_Packed] = []
    for entry in entries:
        payload = compress(entry.data, entry.method)
        item = _Packed(entry, payload, zlib.crc32(entry.data), len(body))
        body += _local_header(item) + payload
        packed.append(item)
    directory = b"".join(_central_header(item) for item in packed)
    count = len(packed) if declared_count is None else declared_count
    return bytes(body) + directory + _end_records(count, len(directory), len(body))


def _zip64_extra(item: _Packed, central: bool) -> bytes:
    if not item.zip64:
        return b""
    values = [item.entry.size, len(item.payload)] + ([item.offset] if central else [])
    data = struct.pack(f"<{len(values)}Q", *values)
    return struct.pack("<HH", 0x0001, len(data)) + data


def _sizes(item: _Packed) -> tuple[int, int]:
    if item.zip64:
        return MAX32, MAX32
    return len(item.payload), item.entry.size


def _local_header(item: _Packed) -> bytes:
    compressed, uncompressed = _sizes(item)
    extra = _zip64_extra(item, central=False)
    version = VERSION_ZIP64 if item.zip64 else VERSION_DEFAULT
    fixed = struct.pack(
        "<IHHHHHIIIHH",
        0x04034B50,
        version,
        UTF8_FLAG,
        item.entry.method,
        DOS_TIME,
        DOS_DATE,
        item.crc,
        compressed,
        uncompressed,
        len(item.entry.name),
        len(extra),
    )
    return fixed + item.entry.name + extra


def _attributes(entry: RawEntry) -> tuple[int, int]:
    """Returns (version made by, external attributes)."""
    if entry.unix_mode is None:
        return VERSION_ZIP64, 0
    return (UNIX_HOST << 8) | VERSION_ZIP64, entry.unix_mode << 16


def _central_header(item: _Packed) -> bytes:
    compressed, uncompressed = _sizes(item)
    extra = _zip64_extra(item, central=True)
    made_by, external = _attributes(item.entry)
    offset = MAX32 if item.zip64 else item.offset
    fixed = struct.pack(
        "<IHHHHHHIIIHHHHHII",
        0x02014B50,
        made_by,
        VERSION_ZIP64 if item.zip64 else VERSION_DEFAULT,
        UTF8_FLAG,
        item.entry.method,
        DOS_TIME,
        DOS_DATE,
        item.crc,
        compressed,
        uncompressed,
        len(item.entry.name),
        len(extra),
        0,
        0,
        0,
        external,
        offset,
    )
    return fixed + item.entry.name + extra


class ZipFormatError(ValueError):
    """The bytes are not a readable ZIP (no end record, or the directory contradicts it)."""


@dataclass(frozen=True)
class CentralRecord:
    """One central-directory record exactly as stored (no name normalisation).

    Attributes:
        name: Raw name bytes.
        method: Compression method.
        compressed_size: Payload size.
        size: Declared uncompressed size.
        unix_mode: High 16 bits of the external attributes.
        offset: Offset of the local header.
    """

    name: bytes
    method: int
    compressed_size: int
    size: int
    unix_mode: int
    offset: int


def _end_of_directory(data: bytes) -> tuple[int, int]:
    """Returns (entry count, directory offset), from the Zip64 end record when present."""
    at = data.rfind(b"PK\x05\x06")
    if at < 0:
        raise ZipFormatError("no end of central directory record")
    count, _, offset = struct.unpack_from("<HII", data, at + 10)
    locator = at - 20
    if locator >= 0 and data[locator : locator + 4] == b"PK\x06\x07":
        record = struct.unpack_from("<Q", data, locator + 8)[0]
        count, _, offset = struct.unpack_from("<QQQ", data, record + 32)
    return count, offset


def _zip64_values(extra: bytes, wanted: int) -> list[int]:
    at = 0
    while at + 4 <= len(extra):
        tag, size = struct.unpack_from("<HH", extra, at)
        if tag == 0x0001:
            return list(struct.unpack_from(f"<{wanted}Q", extra, at + 4))
        at += 4 + size
    raise ZipFormatError("Zip64 values announced but no Zip64 extra field")


def _record(data: bytes, at: int) -> tuple[CentralRecord, int]:
    if data[at : at + 4] != b"PK\x01\x02":
        raise ZipFormatError(f"no central directory record at offset {at}")
    fields = struct.unpack_from("<HHHHHHIIIHHHHHII", data, at + 4)
    compressed, size, name_len, extra_len, comment_len = fields[7:12]
    name = data[at + 46 : at + 46 + name_len]
    extra = data[at + 46 + name_len : at + 46 + name_len + extra_len]
    offset = fields[15]
    if MAX32 in (compressed, size, offset):
        size, compressed, offset = _zip64_values(extra, 3)
    record = CentralRecord(name, fields[3], compressed, size, fields[14] >> 16, offset)
    return record, at + 46 + name_len + extra_len + comment_len


def read_central_directory(data: bytes) -> list[CentralRecord]:
    """Every central-directory record in order, duplicates included.

    Raises:
        ZipFormatError: No end record, a broken record, or a count that disagrees with it.
    """
    count, at = _end_of_directory(data)
    records: list[CentralRecord] = []
    while data[at : at + 4] == b"PK\x01\x02":
        record, at = _record(data, at)
        records.append(record)
    if len(records) != count:
        raise ZipFormatError(f"end record declares {count} entries, directory has {len(records)}")
    return records


def read_payload(data: bytes, record: CentralRecord, limit: int) -> bytes:
    """Decompresses at most ``limit`` bytes of ``record`` (a zip bomb stops at the limit)."""
    name_len, extra_len = struct.unpack_from("<HH", data, record.offset + 26)
    start = record.offset + 30 + name_len + extra_len
    payload = data[start : start + record.compressed_size]
    if record.method == STORED:
        return payload[:limit]
    return zlib.decompressobj(-15).decompress(payload, limit)


def _end_records(count: int, directory_size: int, directory_offset: int) -> bytes:
    if count < MAX16:
        return struct.pack(
            "<IHHHHIIH",
            0x06054B50,
            0,
            0,
            count,
            count,
            directory_size,
            directory_offset,
            0,
        )
    zip64_end_offset = directory_offset + directory_size
    record = struct.pack(
        "<IQHHIIQQQQ",
        0x06064B50,
        44,
        VERSION_ZIP64,
        VERSION_ZIP64,
        0,
        0,
        count,
        count,
        directory_size,
        directory_offset,
    )
    locator = struct.pack("<IIQI", 0x07064B50, 0, zip64_end_offset, 1)
    end = struct.pack("<IHHHHIIH", 0x06054B50, 0, 0, MAX16, MAX16, directory_size, MAX32, 0)
    return record + locator + end
