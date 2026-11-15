"""Two-pass block renderer of render jobs (data-model.md 3, ADR-016 D4/D6).

Pass 1 mixes every block and measures the peak (progress 0..0.5). Pass 2 mixes again, applies
the clip protection gain (peak > 1.0 -> scaled to -0.1 dBFS), quantises with optional TPDF dither
and encodes block by block (progress 0.5..1). Both passes compute identical blocks, so the
measured peak is the peak of what is written.
"""

import hashlib
import math
from collections.abc import Iterator
from dataclasses import dataclass
from pathlib import Path
from typing import Final

import numpy as np

from stemstudio_worker.ports import LibraryReader, RenderError, RenderProgress
from stemstudio_worker.render.dither import TpdfDither, dither_seed, full_scale, quantize
from stemstudio_worker.render.encoders import Encoder, open_encoder
from stemstudio_worker.render.mixing import Block, mix_block
from stemstudio_worker.render.reader import (
    SAMPLE_RATE,
    FrameRange,
    InputFile,
    OpenInputs,
    frame_range,
    open_inputs,
)
from stemstudio_worker.schemas import (
    RENDER_CONTENT_TYPES,
    SILENT_RENDER_TEXT,
    RenderJobRequest,
    RenderResult,
)

TARGET_PEAK: Final = 10 ** (-0.1 / 20)
"""Linear peak of a clip-protected render (-0.1 dBFS)."""

PEAK_FLOOR_DBFS: Final = -150.0
"""Reported ``peakDbfs`` when every written sample quantised to zero."""

WRITE_FAILED_TEXT: Final = "The export file could not be written"
"""Error when the output file cannot be created."""

_HASH_CHUNK: Final = 1 << 20


@dataclass(frozen=True, slots=True)
class _Pass:
    """The opened inputs and the frames of one render, iterated once per pass."""

    opened: OpenInputs
    request: RenderJobRequest
    span: FrameRange
    block_frames: int

    @property
    def block_count(self) -> int:
        """Number of blocks per pass."""
        return math.ceil(self.span.count / self.block_frames)

    def mixed(self) -> Iterator[Block]:
        """Yield the mixed block of every block of the range."""
        for blocks in self.opened.blocks(self.span, self.block_frames):
            yield mix_block(blocks, self.request.inputs, self.request.master_gain)


@dataclass(frozen=True, slots=True)
class _Target:
    """Where and how pass 2 writes."""

    path: Path
    gain: float
    dither: TpdfDither | None


@dataclass(frozen=True, slots=True)
class _Written:
    """Peak of the written integers and their bit depth."""

    peak: int
    bits: int


class BlockRenderExecutor:
    """``RenderExecutor`` that streams the inputs from the library in fixed-length blocks."""

    def __init__(self, library: LibraryReader, block_seconds: float) -> None:
        """Create the executor.

        Args:
            library: Path guard over the read-only library mount.
            block_seconds: Block length (``WorkerSettings.render.block_seconds``).
        """
        self._library = library
        self._block_frames = max(1, round(block_seconds * SAMPLE_RATE))

    @property
    def library(self) -> LibraryReader:
        """The library the inputs are read from (also the source of ``Health.library``)."""
        return self._library

    @property
    def block_frames(self) -> int:
        """Frames per block (at least 1)."""
        return self._block_frames

    def validate(self, request: RenderJobRequest) -> None:
        """Resolve every input path before the job is queued.

        Raises:
            InputPathError: An input path is not allowed (HTTP 400).
            InputNotFoundError: An input file does not exist (HTTP 404).
            RenderError: The library is not available.
        """
        self._resolve(request)

    def render(
        self, request: RenderJobRequest, out_dir: Path, progress: RenderProgress
    ) -> RenderResult:
        """Render ``out_dir/output.<ext>``; dither is seeded from ``out_dir.name`` (the job id).

        Raises:
            RenderError: Unreadable inputs, unsupported format, silent result or unwritable output.
            RenderCancelledError: Propagated from ``progress``; the partial file is removed.
        """
        with open_inputs(self._resolve(request)) as opened:
            span = frame_range(request.range, opened.frames)
            job = _Pass(opened, request, span, self._block_frames)
            target = _target(request, out_dir, _protection_gain(_measure_peak(job, progress)))
            written = _write(job, target, progress)
        return _result(job, target, written)

    def _resolve(self, request: RenderJobRequest) -> list[InputFile]:
        """Resolve every input through the path guard, in request order."""
        return [InputFile(item.path, self._library.resolve(item.path)) for item in request.inputs]


