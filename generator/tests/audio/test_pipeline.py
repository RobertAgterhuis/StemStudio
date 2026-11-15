"""``AudioStemMixer``: the ``StemMixer`` port over the audio helpers (009 US1)."""

from pathlib import Path

import pytest
import soundfile as sf

from stemstudio_generator.audio.pipeline import AudioStemMixer
from stemstudio_generator.ports import InvalidInputError, StemMixer

from ..signals import stereo, tone, write


def _stem(tmp_path: Path, name: str, seconds: float, amplitude: float = 0.3) -> Path:
    return write(tmp_path / name, stereo(tone(seconds, amplitude=amplitude)))


def test_measure_is_the_length_of_the_summed_stems(tmp_path: Path) -> None:
    mixer = AudioStemMixer()

    assert isinstance(mixer, StemMixer)
    assert mixer.measure([_stem(tmp_path, "a.wav", 12.0), _stem(tmp_path, "b.wav", 12.0)]) == (
        pytest.approx(12.0)
    )


def test_measure_refuses_a_bad_stem_count(tmp_path: Path) -> None:
    with pytest.raises(InvalidInputError, match="1 to 6 stems"):
        AudioStemMixer().measure([])


def test_mix_cuts_the_range_and_writes_a_wav(tmp_path: Path) -> None:
    dest = tmp_path / "work" / "source.wav"

    mixed = AudioStemMixer().mix([_stem(tmp_path, "a.wav", 30.0)], dest, (5.0, 20.0))

    assert mixed.path == dest
    assert mixed.duration_seconds == pytest.approx(15.0)
    assert not mixed.clipped
    assert sf.info(dest).duration == pytest.approx(15.0)


def test_mix_without_a_cut_keeps_the_whole_sum_and_reports_clipping(tmp_path: Path) -> None:
    stems = [_stem(tmp_path, f"{index}.wav", 11.0, amplitude=0.6) for index in range(3)]

    mixed = AudioStemMixer().mix(stems, tmp_path / "source.wav", None)

    assert mixed.duration_seconds == pytest.approx(11.0)
    assert mixed.clipped
