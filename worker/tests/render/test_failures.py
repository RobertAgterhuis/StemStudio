"""Tests of I/O failures in the middle of a render: readable errors, no partial file left."""

from pathlib import Path
from typing import Any

import numpy as np
import pytest
import soundfile as sf

from stemstudio_worker.ports import RenderCancelledError, RenderError
from stemstudio_worker.render import executor as executor_module
from stemstudio_worker.render.dither import IntBlock
from stemstudio_worker.render.executor import WRITE_FAILED_TEXT, BlockRenderExecutor
from stemstudio_worker.render.paths import MountedLibrary
from stemstudio_worker.render.reader import FrameRange, OpenInputs
from stemstudio_worker.schemas import RenderJobRequest, RenderMetadata, RenderOutput

_PATH = "tracks/3f2b8c1d-9e4a-4f6b-8a7c-5d3e2f1a0b9c/stems/bass.flac"


class _BrokenEncoder:
    """Creates its file, then fails like a full disk on ``write`` or ``close``."""

    def __init__(self, path: Path, fail_on: str, error: BaseException) -> None:
        path.write_bytes(b"partial")
        self.fail_on, self.error, self.closed = fail_on, error, 0

    @property
    def bits(self) -> int:
        return 16

    def write(self, block: IntBlock) -> None:
        if self.fail_on == "write":
            raise self.error

    def close(self) -> None:
        self.closed += 1
        if self.fail_on == "close":
            raise self.error


def _setup(tmp_path: Path) -> tuple[BlockRenderExecutor, RenderJobRequest, Path]:
    stem = tmp_path / _PATH
    stem.parent.mkdir(parents=True)
    sf.write(stem, np.full((1000, 2), 0.25), 44100, format="FLAC", subtype="PCM_16")
    out_dir = tmp_path / "job"
    out_dir.mkdir()
    request = RenderJobRequest.model_validate(
        {
            "inputs": [{"path": _PATH, "gain": 1, "pan": 0}],
            "masterGain": 1,
            "output": {"format": "wav"},
        }
    )
    return BlockRenderExecutor(MountedLibrary(tmp_path), 400 / 44100), request, out_dir


def _install(monkeypatch: pytest.MonkeyPatch, fail_on: str, error: BaseException) -> list[Any]:
    made: list[Any] = []

    def factory(output: RenderOutput, metadata: RenderMetadata, path: Path) -> _BrokenEncoder:
        made.append(_BrokenEncoder(path, fail_on, error))
        return made[-1]

    monkeypatch.setattr(executor_module, "open_encoder", factory)
    return made


@pytest.mark.parametrize("fail_on", ["write", "close"])
@pytest.mark.parametrize("error", [OSError(28, "No space left on device"), RuntimeError("sf")])
def test_an_encoder_failure_is_a_readable_error_and_removes_the_file(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, fail_on: str, error: BaseException
) -> None:
    executor, request, out_dir = _setup(tmp_path)
    made = _install(monkeypatch, fail_on, error)
    with pytest.raises(RenderError) as caught:
        executor.render(request, out_dir, lambda fraction: None)
    assert (type(caught.value), str(caught.value)) == (RenderError, WRITE_FAILED_TEXT)
    assert list(out_dir.iterdir()) == []
    assert made[0].closed >= 1


def test_a_cancellation_keeps_its_type(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> None:
    executor, request, out_dir = _setup(tmp_path)
    _install(monkeypatch, "write", RenderCancelledError())
    with pytest.raises(RenderCancelledError):
        executor.render(request, out_dir, lambda fraction: None)
    assert list(out_dir.iterdir()) == []


class _FailingFile:
    """A ``SoundFile`` stand-in whose ``read`` fails like a corrupt FLAC frame."""

    frames = 100

    def seek(self, frame: int) -> None:
        pass

    def read(self, count: int, **options: Any) -> Any:
        raise RuntimeError("Internal psf_fseek() failed.")


def test_a_read_failure_names_the_input() -> None:
    opened = OpenInputs([("tracks/x/bass.flac", _FailingFile())])
    with pytest.raises(RenderError, match=r"^Cannot read tracks/x/bass\.flac$"):
        list(opened.blocks(FrameRange(0, 100), 10))
