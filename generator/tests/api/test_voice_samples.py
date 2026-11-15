"""``POST /v1/voice-samples/check`` and the one-time download (010 T039, FR-005, FR-007)."""

import hashlib
from collections.abc import Iterator
from pathlib import Path
from typing import Any

import numpy as np
import pytest
import soundfile as sf

from stemstudio_generator.api.uploads import UploadLimits
from stemstudio_generator.config import CHECKED_SAMPLE_KEEP_SECONDS

from ..contract.docs import V110
from ..signals import noise, tone, write
from .stack import Stack, open_stack, settings_for


@pytest.fixture
def singer(tmp_path: Path) -> Iterator[Stack]:
    """The fake singer (kind voiceConversion, voice-sample checks on)."""
    yield from open_stack(settings_for(tmp_path, kinds=("voiceConversion",)))


def _check(stack: Stack, path: Path, content_type: str = "audio/wav") -> Any:
    files = {"sample": (path.name, path.read_bytes(), content_type)}
    return stack.client.post("/v1/voice-samples/check", files=files)


def _ok_body(response: Any) -> dict[str, Any]:
    assert response.status_code == 200, response.text
    body: dict[str, Any] = response.json()
    assert V110.errors("VoiceSampleCheck", body) == []
    return body


def _voice(tmp_path: Path, seconds: float, rate: int = 44_100) -> Path:
    return write(tmp_path / f"voice-{seconds}.wav", tone(seconds, rate, freq=220.0), rate)


def test_a_sung_sample_of_20_seconds_is_ok_and_downloadable_once(
    singer: Stack, tmp_path: Path
) -> None:
    body = _ok_body(_check(singer, _voice(tmp_path, 20.0)))

    assert body["ok"] is True
    assert (body["reason"], body["message"]) == (None, None)
    assert body["durationSeconds"] == pytest.approx(20.0, abs=0.01)
    assert body["voicedRatio"] >= 0.9
    audio = singer.client.get(f"/v1/voice-samples/{body['checkId']}/audio")
    assert audio.status_code == 200
    assert audio.headers["content-type"] == "audio/flac"
    flac = tmp_path / "normalised.flac"
    flac.write_bytes(audio.content)
    data, rate = sf.read(flac, dtype="int16")
    assert (rate, data.ndim) == (48_000, 1)
    assert hashlib.sha256(data.astype("<i2").tobytes()).hexdigest() == body["pcmSha256"]
    assert singer.client.get(f"/v1/voice-samples/{body['checkId']}/audio").status_code == 404


@pytest.mark.parametrize(
    ("seconds", "expected"),
    [
        (6.0, ("tooShort", "The recording is 6.0 s long; at least 10 s is needed.")),
        (61.0, ("tooLong", "The recording is 61.0 s long; at most 60 s is allowed.")),
    ],
)
def test_samples_outside_10_to_60_seconds_are_refused(
    singer: Stack, tmp_path: Path, seconds: float, expected: tuple[str, str]
) -> None:
    body = _ok_body(_check(singer, _voice(tmp_path, seconds)))

    reason, message = expected
    assert (body["ok"], body["reason"]) == (False, reason)
    assert body["message"].startswith(message)
    assert singer.client.get(f"/v1/voice-samples/{body['checkId']}/audio").status_code == 404


def test_noise_is_refused_as_unvoiced(singer: Stack, tmp_path: Path) -> None:
    path = write(tmp_path / "noise.wav", noise(20.0, 44_100), 44_100)

    body = _ok_body(_check(singer, path))

    assert (body["ok"], body["reason"], body["voicedRatio"]) == (False, "unvoiced", 0.0)
    assert body["message"] == (
        "The recording contains too little singing (0 % voiced, at least 50 % is needed). "
        "Sing for 20-30 seconds without background music."
    )


