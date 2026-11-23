---
id: chords
title: "Chords"
area: instruments
summary: "Chord symbols for the whole song: a lane in the mixer, chords above the karaoke words and a chord sheet on the track page."
feature: chords.view
anchors: ["chords", "chord-sheet"]
keywords: ["chords", "chord lane", "chord sheet", "harmony", "symbols", "transpose", "keys", "guitar", "accompaniment"]
related: [chords-advanced, karaoke]
reviewed: "016"
---

StemStudio finds the chords of a song by itself once the stems are ready, from every stem except drums and vocals. You
do not need to click anything; the status next to **Chords lane** shows the progress. If a song has no chords yet,
**Detect chords** starts the detection.

- The **Chords lane** in the mixer shows the current chord, the one before and the next ones, bar by bar, in time with
  playback. It follows the loop, the speed and every seek. **Chords lane** shows or hides it; StemStudio remembers your
  choice in this browser.
- While lyrics are shown, the chords also appear **above the karaoke words**, on the word where each chord starts.
- The **chord sheet** on the track page puts the chords above the lyric lines, for reading along or printing. Rows
  without words list the chords of instrumental parts. A screen reader reads the sheet as plain text.
- Chords follow the **transpose** of the practice panel: transpose +2 and every chord moves up a whole tone. Chords are
  spelled for the key of the part they are in (for example F# in D major, Gb in Db major).
- Guitarists can use capo mode to see the shapes to play with a capo.

**Limits**: chord detection is a practice aid. It is right for about 80 % of the time on pop songs and less on dense
jazz harmony or songs without clear chords; check by ear and, from the Advanced level, [correct a chord](help:chords-advanced).

The accurate method uses the BTC model (Park et al. 2019, MIT licence); the fast method uses a chroma template match.
