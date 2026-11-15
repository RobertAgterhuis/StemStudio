"""Block-wise reading of the render inputs (data-model.md 2.2, research.md E-04).

Inputs are aligned at frame 0; shorter inputs are padded with silence to the longest one, mono
inputs are duplicated to stereo, and every input must be 44.1 kHz. Only one block per input is
in memory at a time, so memory does not depend on the song length.
"""

from collections.abc import Iterator, Sequence
from contextlib import ExitStack, contextmanager
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Final

import numpy as np
import soundfile as sf

from stemstudio_worker.ports import RenderError
from stemstudio_worker.render.mixing import Block
from stemstudio_worker.schemas import RenderRange

SAMPLE_RATE: Final = 44100
"""The only accepted input (and output) sample rate."""


@dataclass(frozen=True, slots=True)
class InputFile:
    """One resolved render input.

    Attributes:
        name: Library-relative path, used in error messages.
        file: Real path of the file (from ``LibraryReader.resolve``).
    """

    name: str
    file: Path


@dataclass(frozen=True, slots=True)
class FrameRange:
    """Half-open frame interval ``[start, end)`` of the render."""

    start: int
    end: int

    @property
    def count(self) -> int:
        """Number of frames in the range."""
        return self.end - self.start


def frame_range(render_range: RenderRange | None, total_frames: int) -> FrameRange:
    """Convert the requested range to frames; the end is clamped to ``total_frames``.

    Args:
        render_range: Requested range in seconds, or None for everything.
        total_frames: Length of the longest input.

    Returns:
        The frames to render.

    Raises:
        RenderError: The range starts at or after the end of the inputs.
    """
    if render_range is None:
        return FrameRange(0, total_frames)
    start = round(render_range.start_seconds * SAMPLE_RATE)
    end = min(round(render_range.end_seconds * SAMPLE_RATE), total_frames)
    if start >= end:
        msg = "The range starts after the end of the inputs"
        raise RenderError(msg)
    return FrameRange(start, end)


class OpenInputs:
    """The opened input files of one render pass."""

    def __init__(self, files: Sequence[tuple[str, Any]]) -> None:
        """Wrap ``(name, soundfile.SoundFile)`` pairs that are already open (``open_inputs``)."""
        self._files = files

    @property
    def frames(self) -> int:
        """Length of the longest input in frames."""
        return max(int(file.frames) for _, file in self._files)

    def blocks(self, span: FrameRange, block_frames: int) -> Iterator[list[Block]]:
        """Yield one list of float32 ``(n, 2)`` blocks (one per input) per block of ``span``.

        Args:
            span: Frames to read.
            block_frames: Frames per block; the last block may be shorter.

        Raises:
            RenderError: A file fails while it is read ("Cannot read <path>").
        """
        for _, file in self._files:
            if span.start < file.frames:
                file.seek(span.start)
        for position in range(span.start, span.end, block_frames):
            count = min(block_frames, span.end - position)
            yield [_read_block(name, file, position, count) for name, file in self._files]


@contextmanager
def open_inputs(inputs: Sequence[InputFile]) -> Iterator[OpenInputs]:
    """Open every input for reading and close them all afterwards.

    Args:
        inputs: The resolved inputs, in request order.

    Yields:
        The opened inputs.

    Raises:
        RenderError: A file cannot be read, is not 44.1 kHz, or has more than two channels.
    """
    with ExitStack() as stack:
        files = [(item.name, stack.enter_context(_open(item))) for item in inputs]
        yield OpenInputs(files)


def _open(item: InputFile) -> Any:
    """Open one input and check its format."""
    try:
        file = sf.SoundFile(item.file)
    except RuntimeError:
        raise RenderError(f"Cannot read {item.name}") from None
    if file.samplerate != SAMPLE_RATE or file.channels > 2:  # noqa: PLR2004  (stereo)
        file.close()
        raise RenderError(_format_problem(item.name, file.samplerate, file.channels))
    return file


def _format_problem(name: str, rate: int, channels: int) -> str:
    """Return the error text of an input with an unsupported format."""
    if rate != SAMPLE_RATE:
        return f"Unsupported sample rate {rate} Hz in {name}"
    return f"Unsupported channel count {channels} in {name}"


def _read_block(name: str, file: Any, position: int, count: int) -> Block:
    """Read ``count`` frames at ``position`` as stereo float32, padded with silence."""
    if position >= file.frames:
        return np.zeros((count, 2), dtype=np.float32)
    try:
        data = file.read(count, dtype="float32", always_2d=True)
    except RuntimeError:
        raise RenderError(f"Cannot read {name}") from None
    if data.shape[1] == 1:
        data = np.repeat(data, 2, axis=1)
    if data.shape[0] < count:
        data = np.concatenate([data, np.zeros((count - data.shape[0], 2), dtype=np.float32)])
    block: Block = data
    return block
