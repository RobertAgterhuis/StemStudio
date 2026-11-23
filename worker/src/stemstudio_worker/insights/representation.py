"""Analysis representation shared by every quality measure (data-model 4.1, research R-02).

Each FLAC is read in blocks, mixed to mono, resampled to 22,050 Hz (soxr) and turned into a
64-band mel power spectrogram (STFT 2048/512, Hann, 30 Hz-11 kHz). The mix is the sum of the six
standard stems in the time domain before the STFT. Only the mel power and one broadband level
per frame are kept, so a 15-minute stem needs about 10 MB.
"""

from collections.abc import Iterator, Mapping, Sequence
from contextlib import ExitStack, contextmanager
from dataclasses import dataclass
from pathlib import Path
from typing import Final

import numpy as np
import numpy.typing as npt
import soundfile as sf
import soxr

from stemstudio_worker.ports import QUALITY_SAMPLE_RATE, QualityAnalysisError

N_FFT: Final = 2048
"""STFT window length (samples at 22,050 Hz)."""

HOP: Final = 512
"""STFT hop (samples); one frame is about 23 ms."""

BANDS: Final = 64
"""Mel bands from ``LOW_HZ`` to ``HIGH_HZ``."""

LOW_HZ: Final = 30.0
HIGH_HZ: Final = 11_000.0

FRAME_SECONDS: Final = HOP / QUALITY_SAMPLE_RATE
"""Duration of one frame (active seconds = active frames x this)."""

BLOCK_FRAMES = 1 << 16
"""Sample frames read from a file per block (module variable so tests can shrink it)."""

ACTIVE_FLOOR_DB: Final = -50.0
"""A frame is active above ``max(ACTIVE_FLOOR_DB, peak - ACTIVE_RANGE_DB)`` (002 gate family)."""

ACTIVE_RANGE_DB: Final = 40.0

SILENCE_DB: Final = -120.0
"""Level of digital silence (``10 log10(1e-12)``)."""

_EPS: Final = 1e-12
_WINDOW: Final = np.hanning(N_FFT + 1)[:-1].astype(np.float32)
_SCALE: Final = 2.0 / (N_FFT * float(np.sum(_WINDOW**2)))
_EMPTY: Final = np.zeros(0, dtype=np.float32)

FloatArray = npt.NDArray[np.float32]
BoolArray = npt.NDArray[np.bool_]


@dataclass(frozen=True, slots=True)
class Spectrogram:
    """Mel power of one signal.

    Attributes:
        power: ``P(t, b)``, shape ``(frames, BANDS)``, float32.
        frame_db: Broadband level of every frame in dBFS (mean square of the frame), floored at
            ``SILENCE_DB``.
        block_db: Level of the first ``HOP`` samples of every frame (consecutive 23 ms blocks
            without overlap), the fine envelope the dropout measure uses.
    """

    power: FloatArray
    frame_db: FloatArray
    block_db: FloatArray

    @property
    def frames(self) -> int:
        """Number of frames."""
        return int(self.power.shape[0])

    @property
    def seconds(self) -> float:
        """Duration covered by the frames."""
        return self.frames * FRAME_SECONDS

    def band_db(self) -> FloatArray:
        """``L = 10 log10(P + 1e-12)`` per frame and band."""
        return (10.0 * np.log10(self.power + np.float32(_EPS))).astype(np.float32)

    def active(self) -> BoolArray:
        """Frames above the activity floor ``max(-50 dBFS, peak - 40 dB)``."""
        if self.frames == 0:
            return np.zeros(0, dtype=np.bool_)
        floor = max(ACTIVE_FLOOR_DB, float(self.frame_db.max()) - ACTIVE_RANGE_DB)
        return np.asarray(self.frame_db > floor)


def _hz_to_mel(hz: npt.ArrayLike) -> npt.NDArray[np.float64]:
    return 2595.0 * np.log10(1.0 + np.asarray(hz, dtype=np.float64) / 700.0)


def _mel_to_hz(mel: npt.ArrayLike) -> npt.NDArray[np.float64]:
    return 700.0 * (10.0 ** (np.asarray(mel, dtype=np.float64) / 2595.0) - 1.0)


def _band_edges() -> npt.NDArray[np.float64]:
    return _mel_to_hz(np.linspace(_hz_to_mel(LOW_HZ), _hz_to_mel(HIGH_HZ), BANDS + 2))


def band_centres() -> npt.NDArray[np.float64]:
    """Centre frequency of every mel band (Hz)."""
    return _band_edges()[1:-1]


def mel_filterbank() -> FloatArray:
    """Triangular mel filters (HTK scale), shape ``(BANDS, N_FFT // 2 + 1)``."""
    edges = _band_edges()
    freqs = np.fft.rfftfreq(N_FFT, 1.0 / QUALITY_SAMPLE_RATE)
    lower, centre, upper = edges[:-2, None], edges[1:-1, None], edges[2:, None]
    rising = (freqs - lower) / (centre - lower)
    falling = (upper - freqs) / (upper - centre)
    return np.maximum(0.0, np.minimum(rising, falling)).astype(np.float32)


_FILTERS: Final = mel_filterbank().T.copy()


def _level_db(frames: FloatArray) -> FloatArray:
    mean_square = np.mean(np.square(frames, dtype=np.float32), axis=1)
    return (10.0 * np.log10(mean_square + np.float32(_EPS))).astype(np.float32)


