"""Tests for the labelled take FLAC writer and file hashes (009 T018, FR-008)."""

import hashlib
import json
import shutil
import sys
from collections.abc import Sequence
from pathlib import Path
from typing import Any

import numpy as np
import pytest
import soundfile as sf

from stemstudio_generator.audio.commands import CommandResult, SubprocessRunner
from stemstudio_generator.audio.flac import (
    AI_COMMENT,
    FlacWriter,
    TakeLabels,
    ffmpeg_flac_args,
    file_sha256,
    vorbis_comments,
)
from stemstudio_generator.ports import GeneratorError

from ..signals import stereo, tone, write

GENERATION = "0123456789abcdef0123456789abcdef"
RESTYLE = TakeLabels("ACE-Step 1.5 (19671f4)", GENERATION, 2)


class FakeFfmpeg:
    """Pretends to be ffmpeg: converts ``-i file:<src>`` to the last argument with soundfile."""

    def __init__(self, returncode: int = 0, write_output: bool = True) -> None:
        self.calls: list[list[str]] = []
        self.returncode = returncode
        self.write_output = write_output

    def run(self, args: Sequence[str], timeout_seconds: float) -> CommandResult:
        self.calls.append(list(args))
        assert timeout_seconds > 0
        if self.write_output:
            source = Path(args[args.index("-i") + 1].removeprefix("file:"))
            data, rate = sf.read(source, dtype="float32", always_2d=True)
            sf.write(Path(args[-1].removeprefix("file:")), data, rate, subtype="PCM_24")
        return CommandResult(self.returncode, "", "secret tool output")


def test_vorbis_comments_of_a_restyle_take() -> None:
    assert vorbis_comments(RESTYLE) == [
        ("AI_GENERATED", "true"),
        ("GENERATOR", "ACE-Step 1.5 (19671f4)"),
        ("STEMSTUDIO_GENERATION", GENERATION),
        ("STEMSTUDIO_TAKE", "2"),
        ("COMMENT", AI_COMMENT),
    ]
    assert AI_COMMENT == "AI-generated with StemStudio"


def test_vorbis_comments_add_the_voice_label_and_profile() -> None:
    labels = TakeLabels("SoulX-Singer-SVC (40493ad)", GENERATION, 1, "converted", "My\nvoice")

    comments = dict(vorbis_comments(labels))

    assert comments["AI_VOICE"] == "converted"
    assert comments["AI_VOICE_PROFILE"] == "My voice"


def test_vorbis_comments_leave_out_the_voice_label_none() -> None:
    comments = dict(vorbis_comments(TakeLabels("g", GENERATION, 1, "none")))

    assert "AI_VOICE" not in comments
    assert "AI_VOICE_PROFILE" not in comments


def test_ffmpeg_arguments_are_a_list_with_file_urls_and_24_bit_48k_stereo(tmp_path: Path) -> None:
    args = ffmpeg_flac_args("ffmpeg", tmp_path / "-in.wav", tmp_path / "out.flac", RESTYLE)

    assert args[0] == "ffmpeg"
    assert f"file:{(tmp_path / '-in.wav').resolve()}" in args
    assert args[-1] == f"file:{(tmp_path / 'out.flac').resolve()}"
    joined = " ".join(args)
    for option in ("-c:a flac", "-sample_fmt s32", "-bits_per_raw_sample 24", "-ar 48000",
                   "-ac 2", "-map_metadata -1", "-nostdin", "-y"):  # fmt: skip
        assert option in joined
    metadata = [args[i + 1] for i, a in enumerate(args) if a == "-metadata"]
    assert metadata[0] == "AI_GENERATED=true"
    assert f"STEMSTUDIO_GENERATION={GENERATION}" in metadata
    assert "COMMENT=AI-generated with StemStudio" in metadata


def test_writer_returns_hash_size_duration_and_peak(tmp_path: Path) -> None:
    source = write(tmp_path / "take.flac", stereo(tone(2.0, amplitude=0.5)))
    runner = FakeFfmpeg()

    written = FlacWriter(runner).write(source, tmp_path / "out" / "take-2.flac", RESTYLE)

    data = written.path.read_bytes()
    assert written.sha256 == hashlib.sha256(data).hexdigest()
    assert written.bytes == len(data)
    assert written.duration_seconds == pytest.approx(2.0)
    assert written.peak_dbfs == pytest.approx(-6.02, abs=0.01)
    assert runner.calls[0][0] == "ffmpeg"


def test_writer_failure_hides_the_tool_output(tmp_path: Path) -> None:
    source = write(tmp_path / "take.flac", stereo(tone(0.5)))

    with pytest.raises(GeneratorError, match="could not be written") as info:
        FlacWriter(FakeFfmpeg(returncode=1)).write(source, tmp_path / "o.flac", RESTYLE)

    assert "secret" not in str(info.value)
    assert info.value.code == "engineError"


def test_writer_failure_when_ffmpeg_wrote_nothing(tmp_path: Path) -> None:
    source = write(tmp_path / "take.flac", stereo(tone(0.5)))

    with pytest.raises(GeneratorError, match="could not be written"):
        FlacWriter(FakeFfmpeg(write_output=False)).write(source, tmp_path / "o.flac", RESTYLE)


def test_file_sha256_streams_the_file(tmp_path: Path) -> None:
    path = tmp_path / "x.bin"
    path.write_bytes(b"a" * 3_000_000)

    assert file_sha256(path) == hashlib.sha256(b"a" * 3_000_000).hexdigest()


def test_subprocess_runner_captures_the_output_of_a_program() -> None:
    result = SubprocessRunner().run([sys.executable, "-c", "print('ok')"], 30.0)

    assert (result.returncode, result.stdout.strip()) == (0, "ok")


def test_subprocess_runner_reports_a_missing_program() -> None:
    result = SubprocessRunner().run(["stemstudio-no-such-program-xyz"], 5.0)

    assert result.returncode == -1
    assert result.stderr


def _ffprobe(path: Path) -> dict[str, Any]:
    args = ["ffprobe", "-v", "error", "-show_streams", "-show_format", "-of", "json"]
    result: dict[str, Any] = json.loads(SubprocessRunner().run([*args, f"file:{path}"], 30).stdout)
    return result


@pytest.mark.skipif(shutil.which("ffmpeg") is None or shutil.which("ffprobe") is None,
                    reason="ffmpeg/ffprobe not installed")  # fmt: skip
def test_real_ffmpeg_writes_24_bit_48k_stereo_with_the_tags(tmp_path: Path) -> None:
    source = write(tmp_path / "take.wav", tone(1.0, 24_000)[:, None], 24_000, "FLOAT")
    labels = TakeLabels("SoulX-Singer-SVC (40493ad)", GENERATION, 1, "converted", "My voice")

    written = FlacWriter().write(source, tmp_path / "take-1.flac", labels)

    info = _ffprobe(written.path)
    stream = info["streams"][0]
    tags = {k.upper(): v for k, v in info["format"]["tags"].items()}
    assert (stream["codec_name"], stream["sample_rate"], stream["channels"]) == ("flac", "48000", 2)
    assert stream["bits_per_raw_sample"] == "24"
    assert tags["AI_GENERATED"] == "true"
    assert tags["AI_VOICE"] == "converted"
    assert tags["AI_VOICE_PROFILE"] == "My voice"
    assert tags["STEMSTUDIO_TAKE"] == "1"
    assert written.duration_seconds == pytest.approx(1.0, abs=1e-3)
    assert np.isfinite(written.peak_dbfs)
