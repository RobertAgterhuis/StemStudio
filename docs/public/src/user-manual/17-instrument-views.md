# Instrument views

Every instrument counts the same in StemStudio. Each family gets a view made for it: chords for
keys and accompaniment, tablature for guitar and bass, a drum trainer for drummers, singing
feedback for singers and a part in the written pitch for strings, brass and woodwinds. One entry
point, **Practise as ...**, opens the view of your family with a matching minus-one mix.

## Practise as ...

1. Open the mixer of a separated song and press **Practise as ...**.
2. Choose what you play. StemStudio mutes every channel of that family (the stem and all its
   detail channels, also while their strips are collapsed) and unmutes and unsolos all other
   channels; volumes and pans stay as they are.
3. The view of the family opens below the chords.

| Family | View that opens |
|---|---|
| Vocals | Singing feedback: the pitch view and a score per line |
| Drums | Drum trainer: five hit lanes and the drum notation |
| Bass, Guitar | Tablature: the part with tab, or **Generate tab** |
| Keys | Chords: the chord sheet with the lyrics |
| Strings, brass, woodwinds | Part for your instrument: choose your instrument, then see or generate its part |

Your choice is remembered in this browser. The next time the mixer offers it again
(*"Practise as Keys again?"*) but does not apply it until you click. You leave a view by
choosing another family or another view in **Practise as ...**.

The *Play along* practice presets work the same way: they mute the detail channels of the family
too, so an expanded drum group no longer stays audible.

Limit: the mute follows the separated stems. An instrument that the separation put into another
stem (for example a piano in *other*) is not muted with its family.

## Chords

StemStudio finds the chords by itself once the stems are ready, from every stem except drums and
vocals. The status next to **Chords lane** shows the progress; if a song has no chords yet,
**Detect chords** starts the detection.

- The **chords lane** in the mixer shows the current chord, the one before and the next ones, bar
  by bar, in time with the playback. It follows the loop, the speed and every seek. **Chords
  lane** shows or hides it.
- While lyrics are shown, the chords also appear **above the karaoke words**, on the word where
  each chord starts.
- The **chord sheet** on the track page puts the chords above the lyric lines, for reading along
  or printing. Rows without words list the chords of instrumental parts. A screen reader reads the
  sheet as plain text.
