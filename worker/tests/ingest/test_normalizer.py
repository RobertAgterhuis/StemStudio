"""T047: ffmpeg normaliser with a fake ``CommandRunner`` (no real subprocess)."""

import hashlib
import json
import subprocess
import wave
from collections.abc import Sequence
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import pytest

from stemstudio_worker.ingest import normalizer as normalizer_module
from stemstudio_worker.ingest.normalizer import (
    CommandResult,
    FfmpegNormalizer,
    SubprocessRunner,
)
from stemstudio_worker.ports import AudioDecodeError, AudioTooLongError


def _probe_json(duration: str | None = "12.5", audio: bool = True) -> str:
    fmt: dict[str, Any] = {} if duration is None else {"duration": duration}
    streams = [{"codec_type": "audio"}] if audio else []
    return json.dumps({"streams": streams, "format": fmt})


@dataclass
class WavSpec:
    frames: int = 44_100 * 2
    rate: int = 44_100
    channels: int = 2
    width: int = 2


def write_wav(path: Path, spec: WavSpec) -> None:
    with wave.open(str(path), "wb") as handle:
        handle.setnchannels(spec.channels)
        handle.setsampwidth(spec.width)
        handle.setframerate(spec.rate)
        handle.writeframes(b"\x01\x00" * spec.channels * spec.frames * spec.width // 2)


@dataclass
class FakeRunner:
    """Answers ffprobe with canned JSON and makes ffmpeg write a WAV to its last argument."""

    probe: CommandResult = field(default_factory=lambda: CommandResult(0, _probe_json(), ""))
    convert_code: int = 0
    wav: WavSpec | None = field(default_factory=WavSpec)
    raw_output: bytes | None = None
    calls: list[tuple[list[str], float]] = field(default_factory=list)

    def run(self, args: Sequence[str], timeout_seconds: float) -> CommandResult:
        self.calls.append((list(args), timeout_seconds))
        if args[0] == "ffprobe":
            return self.probe
        output = Path(args[-1].removeprefix("file:"))
        if self.raw_output is not None:
            output.write_bytes(self.raw_output)
        elif self.wav is not None and self.convert_code == 0:
            write_wav(output, self.wav)
        return CommandResult(self.convert_code, "", "Invalid data found when processing input")


def _paths(tmp_path: Path) -> tuple[Path, Path]:
    source = tmp_path / "source.webm"
    source.write_bytes(b"compressed")
    return source, tmp_path / "audio.wav"


def test_probe_and_convert_use_exact_argument_lists(tmp_path: Path) -> None:
    runner = FakeRunner()
    source, output = _paths(tmp_path)

    FfmpegNormalizer(runner).normalize(source, output)

    (probe_args, _), (convert_args, _) = runner.calls
    assert probe_args == [
        "ffprobe",
        "-v",
        "error",
        "-select_streams",
        "a:0",
        "-show_entries",
        "stream=codec_type:format=duration",
        "-of",
        "json",
        f"file:{source.resolve()}",
    ]
    assert convert_args == [
        "ffmpeg",
        "-nostdin",
        "-hide_banner",
        "-loglevel",
        "error",
        "-y",
        "-i",
        f"file:{source.resolve()}",
        "-map",
        "0:a:0",
        "-vn",
        "-sn",
        "-dn",
        "-t",
        "901",
        "-ac",
        "2",
        "-ar",
        "44100",
        "-c:a",
        "pcm_s16le",
        "-f",
        "wav",
        f"file:{output.resolve()}",
    ]


def test_normalize_returns_duration_and_sha256_of_the_wav(tmp_path: Path) -> None:
    source, output = _paths(tmp_path)

    result = FfmpegNormalizer(FakeRunner()).normalize(source, output)

    assert result.wav_path == output
    assert result.duration_seconds == pytest.approx(2.0)
    assert result.sha256 == hashlib.sha256(output.read_bytes()).hexdigest()
    assert len(result.sha256) == 64


def test_custom_binaries_and_limit_are_used(tmp_path: Path) -> None:
    runner = FakeRunner()
    source, output = _paths(tmp_path)

    FfmpegNormalizer(runner, max_duration_seconds=60, ffmpeg="/opt/ffmpeg").normalize(
        source, output
    )

    convert_args = runner.calls[1][0]
    assert convert_args[0] == "/opt/ffmpeg"
    assert convert_args[convert_args.index("-t") + 1] == "61"


def test_probe_failure_is_a_decode_error(tmp_path: Path) -> None:
    runner = FakeRunner(probe=CommandResult(1, "", "moov atom not found"))

    with pytest.raises(AudioDecodeError) as info:
        FfmpegNormalizer(runner).normalize(*_paths(tmp_path))

    assert "moov" not in str(info.value)
    assert len(runner.calls) == 1


@pytest.mark.parametrize("stdout", ["not json", "[]", json.dumps({"streams": "x"})])
def test_unparsable_probe_output_is_a_decode_error(tmp_path: Path, stdout: str) -> None:
    with pytest.raises(AudioDecodeError):
        FfmpegNormalizer(FakeRunner(probe=CommandResult(0, stdout, ""))).normalize(
            *_paths(tmp_path)
        )


def test_no_audio_stream_is_a_decode_error(tmp_path: Path) -> None:
    runner = FakeRunner(probe=CommandResult(0, _probe_json(audio=False), ""))

    with pytest.raises(AudioDecodeError, match="no audio stream"):
        FfmpegNormalizer(runner).normalize(*_paths(tmp_path))


def test_probed_duration_over_limit_fails_before_conversion(tmp_path: Path) -> None:
    runner = FakeRunner(probe=CommandResult(0, _probe_json("900.01"), ""))

    with pytest.raises(AudioTooLongError) as info:
        FfmpegNormalizer(runner).normalize(*_paths(tmp_path))

    assert str(info.value) == "Audio is longer than 15 minutes"
    assert len(runner.calls) == 1


@pytest.mark.parametrize("duration", [None, "N/A", "900"])
def test_unknown_or_boundary_probe_duration_is_converted(
    tmp_path: Path, duration: str | None
) -> None:
    runner = FakeRunner(probe=CommandResult(0, _probe_json(duration), ""))

    FfmpegNormalizer(runner).normalize(*_paths(tmp_path))

    assert len(runner.calls) == 2


def test_converted_duration_over_limit_is_too_long(tmp_path: Path) -> None:
    runner = FakeRunner(
        probe=CommandResult(0, _probe_json(None), ""), wav=WavSpec(frames=44_100 * 61)
    )

    with pytest.raises(AudioTooLongError, match="longer than 1 minutes"):
        FfmpegNormalizer(runner, max_duration_seconds=60).normalize(*_paths(tmp_path))


def test_non_zero_ffmpeg_exit_is_a_decode_error(tmp_path: Path) -> None:
    with pytest.raises(AudioDecodeError) as info:
        FfmpegNormalizer(FakeRunner(convert_code=1)).normalize(*_paths(tmp_path))

    assert "Invalid data" not in str(info.value)


@pytest.mark.parametrize(
    "runner",
    [
        FakeRunner(wav=None),
        FakeRunner(raw_output=b"RIFF garbage"),
        FakeRunner(wav=WavSpec(frames=0)),
        FakeRunner(wav=WavSpec(rate=48_000)),
        FakeRunner(wav=WavSpec(channels=1)),
        FakeRunner(wav=WavSpec(width=3)),
    ],
    ids=["missing", "corrupt", "empty", "rate", "mono", "24-bit"],
)
def test_invalid_converted_wav_is_a_decode_error(tmp_path: Path, runner: FakeRunner) -> None:
    with pytest.raises(AudioDecodeError):
        FfmpegNormalizer(runner).normalize(*_paths(tmp_path))


def test_normalizer_exposes_its_limit() -> None:
    assert FfmpegNormalizer().max_duration_seconds == 900.0


@dataclass
class RunSpy:
    outcome: Any
    calls: list[tuple[Any, dict[str, Any]]] = field(default_factory=list)

    def __call__(self, args: Any, **kwargs: Any) -> Any:
        self.calls.append((args, kwargs))
        if isinstance(self.outcome, BaseException):
            raise self.outcome
        return self.outcome


def test_subprocess_runner_passes_an_argument_list_without_shell(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    spy = RunSpy(subprocess.CompletedProcess(["ffprobe"], 0, "out", "err"))
    monkeypatch.setattr(normalizer_module.subprocess, "run", spy)

    result = SubprocessRunner().run(("ffprobe", "-v", "error"), timeout_seconds=5)

    assert result == CommandResult(0, "out", "err")
    args, kwargs = spy.calls[0]
    assert args == ["ffprobe", "-v", "error"]
    assert kwargs.get("shell", False) is False
    assert kwargs["capture_output"] is True
    assert kwargs["timeout"] == 5
    assert kwargs["check"] is False


@pytest.mark.parametrize(
    "error",
    [subprocess.TimeoutExpired(["ffmpeg"], 5), FileNotFoundError("ffmpeg")],
    ids=["timeout", "missing-binary"],
)
def test_subprocess_runner_maps_launch_failures_to_a_failed_result(
    monkeypatch: pytest.MonkeyPatch, error: BaseException
) -> None:
    monkeypatch.setattr(normalizer_module.subprocess, "run", RunSpy(error))

    result = SubprocessRunner().run(["ffmpeg"], timeout_seconds=5)

    assert result.returncode != 0
    assert result.stderr


@pytest.mark.integration
def test_real_ffmpeg_converts_a_generated_tone(tmp_path: Path) -> None:
    tone = tmp_path / "tone.flac"
    subprocess.run(  # noqa: S603 - fixed argument list, generated input
        [  # noqa: S607 - ffmpeg is resolved from PATH by design
            "ffmpeg",
            "-nostdin",
            "-loglevel",
            "error",
            "-f",
            "lavfi",
            "-i",
            "sine=frequency=440:sample_rate=48000:duration=2",
            str(tone),
        ],
        check=True,
        timeout=60,
    )

    result = FfmpegNormalizer().normalize(tone, tmp_path / "audio.wav")

    with wave.open(str(result.wav_path), "rb") as handle:
        assert handle.getframerate() == 44_100
        assert handle.getnchannels() == 2
        assert handle.getsampwidth() == 2
    assert result.duration_seconds == pytest.approx(2.0, abs=0.05)
