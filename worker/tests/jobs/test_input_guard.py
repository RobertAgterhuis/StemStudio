"""Tests of the input path guard shared by the lane jobs (020 T074/T075, data-model 2.2)."""

from pathlib import Path

import pytest

from stemstudio_worker.jobs.handout_service import UnavailableRenderer
from stemstudio_worker.jobs.input_guard import check_input, resolve_contained
from stemstudio_worker.ports import (
    HANDOUT_FAILED_TEXT,
    HandoutRenderError,
    InputNotFoundError,
    InputPathError,
    RenderError,
)
from stemstudio_worker.schemas import LIBRARY_UNAVAILABLE_TEXT

from .insights_requests import handout_request

PDF = "assets/1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6/tab.pdf"


@pytest.fixture
def root(tmp_path: Path) -> Path:
    library = tmp_path / "library"
    (library / PDF).parent.mkdir(parents=True)
    (library / PDF).write_bytes(b"%PDF")
    return library


def test_a_contained_file_resolves_to_its_real_path(root: Path) -> None:
    assert resolve_contained(root, PDF) == (root / PDF).resolve()


@pytest.mark.parametrize("missing", [None, "nowhere"])
def test_without_a_library_the_guard_reports_it_unavailable(
    tmp_path: Path, missing: str | None
) -> None:
    library = None if missing is None else tmp_path / missing

    with pytest.raises(RenderError, match=LIBRARY_UNAVAILABLE_TEXT):
        resolve_contained(library, PDF)


def test_missing_files_and_folders_are_not_found(root: Path) -> None:
    (root / "assets/2c3d4e5f-6071-4829-8a0b-c1d2e3f4a5b7/score.pdf").mkdir(parents=True)

    with pytest.raises(InputNotFoundError):
        resolve_contained(root, "assets/2c3d4e5f-6071-4829-8a0b-c1d2e3f4a5b7/score.pdf")
    with pytest.raises(InputNotFoundError):
        resolve_contained(root, "assets/2c3d4e5f-6071-4829-8a0b-c1d2e3f4a5b7/none.pdf")


def test_a_link_out_of_the_library_is_refused(root: Path, tmp_path: Path) -> None:
    (tmp_path / "secret.pdf").write_bytes(b"%PDF")
    (root / PDF).unlink()
    try:
        (root / PDF).symlink_to(tmp_path / "secret.pdf")
    except OSError:
        pytest.skip("symlinks are not available here")

    with pytest.raises(InputPathError):
        resolve_contained(root, PDF)


def test_check_input_refuses_paths_the_kind_does_not_allow(root: Path) -> None:
    with pytest.raises(InputPathError):
        check_input(lambda path: resolve_contained(root, path), PDF, lambda _path: False)


def test_check_input_lets_an_unavailable_library_pass(tmp_path: Path) -> None:
    check_input(lambda path: resolve_contained(None, path), PDF, lambda _path: True)


def test_the_unavailable_renderer_fails_readably() -> None:
    with pytest.raises(HandoutRenderError, match=HANDOUT_FAILED_TEXT):
        UnavailableRenderer().render(handout_request(), {}, lambda _fraction: None)
