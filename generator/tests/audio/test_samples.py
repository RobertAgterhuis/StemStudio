"""Container sniffing of voice-sample uploads (security review of 010 US3)."""

from collections.abc import Sequence
from pathlib import Path

import numpy as np
import pytest

from stemstudio_generator.audio.commands import CommandResult
from stemstudio_generator.audio.samples import SampleAnalyzer, container
from stemstudio_generator.ports import InvalidInputError

from ..signals import tone, write


@pytest.mark.parametrize(
    ("header", "expected"),
    [
        (b"RIFF\x00\x00\x00\x00WAVEfmt ", "soundfile"),
        (b"fLaC\x00\x00\x00\x22", "soundfile"),
        (b"ID3\x04\x00\x00\x00\x00", "soundfile"),
        (b"\xff\xfb\x90\x64\x00", "soundfile"),
        (b"OggS\x00\x02", "soundfile"),
        (b"\x1aE\xdf\xa3\x01\x00", "matroska"),
        (b"\x00\x00\x00\x18ftypmp42", "mov"),
        (b"RIFF\x00\x00\x00\x00AVI LIST", None),
        (b"#EXTM3U\n", None),
        (b"ffconcat version 1.0", None),
        (b"\xff", None),
        (b"", None),
    ],
)
def test_only_known_audio_containers_are_accepted(header: bytes, expected: str | None) -> None:
    assert container(header) == expected


class _Tracker:
    def f0(self, audio: Path) -> np.ndarray:
        return np.full(100, 200.0, np.float32)


class _FailingDecoder:
    def __init__(self) -> None:
        self.calls = 0

    def run(self, args: Sequence[str], timeout_seconds: float) -> CommandResult:
        self.calls += 1
        return CommandResult(returncode=1, stdout="", stderr="Invalid data")


def test_a_failing_ffmpeg_decode_is_unreadable(tmp_path: Path) -> None:
    decoder = _FailingDecoder()
    upload = tmp_path / "rec.webm"
    upload.write_bytes(b"\x1aE\xdf\xa3" + b"\x00" * 32)

    with pytest.raises(InvalidInputError, match="could not be read"):
        SampleAnalyzer(_Tracker(), decoder).analyze(upload, tmp_path / "out.flac")
    assert decoder.calls == 1


def test_a_wav_with_a_broken_header_is_unreadable_without_ffmpeg(tmp_path: Path) -> None:
    decoder = _FailingDecoder()
    upload = tmp_path / "broken.wav"
    upload.write_bytes(b"RIFF\x00\x00\x00\x00WAVE" + b"\x00" * 8)

    with pytest.raises(InvalidInputError):
        SampleAnalyzer(_Tracker(), decoder).analyze(upload, tmp_path / "out.flac")
    assert decoder.calls == 0


def test_a_soundfile_decode_error_after_a_good_header_is_unreadable(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def broken(path: Path) -> object:
        raise InvalidInputError("The sample could not be read as audio.")

    monkeypatch.setattr("stemstudio_generator.audio.samples.read_audio", broken)
    upload = write(tmp_path / "voice.wav", tone(12.0, 16_000), 16_000)

    with pytest.raises(InvalidInputError):
        SampleAnalyzer(_Tracker(), _FailingDecoder()).analyze(upload, tmp_path / "out.flac")
