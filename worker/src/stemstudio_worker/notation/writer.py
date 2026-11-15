"""music21 score -> MusicXML 4.0 and Standard MIDI file bytes (data-model.md 3, ADR-017 D6)."""

from music21 import stream as m21_stream
from music21.midi import translate as m21_midi
from music21.musicxml import m21ToXml


def musicxml_bytes(score: m21_stream.Score) -> bytes:
    """Return the uncompressed MusicXML 4.0 document of ``score``."""
    return bytes(m21ToXml.GeneralObjectExporter(score).parse())


def midi_bytes(score: m21_stream.Score) -> bytes:
    """Return the Standard MIDI file of ``score`` (constant tempo, tied notes merged)."""
    return bytes(m21_midi.streamToMidiFile(score).writestr())