def test_mode_unvoiced_sample_refuses_every_sample(tmp_path: Path) -> None:
    settings = settings_for(tmp_path, "unvoiced-sample", kinds=("voiceConversion",))
    for stack in open_stack(settings):
        assert _ok_body(_check(stack, _voice(tmp_path, 20.0)))["reason"] == "unvoiced"


def test_stereo_flac_samples_are_mixed_down(singer: Stack, tmp_path: Path) -> None:
    stereo = np.column_stack([tone(15.0, 48_000, freq=220.0)] * 2)
    path = write(tmp_path / "voice.flac", stereo, 48_000)

    assert _ok_body(_check(singer, path, "audio/flac"))["ok"] is True


def test_formats_soundfile_cannot_read_are_decoded_with_ffmpeg(
    singer: Stack, tmp_path: Path
) -> None:
    calls: list[list[str]] = []
    wav = _voice(tmp_path, 12.0)

    def run(args: list[str], timeout_seconds: float) -> Any:
        calls.append(list(args))
        Path(args[-1].removeprefix("file:")).write_bytes(wav.read_bytes())
        return type("Result", (), {"returncode": 0, "stderr": ""})()

    singer.composition.app.state.context.analyzer.decoder.run = run
    recording = tmp_path / "recording.webm"
    recording.write_bytes(b"\x1aE\xdf\xa3 webm/opus from the browser")

    body = _ok_body(_check(singer, recording, "audio/webm"))

    assert body["ok"] is True
    assert calls[0][0] == "ffmpeg"
    assert calls[0][calls[0].index("-i") + 1].startswith("file:")


def test_unreadable_samples_are_400(singer: Stack, tmp_path: Path) -> None:
    def fail(args: list[str], timeout_seconds: float) -> Any:
        return type("Result", (), {"returncode": 1, "stderr": "Invalid data"})()

    singer.composition.app.state.context.analyzer.decoder.run = fail
    broken = tmp_path / "broken.wav"
    broken.write_bytes(b"not audio at all")

    response = _check(singer, broken)

    assert response.status_code == 400
    assert response.json() == {
        "type": "about:blank", "title": "Invalid input", "status": 400,
        "detail": "The sample could not be read as audio.", "code": "invalidInput",
    }  # fmt: skip


def test_a_missing_sample_part_is_400(singer: Stack) -> None:
    response = singer.client.post("/v1/voice-samples/check", data={"other": "x"})

    assert response.status_code == 400
    assert response.json()["code"] == "invalidInput"


def test_a_sample_above_20_mib_is_413(singer: Stack, tmp_path: Path) -> None:
    singer.composition.app.state.context.limits = UploadLimits(sample_bytes=1 << 20)
    big = write(tmp_path / "big.wav", tone(12.0, 48_000), 48_000, "FLOAT")

    response = _check(singer, big)

    assert response.status_code == 413
    assert response.json()["detail"] == "The sample is larger than 1 MiB (1,048,576 bytes)."


def test_a_checked_sample_expires_after_ten_minutes(singer: Stack, tmp_path: Path) -> None:
    body = _ok_body(_check(singer, _voice(tmp_path, 20.0)))

    singer.clock.advance(CHECKED_SAMPLE_KEEP_SECONDS + 1)

    assert singer.client.get(f"/v1/voice-samples/{body['checkId']}/audio").status_code == 404
    assert list((singer.settings.data_dir / "samples").glob("*.flac")) == []


def test_unknown_or_malformed_check_ids_are_404(singer: Stack) -> None:
    assert singer.client.get(f"/v1/voice-samples/{'a' * 32}/audio").status_code == 404
    assert singer.client.get("/v1/voice-samples/zz/audio").status_code == 404


def test_the_generator_service_does_not_check_samples(stack: Stack, tmp_path: Path) -> None:
    response = _check(stack, _voice(tmp_path, 20.0))

    assert response.status_code == 409
    assert response.json()["code"] == "kindNotServed"
    assert response.json()["detail"] == "This service does not check voice samples."
    assert stack.client.get(f"/v1/voice-samples/{'a' * 32}/audio").status_code == 409