def _report(progress: RenderProgress, offset: float, index: int, total: int) -> None:
    """Report the progress after block ``index`` of a pass starting at ``offset``."""
    progress(offset + 0.5 * (index + 1) / total)


def _measure_peak(job: _Pass, progress: RenderProgress) -> float:
    """Pass 1: return ``max |y|`` over the range."""
    peak = 0.0
    for index, mixed in enumerate(job.mixed()):
        peak = max(peak, float(np.abs(mixed).max()))
        _report(progress, 0.0, index, job.block_count)
    return peak


def _protection_gain(peak: float) -> float:
    """Return 1 for ``peak <= 1.0``, else the gain that brings the peak to -0.1 dBFS."""
    if peak == 0.0:
        raise RenderError(SILENT_RENDER_TEXT)
    return 1.0 if peak <= 1.0 else TARGET_PEAK / peak


def _target(request: RenderJobRequest, out_dir: Path, gain: float) -> _Target:
    """Build the pass 2 target; dither is seeded from the job id (the output directory name)."""
    dither = TpdfDither(dither_seed(out_dir.name)) if request.output.dither else None
    return _Target(out_dir / f"output.{request.output.format}", gain, dither)


def _write(job: _Pass, target: _Target, progress: RenderProgress) -> _Written:
    """Pass 2: encode the file; on any failure (also cancellation) remove the partial file."""
    try:
        encoder = open_encoder(job.request.output, job.request.metadata, target.path)
    except (OSError, RuntimeError):
        raise RenderError(WRITE_FAILED_TEXT) from None
    try:
        peak = _encode(job, target, encoder, progress)
    except BaseException:
        encoder.close()
        target.path.unlink(missing_ok=True)
        raise
    encoder.close()
    return _Written(peak, encoder.bits)


def _encode(job: _Pass, target: _Target, encoder: Encoder, progress: RenderProgress) -> int:
    """Scale, quantise and write every block; return the peak of the written integers."""
    peak = 0
    for index, mixed in enumerate(job.mixed()):
        ints = quantize(mixed * np.float32(target.gain), encoder.bits, target.dither)
        encoder.write(ints)
        peak = max(peak, int(np.abs(ints).max()))
        _report(progress, 0.5, index, job.block_count)
    return peak


def _result(job: _Pass, target: _Target, written: _Written) -> RenderResult:
    """Describe the written file."""
    output_format = job.request.output.format
    return RenderResult(
        file_name=target.path.name,
        content_type=RENDER_CONTENT_TYPES[output_format],
        size_bytes=target.path.stat().st_size,
        sha256=_sha256(target.path),
        duration_seconds=job.span.count / SAMPLE_RATE,
        peak_dbfs=_dbfs(written.peak / full_scale(written.bits)),
        applied_gain_db=20 * math.log10(target.gain),
    )


def _dbfs(level: float) -> float:
    """Return ``20·log10(level)``, or ``PEAK_FLOOR_DBFS`` for silence."""
    return 20 * math.log10(level) if level > 0 else PEAK_FLOOR_DBFS


def _sha256(path: Path) -> str:
    """Return the hex SHA-256 of a file, read in chunks."""
    digest = hashlib.sha256()
    with path.open("rb") as file:
        while chunk := file.read(_HASH_CHUNK):
            digest.update(chunk)
    return digest.hexdigest()