class _MelStream:
    """Turns consecutive mono blocks at 22,050 Hz into mel frames without keeping the audio."""

    def __init__(self) -> None:
        self._tail = _EMPTY
        self._seen = 0
        self._power: list[FloatArray] = []
        self._db: list[FloatArray] = []
        self._block: list[FloatArray] = []

    def push(self, samples: FloatArray) -> None:
        buffer = np.concatenate([self._tail, samples]) if len(self._tail) else samples
        count = 0 if len(buffer) < N_FFT else (len(buffer) - N_FFT) // HOP + 1
        if count:
            frames = np.lib.stride_tricks.sliding_window_view(buffer, N_FFT)[::HOP][:count]
            self._add(frames)
        self._tail = buffer[count * HOP :]
        self._seen += len(samples)

    def _add(self, frames: FloatArray) -> None:
        spectrum = np.fft.rfft(frames * _WINDOW, axis=1)
        bins = (spectrum.real**2 + spectrum.imag**2).astype(np.float32) * np.float32(_SCALE)
        self._power.append(bins @ _FILTERS)
        self._db.append(_level_db(frames))
        self._block.append(_level_db(frames[:, :HOP]))

    def finish(self) -> Spectrogram:
        covered = N_FFT - HOP if self._power else 0
        if self._seen and len(self._tail) > covered:
            self.push(np.zeros(N_FFT - len(self._tail), dtype=np.float32))
        if not self._power:
            return Spectrogram(np.zeros((0, BANDS), np.float32), _EMPTY, _EMPTY)
        return Spectrogram(
            np.concatenate(self._power), np.concatenate(self._db), np.concatenate(self._block)
        )


class _Reader:
    """One open file: block-wise mono, resampled, fed into its own mel stream."""

    def __init__(self, audio: sf.SoundFile) -> None:
        self._audio = audio
        self._resampler = (
            None if audio.samplerate == QUALITY_SAMPLE_RATE
            else soxr.ResampleStream(audio.samplerate, QUALITY_SAMPLE_RATE, 1, dtype="float32")
        )  # fmt: skip
        self.mel = _MelStream()
        self.done = False

    def step(self) -> FloatArray:
        """Read, resample and analyse the next block; returns the resampled mono samples."""
        if self.done:
            return _EMPTY
        block = self._audio.read(BLOCK_FRAMES, dtype="float32", always_2d=True)
        self.done = len(block) < BLOCK_FRAMES
        mono = np.ascontiguousarray(block.mean(axis=1, dtype=np.float32))
        if self._resampler is not None:
            mono = np.asarray(self._resampler.resample_chunk(mono, last=self.done), np.float32)
        self.mel.push(mono)
        return mono


def _resampled(mono: FloatArray, rate: int) -> FloatArray:
    if rate == QUALITY_SAMPLE_RATE:
        return mono
    return np.asarray(soxr.resample(mono, rate, QUALITY_SAMPLE_RATE), dtype=np.float32)


def spectrogram_of(mono: npt.ArrayLike, rate: int) -> Spectrogram:
    """Mel spectrogram of mono samples held in memory (tests and calibration)."""
    stream = _MelStream()
    stream.push(_resampled(np.ascontiguousarray(mono, dtype=np.float32), rate))
    return stream.finish()


@contextmanager
def _opened(path: Path) -> Iterator[sf.SoundFile]:
    try:
        with sf.SoundFile(path) as audio:
            yield audio
    except (sf.SoundFileError, OSError) as error:
        msg = f"A stem could not be read: {path.name}"
        raise QualityAnalysisError(msg) from error


def read_spectrogram(path: Path) -> Spectrogram:
    """Mel spectrogram of one stem file.

    Raises:
        QualityAnalysisError: The file cannot be opened or decoded.
    """
    with _opened(path) as audio:
        reader = _Reader(audio)
        while not reader.done:
            reader.step()
        return reader.mel.finish()


def _sum_padded(chunks: Sequence[FloatArray]) -> FloatArray:
    total = np.zeros(max(len(chunk) for chunk in chunks), dtype=np.float32)
    for chunk in chunks:
        total[: len(chunk)] += chunk
    return total


def _check_rates(files: Sequence[sf.SoundFile]) -> None:
    if len({audio.samplerate for audio in files}) > 1:
        msg = "The standard stems have different sample rates"
        raise QualityAnalysisError(msg)


def read_standard(paths: Mapping[str, Path]) -> tuple[dict[str, Spectrogram], Spectrogram]:
    """Spectrograms of the standard stems and of their sum (the mix), in one pass.

    A shorter stem is padded with silence in the mix.

    Raises:
        QualityAnalysisError: A file cannot be read, or the stems have different sample rates.
    """
    with ExitStack() as stack:
        files = [stack.enter_context(_opened(path)) for path in paths.values()]
        _check_rates(files)
        readers = [_Reader(audio) for audio in files]
        mix = _mix_of(readers)
        return {name: reader.mel.finish() for name, reader in zip(paths, readers, strict=True)}, mix


def _mix_of(readers: Sequence[_Reader]) -> Spectrogram:
    """Step every reader in lockstep and analyse the sum of their samples."""
    mix = _MelStream()
    while not all(reader.done for reader in readers):
        mix.push(_sum_padded([reader.step() for reader in readers]))
    return mix.finish()
