import io
import struct
import zipfile

import pytest

from backup_zip import (
    DEFLATED,
    MAX32,
    S_IFLNK,
    STORED,
    RawEntry,
    ZipFormatError,
    build_zip,
    read_central_directory,
    read_payload,
)


def test_ordinary_archive_reads_back_with_zipfile() -> None:
    data = build_zip([RawEntry(b"a.txt", b"hello", DEFLATED), RawEntry(b"b.flac", b"\x00" * 9)])
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        assert archive.testzip() is None
        assert archive.read("a.txt") == b"hello"
        assert [info.compress_type for info in archive.infolist()] == [DEFLATED, STORED]
        assert archive.infolist()[0].date_time == (2026, 11, 16, 14, 22, 32)


def test_raw_names_are_kept_byte_for_byte() -> None:
    names = [b"files\\x\\y.flac", b"files/voc\x01als.flac", b"C:\\x", b"/abs"]
    records = read_central_directory(build_zip([RawEntry(name, b"x") for name in names]))
    assert [record.name for record in records] == names


def test_link_mode_lands_in_the_external_attributes() -> None:
    data = build_zip([RawEntry(b"link", b"target", unix_mode=S_IFLNK | 0o777)])
    assert read_central_directory(data)[0].unix_mode == S_IFLNK | 0o777
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        assert archive.infolist()[0].external_attr >> 16 == S_IFLNK | 0o777
        assert archive.infolist()[0].create_system == 3


def test_duplicates_are_written_twice() -> None:
    records = read_central_directory(build_zip([RawEntry(b"same", b"1"), RawEntry(b"same", b"2")]))
    assert [record.name for record in records] == [b"same", b"same"]


def test_huge_declared_size_uses_zip64_fields() -> None:
    huge = 1 << 50
    data = build_zip([RawEntry(b"big.wav", b"RIFF", DEFLATED, declared_size=huge)])
    record = read_central_directory(data)[0]
    assert record.size == huge
    assert struct.unpack_from("<I", data, data.find(b"PK\x01\x02") + 24)[0] == MAX32
    with zipfile.ZipFile(io.BytesIO(data)) as archive:
        assert archive.infolist()[0].file_size == huge


def test_declared_count_above_16_bits_writes_zip64_end_records() -> None:
    data = build_zip([RawEntry(b"a", b"1")], declared_count=2_000_001)
    assert b"PK\x06\x06" in data
    assert b"PK\x06\x07" in data
    with pytest.raises(ZipFormatError, match="2000001 entries"):
        read_central_directory(data)


def test_large_real_count_reads_back() -> None:
    entries = [RawEntry(f"e{i}".encode(), b"") for i in range(70_000)]
    assert len(read_central_directory(build_zip(entries))) == 70_000


def test_payload_is_bounded_by_the_limit() -> None:
    data = build_zip(
        [
            RawEntry(b"bomb", bytes(1 << 20), DEFLATED, declared_size=10),
            RawEntry(b"plain", b"0123456789"),
        ]
    )
    bomb, plain = read_central_directory(data)
    assert len(read_payload(data, bomb, 11)) == 11
    assert read_payload(data, plain, 4) == b"0123"


def test_missing_end_record_is_a_format_error() -> None:
    with pytest.raises(ZipFormatError, match="no end"):
        read_central_directory(b"plain text")


def test_broken_directory_offset_is_a_format_error() -> None:
    data = bytearray(build_zip([RawEntry(b"a", b"1")]))
    at = data.rfind(b"PK\x05\x06")
    struct.pack_into("<I", data, at + 16, 0)
    with pytest.raises(ZipFormatError, match="end record declares 1"):
        read_central_directory(bytes(data))


def test_zip64_marker_without_extra_field_is_a_format_error() -> None:
    data = bytearray(build_zip([RawEntry(b"a", b"1")]))
    at = data.find(b"PK\x01\x02")
    struct.pack_into("<I", data, at + 24, MAX32)
    with pytest.raises(ZipFormatError, match="no Zip64 extra"):
        read_central_directory(bytes(data))
