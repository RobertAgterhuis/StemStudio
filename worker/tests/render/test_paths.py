"""Tests of the library path guard (data-model.md 2.1, ADR-016 D5)."""

import os
import sys
from pathlib import Path

import pytest

from stemstudio_worker.ports import InputNotFoundError, InputPathError, LibraryReader, RenderError
from stemstudio_worker.render.paths import MountedLibrary
from stemstudio_worker.schemas import LIBRARY_UNAVAILABLE_TEXT, LibraryHealth

_ID = "3f2b8c1d-9e4a-4f6b-8a7c-5d3e2f1a0b9c"

_VALID = (
    f"tracks/{_ID}/stems/vocals.flac",
    f"tracks/{_ID}/stems/other.flac",
    f"tracks/{_ID}/detail/drums/kick.flac",
    f"tracks/{_ID}/detail/vocals/lead-vocals.flac",
    f"assets/{_ID}/audio.flac",
    f"assets/{_ID}/audio.wav",
    f"assets/{_ID}/mix-2.mp3",
)

_INVALID = (
    f"tracks/{_ID.upper()}/stems/vocals.flac",
    f"tracks/{_ID}/stems/../../../stemstudio.db",
    "tracks/../stemstudio.db",
    f"/tracks/{_ID}/stems/vocals.flac",
    f"C:/library/tracks/{_ID}/stems/vocals.flac",
    f"tracks\\{_ID}\\stems\\vocals.flac",
    f"tracks/{_ID}/stems/keys.flac",
    f"tracks/{_ID}/stems/vocals.wav",
    f"tracks/{_ID}/detail/bass/low.flac",
    f"assets/{_ID}/audio.ogg",
    f"assets/{_ID}/Audio.flac",
    f"tracks/{_ID}/stems/vocals.flac ",
    "stemstudio.db",
    "",
)


def _write(root: Path, relative: str) -> Path:
    file = root / relative
    file.parent.mkdir(parents=True, exist_ok=True)
    file.write_bytes(b"fLaC")
    return file


def _link_directory(link: Path, target: Path) -> None:
    """Create a directory symlink, or a junction on Windows without the symlink privilege."""
    link.parent.mkdir(parents=True, exist_ok=True)
    try:
        link.symlink_to(target, target_is_directory=True)
    except OSError:
        if sys.platform != "win32":
            raise
        import _winapi  # noqa: PLC0415  (Windows only)

        _winapi.CreateJunction(str(target), str(link))


def test_mounted_library_is_a_library_reader(tmp_path: Path) -> None:
    library: LibraryReader = MountedLibrary(tmp_path)
    assert library.status() == LibraryHealth(readable=True, reason=None)


@pytest.mark.parametrize("relative", _VALID)
def test_allowed_paths_resolve_to_the_real_file(tmp_path: Path, relative: str) -> None:
    file = _write(tmp_path, relative)
    assert MountedLibrary(tmp_path).resolve(relative) == file.resolve()


@pytest.mark.parametrize("relative", _INVALID)
def test_paths_off_the_allow_list_are_rejected_without_reading(
    tmp_path: Path, relative: str
) -> None:
    with pytest.raises(InputPathError) as caught:
        MountedLibrary(tmp_path).resolve(relative)
    assert str(caught.value) == f"Input path is not allowed: {relative}"


def test_a_path_off_the_allow_list_is_rejected_even_without_a_library() -> None:
    with pytest.raises(InputPathError):
        MountedLibrary(None).resolve("tracks/../stemstudio.db")


def test_a_link_that_escapes_the_root_is_rejected(tmp_path: Path) -> None:
    root, outside = tmp_path / "library", tmp_path / "outside"
    _write(outside, "stems/vocals.flac")
    _link_directory(root / "tracks" / _ID, outside)
    relative = f"tracks/{_ID}/stems/vocals.flac"
    with pytest.raises(InputPathError, match="Input path is not allowed"):
        MountedLibrary(root).resolve(relative)


def test_a_link_that_stays_inside_the_root_is_accepted(tmp_path: Path) -> None:
    root = tmp_path / "library"
    file = _write(root, f"assets/{_ID}/vocals.flac")
    _link_directory(root / "tracks" / _ID / "stems", file.parent)
    assert MountedLibrary(root).resolve(f"tracks/{_ID}/stems/vocals.flac") == file.resolve()


def test_a_missing_file_raises_input_not_found_naming_the_path(tmp_path: Path) -> None:
    relative = f"tracks/{_ID}/stems/bass.flac"
    with pytest.raises(InputNotFoundError) as caught:
        MountedLibrary(tmp_path).resolve(relative)
    assert (caught.value.path, str(caught.value)) == (relative, f"Input not found: {relative}")


def test_a_directory_with_a_file_name_is_not_found(tmp_path: Path) -> None:
    relative = f"assets/{_ID}/audio.flac"
    (tmp_path / relative).mkdir(parents=True)
    with pytest.raises(InputNotFoundError):
        MountedLibrary(tmp_path).resolve(relative)


def test_the_root_can_be_given_relative_to_the_working_directory(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    relative = f"tracks/{_ID}/stems/drums.flac"
    file = _write(tmp_path / "library", relative)
    monkeypatch.chdir(tmp_path)
    assert MountedLibrary(Path("library")).resolve(relative) == file.resolve()


def _unavailable_roots(tmp_path: Path) -> list[Path | None]:
    not_a_directory = tmp_path / "file"
    not_a_directory.write_text("x", encoding="utf-8")
    return [None, tmp_path / "missing", not_a_directory]


@pytest.mark.parametrize("index", [0, 1, 2], ids=["unset", "missing", "not a directory"])
def test_an_unusable_root_is_reported_and_fails_every_resolve(tmp_path: Path, index: int) -> None:
    library = MountedLibrary(_unavailable_roots(tmp_path)[index])
    assert library.status() == LibraryHealth(readable=False, reason=LIBRARY_UNAVAILABLE_TEXT)
    with pytest.raises(RenderError) as caught:
        library.resolve(f"tracks/{_ID}/stems/vocals.flac")
    assert (type(caught.value), str(caught.value)) == (RenderError, LIBRARY_UNAVAILABLE_TEXT)


def test_an_unlistable_root_is_unavailable(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def deny(path: object) -> object:
        raise PermissionError(13, "Permission denied", str(path))

    monkeypatch.setattr(os, "scandir", deny)
    assert MountedLibrary(tmp_path).status().readable is False


def test_status_is_checked_on_every_call(tmp_path: Path) -> None:
    root = tmp_path / "library"
    library = MountedLibrary(root)
    before = library.status().readable
    root.mkdir()
    assert (before, library.status().readable) == (False, True)