- Chords follow the **transpose** of the practice panel (chapter 5) and are spelled for the key
  of the part they are in (F# in D major, Gb in Db major).

From the Advanced level:

- **Vocabulary**: *Simple* (major and minor only, often the most readable) or *Full* (sevenths,
  sixths, diminished, augmented, suspended chords and inversions such as Am7/G). After a change,
  **Detect again** uses the new vocabulary.
- **Correct a chord**: the pencil next to a chord in the chord sheet opens the editor (root,
  quality, bass note). Corrections are kept; a later **Detect again** asks before it replaces
  them.
- **ChordPro**: saves the chord sheet as a `.cho` file with your transpose and capo, for songbook
  apps.
- **Include chords** in the sheet music dialog puts chord symbols above the top staff (chapter 16).
- Expert chooses the **method**: *Accurate* (the BTC model, Park et al. 2019, MIT licence, about
  12 MB, fetched once) or *Fast* (a chroma template match; works offline and on slow computers,
  less accurate).

Limits: chord detection is a practice aid. It is right about 80 % of the time on pop songs and
less on dense jazz harmony or songs without clear chords; check by ear.

## The song key

The **key** stands next to the tempo on the track page and in the mixer, for every instrument. It
is found together with the chords.

- **Key changes**: the mixer shows the key of the part you hear (*"Now: D major"*); from Advanced
  the track page lists every key change with the bar it starts in.
- **Transpose**: the key you hear is shown with the original and the interval, for example
  *D major (original C major, +2)*.
- **Your part**: when you practise as strings, brass or woodwinds with a transposing instrument,
  the key of your written part is shown too (*"Your part: A major"* for an alto saxophone in a
  C major song).
- **How sure**: a key marked *(uncertain)* was hard to detect. From Advanced the confidence and
  the closest other key are shown, for example *81 % (or A minor)*.

Limit: the key comes from the chords, so songs with few chords, modal songs and one-chord songs
can show a relative key (A minor instead of C major) or a wrong one.

## Capo mode (guitar)

After **Practise as Guitar**, **Capo mode** appears next to the chords in the mixer and on the
track page. With a capo on fret *n* the chords are shown as the **shapes** you play (C, G, Am, F
for a song in E-flat with capo 3); the song sounds unchanged and a *Capo 3* badge reminds you
where the capo goes.

- **Suggestions**: up to three capo positions (0 to 7) with the key of the shapes and how many of
  them are open chords, best first. One click applies a suggestion.
- **After a transpose** up (+1 to +7), *Capo n: play the original shapes* keeps the shapes of the
  original song; for -1 to -5 the capo 12 + transpose is offered when it is 7 or lower.
- **Sounding chords** (from Advanced) shows the chord that sounds next to each shape, for example
  *C (Eb)*.
- The chords lane, the karaoke banner, the chord sheet, the ChordPro file and the tablature
  options use the capo. The capo is remembered per song in this browser.

Capo mode is for the guitar family only: bass and keys have no capo. The suggestions count open
shapes; barre chords, voicings and the real difficulty are not taken into account.

## Tablature (guitar and bass)

Sheet music for the **guitar** and **bass** profiles can include tablature, one line per string
with the fret to play.

- **Layout**: *Notation and tab* (the default), *Tab only* or *No tab*.
- **Tuning**: Beginner offers standard tuning and drop D; from Advanced every tuning, such as half
  a step down, DADGAD, open G, and 5- and 6-string bass.
- **Capo** (guitar, from Advanced): frets are counted from the capo; with capo mode on, its capo
  is filled in.

Limits: notes too low or too high for the tuning move by an octave, and chord notes beyond the
number of strings are left out (quietest first); the library shows how many notes changed. The
fingering is chosen by the computer: it prefers small hand movements but can differ from how a
guitarist would play.

## Drum trainer and Drumless

**Drumless** (one click in the mixer) mutes the drums channel and every drum detail channel, also
when the drum strips are expanded. It switches the metronome on with a one-bar count-in and opens
the drum trainer. A second click brings back your previous drum, metronome and count-in settings.

The **drum trainer** shows five lanes, from top to bottom cymbals, hi-hat, toms, snare and kick.
The hits scroll towards the *now* line while the song plays; taller marks are harder hits. The
hits of the bar you hear are also listed as text for screen readers.

- **Split first**: the hits come from the drum detail stems. When the drums are not split, the
  trainer says so and **Split the drums** starts the split; the hits follow right after.
  Drumless works without a split.
- **Guide piece** (from Advanced): click a lane label, for example *Hi-hat*, to keep that piece
  audible on top of the drumless mix; click again to mute it.
- **Look-ahead** (from Advanced): 2 to 8 seconds (4 by default).
- **Drum notation**: drum sheet music of the song is shown next to the lanes and follows the
  playback; without it, **Generate drum notation** makes it.

Limits: fast fills, ghost notes and quiet hits can be missed, and bleed between the pieces can
add extra hits. With reduced motion the lanes page instead of scrolling.

## Singing feedback

**Singing feedback** compares your voice with the lead vocal while the song plays.

1. The first time, the **reference melody** is analysed from the lead vocal detail stem (or the
   vocals stem when vocals were not split). For a long analysis the time it takes is shown first.
2. Choose your microphone in the recording settings (chapter 11) and, for exact timing, calibrate
   the latency there.
3. Press **Start singing feedback** and play the song. The lead vocal is muted while you sing and
   comes back when you stop. Use **headphones**, so the microphone hears only you.

The **pitch view** shows the melody as bars and your voice as a line. After every lyric line a
score from 0 to 100 appears next to the karaoke banner and is announced to screen readers: the
share of the melody you sang within the tolerance. An octave lower or higher counts the same, and
a transposed song is scored in the new key. Without synced lyrics, sections or blocks of bars are
scored. At the end of the song or when you stop, a summary shows your average and your best line. A saved take can be
scored the same way with **Score this take**.

- **Tolerance**: how far from the melody a note may be, in cents (100 cents is a semitone):
  100 at Beginner, 50 at Advanced, 25 at Expert; from Advanced you can set 10 to 200.
- **Latency**: without a calibration an estimate of the browser is used, which can make good
  lines score lower; Bluetooth headphones can be off by tens of milliseconds.
- At Expert a raw readout shows the pitch you sing in hertz, as a note and its deviation in cents.

**Privacy**: everything you sing stays in your browser. Neither the microphone audio nor the
scores are sent to the StemStudio server.

Limits: bleed from other instruments in the vocal stem can add wrong notes to the reference;
lines with very little melody get no score; on a slow device the pitch tracking stops itself with
a message while the song keeps playing.

## Parts for strings, brass and woodwinds

Many instruments read music written in another key than it sounds: a trumpet or clarinet in
B-flat, an alto saxophone in E-flat, a horn in F. In the sheet music dialog (and in the view of
**Practise as** strings, brass or woodwinds) choose your instrument under **Part for your
instrument**: notes and key signature are transposed (concert C major becomes A major for an alto
saxophone) and the part is named after the instrument. *Concert pitch* keeps the sounding notes.

- The detail stems *strings*, *brass* and *woodwinds* suggest their own instruments; a section
  playing together is shown as its loudest line.
- **Clef** (from Advanced): for example the alto clef for a viola or the tenor clef for a
  trombone. Keys and piano always use two staves.
- **Range**: notes your instrument cannot play are kept, and the library says how many are out of
  range.
- The score view follows the playback the same way for a part as for the concert score.

Limit: the transposition is exact, but the notes come from an automatic transcription;
articulation, bowing and breath marks are not notated.

## MIDI of one instrument

Every instrument on the track page and in the mixer has a **MIDI** button. When sheet music of
that instrument exists, its MIDI file downloads at once; otherwise StemStudio makes the sheet
music first and shows the progress next to the button. The notes are always at concert pitch,
also for transposing instruments, and drums use the standard drum map on MIDI channel 10.
