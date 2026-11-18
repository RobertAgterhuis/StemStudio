# Sheet music

Turn a stem into a practice lead sheet: a PDF to print, plus MusicXML and MIDI files for
your notation program.

## Creating sheet music

1. On the track page press **Sheet music** next to a stem or a pitched detail stem. You can
   also start from an audio item in the library.
2. The dialog suggests a **profile** and shows its limits, an estimated time and a short
   copyright notice.
3. Press **Generate**. The result appears in the library as an item of kind *Sheet music*.

| Profile | Staff | Notes |
|---|---|---|
| Vocals | Treble clef | One voice; quantised to eighth notes |
| Bass | Bass clef (sounding an octave lower) | One voice |
| Guitar | Treble clef (sounding an octave lower) | |
| Keys | Grand staff, split at middle C | |
| Piano | Grand staff | *Accurate* (best with a GPU) or *Fast* |
| Drums | Percussion staff | Needs split drums (detail stems) |

If the drums are not split yet, the dialog offers **Split drums first**.

## What you get

- An A4 **PDF** that you can preview in the browser, plus **MusicXML** and **MIDI**
  downloads.
- The notes follow the song's beat grid: bar 1 starts at the first downbeat, the tempo mark
  is the rounded BPM, and notes are quantised to sixteenths (eighths for vocals and in
  *simplified* mode).
- The estimated **key** with its confidence, for example *A-flat major (83 %)*. At Advanced
  level you can override the key.
- Not included: triplets, swing, dynamics, articulations, lyrics, chords and tablature.

## When the beat grid is unreliable

If StemStudio could not detect a reliable tempo, you enter the **tempo** (30 to 300 BPM),
**beats per bar** and the **first downbeat** yourself. The values from the metronome
corrections are filled in for you.

## Timing and messages

A melodic stem or drums take at most about two minutes for a five-minute song. The accurate
piano profile on a CPU takes longer (about two minutes for a 4-minute song on an 8-thread
CPU; the dialog shows the estimate). The first piano transcription downloads a 172 MB model,
which adds about a minute.

Typical notes on the result: *"The key is uncertain (confidence 52 %)."*, *"Very few notes
were detected."*, *"Detail stems can contain other instruments; expect extra notes."* When no
notes are found at all, the item fails with *"No notes were detected in this audio."*

The piano model is published under CC BY 4.0; its attribution is shown with every piano
item.

## Reading along in the mixer

Open **Score** in the mixer to read the sheet music while you play: the bar you hear is marked
and the page scrolls along (chapter 8).