def test_checks_work_without_a_gpu(tmp_path: Path) -> None:
    settings = settings_for(tmp_path, "no-gpu", kinds=("voiceConversion",))
    for stack in open_stack(settings):
        assert _ok_body(_check(stack, _voice(tmp_path, 20.0)))["ok"] is True


class _SpyDecoder:
    """Decoder that records calls and writes a decoded WAV of ``seconds``."""

    def __init__(self, seconds: float = 12.0) -> None:
        self.calls: list[list[str]] = []
        self.seconds = seconds

    def run(self, args: list[str], timeout_seconds: float) -> Any:
        self.calls.append(list(args))
        write(Path(args[-1].removeprefix("file:")), tone(self.seconds, 48_000), 48_000)
        return type("Result", (), {"returncode": 0, "stderr": ""})()


def _spy(stack: Stack, seconds: float = 12.0) -> _SpyDecoder:
    spy = _SpyDecoder(seconds)
    stack.composition.app.state.context.analyzer.decoder = spy
    return spy


@pytest.mark.parametrize(
    ("name", "content"),
    [
        ("sample.wav", b"#EXTM3U\n#EXT-X-VERSION:3\n#EXTINF:10,\nhttp://169.254.169.254/latest\n"),
        ("sample.webm", b"ffconcat version 1.0\nfile '/etc/passwd'\n"),
        ("sample.mp3", b"<?xml version='1.0'?><MPD></MPD>"),
    ],
)
def test_playlists_disguised_as_audio_never_reach_ffmpeg(
    singer: Stack, tmp_path: Path, name: str, content: bytes
) -> None:
    spy = _spy(singer)
    path = tmp_path / name
    path.write_bytes(content)

    response = _check(singer, path)

    assert response.status_code == 400
    assert response.json()["detail"] == "The sample could not be read as audio."
    assert spy.calls == []


@pytest.mark.parametrize(
    ("magic", "demuxer"),
    [
        (b"\x1aE\xdf\xa3", "matroska"),
        (b"OggS\x00\x02" + b"\x00" * 22 + b"OpusHead", "ogg"),
        (b"\x00\x00\x00\x20ftypisom", "mov"),
    ],
)
def test_ffmpeg_gets_an_explicit_demuxer_a_file_only_whitelist_and_a_time_cap(
    singer: Stack, tmp_path: Path, magic: bytes, demuxer: str
) -> None:
    spy = _spy(singer)
    recording = tmp_path / "recording.bin"
    recording.write_bytes(magic + b"\x00" * 64)

    assert _ok_body(_check(singer, recording, "audio/webm"))["ok"] is True

    [args] = spy.calls
    input_at = args.index("-i")
    assert args[args.index("-f") : args.index("-f") + 2] == ["-f", demuxer]
    assert args.index("-f") < input_at
    assert args[args.index("-protocol_whitelist") + 1] == "file"
    assert args[args.index("-t") + 1] == "61"


def test_a_long_stream_is_decoded_only_up_to_the_cap_and_refused_as_too_long(
    singer: Stack, tmp_path: Path
) -> None:
    _spy(singer, seconds=61.0)
    recording = tmp_path / "long.webm"
    recording.write_bytes(b"\x1aE\xdf\xa3" + b"\x00" * 64)

    body = _ok_body(_check(singer, recording, "audio/webm"))

    assert (body["ok"], body["reason"]) == (False, "tooLong")


def test_a_long_soundfile_sample_is_refused_before_decoding(
    singer: Stack, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    def no_decode(path: Path) -> Any:
        pytest.fail("a sample over the cap must not be decoded")

    monkeypatch.setattr("stemstudio_generator.audio.samples.read_audio", no_decode)
    long_wav = write(tmp_path / "long.wav", np.zeros(90 * 8_000, np.float32), 8_000)

    body = _ok_body(_check(singer, long_wav))

    assert (body["ok"], body["reason"]) == (False, "tooLong")
    assert body["durationSeconds"] == pytest.approx(90.0)
