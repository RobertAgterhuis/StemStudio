# Stem quality and lesson handouts

Two things help you and your teacher or band after a song is separated: a **quality report**
that says how clean each stem is, and **lesson handouts**: PDFs per instrument family with
everything you need for a lesson. Every family counts the same: vocals, guitar, bass, keys,
drums and the melodic instruments each get their own handout.

## Stem quality

The **Stem quality** card on the track page rates every stem and every detail stem. Each rating
has a word and an icon, never just a colour:

| Rating | Meaning |
|---|---|
| **Good** | Clean enough to practise with |
| **Fair** | Usable, but some sound of other instruments or some damaged sound is in it |
| **Poor** | Much of what you hear belongs to other instruments, or the sound is damaged |
| **Probably not in this song** | The instrument does not seem to play; what you hear is spill from other instruments |
| **Not rated** | Too little audio in the stem (less than about 10 seconds of sound) |

Next to the rating is one short reason, for example *"Some sound of drums is in this stem."* In
the mixer every channel strip shows the rating as a small badge; select it to come back to the
card.

**When a report is made.** Automatically after every six-stem separation and every detail split;
for older tracks press **Check stem quality**. **Check again** makes a new report. When the new
rating differs, the old one stays visible as *"was Fair"*. A report made with an older version of
StemStudio is marked *"Made with an older version"* and is only remade when you ask. Mixing,
exports and imports never wait for a report. A track separated into 4 stems gets no report: the
check needs the six stems (vocals, drums, bass, guitar, piano and other).

### How honest the ratings are

The ratings are **estimates**. StemStudio has no copy of the separate instruments the song was
recorded with, so it cannot measure the truth; it looks for signs in the stems instead.

- Every rating is labelled a **rough indication** for now. The measures were checked on
  synthetic test songs, where known amounts of bleed and damage were mixed in on purpose; the
  check on real multitrack music has not run yet. Guitar, piano, detail and rest stems stay rough
  indications even after that check.
- **Artefacts are estimated from dropouts only**: short holes where a stem suddenly falls silent
  and comes back. A watery, warbling sound is not measured yet, so a stem can sound watery and
  still show few artefacts.
- **Instruments that play together can look like bleed.** Kick and bass hitting at the same
  moments are recognised and capped at *Fair* (*"May be bleed of bass, or the two instruments
  playing together."*). Instruments that hold notes together on the same beats for a whole song
  can still read as bleed, so the rating is then too strict. Listen to the stem before you trust a
  *Poor*.

### Tips

Each stem shows at most two tips, the most useful first:

| Tip | Button |
|---|---|
| Detail separation may pull apart the instruments mixed into this stem | **Split further** starts the detail separation of this stem |
| Use a detail stem instead, or listen to the "rest" stem too | **Open detail stem** opens the mixer with this stem's detail stems expanded |
| The instrument is probably not in this song | **Mute** opens the mixer with this stem muted; nothing is saved until you save the mix |
| Hard to isolate (for example guitar and piano) | none: the stem is close to the best StemStudio can do |
| Clean enough to practise with | none |

There is no "run it on the GPU" tip: the graphics card runs the same models as the processor and
gives the same stems, only faster.

### Levels

| Level | What you see |
|---|---|
| Beginner | The rating, the reason and the tips |
| Advanced | Also the two measures behind the rating: **Bleed** (Low, Medium, High, with the main source, for example *"Bleed: medium, mostly drums"*) and **Artefacts**, with their reliability label |
| Expert | Also the index values (0 to 1), the analyzer version and when the report was made |

## Lesson handouts

Open **Lesson handout** on the track page and pick a family. One click makes a PDF; a status line
under the menu shows the progress and **Open in library** takes you to the finished item.

| Family | What it contains besides the song facts, the form chart and your practice notes |
|---|---|
| **Band overview** | Lyrics with the chords above the words, chord chart per section; no sheet music |
| **Vocals** | Lyrics with chords, vocal sheet music |
| **Guitar** | Lyrics with chords, chord chart, guitar tablature and sheet music |
| **Bass** | Chord chart, bass tablature and sheet music; lyrics with chords optional |
| **Keys** | Lyrics with chords, chord chart, piano or keys sheet music |
| **Drums** | Drum notation and the first lyric line of each section as cues; no chords |
| **Melodic instruments** | Chord chart and the part for your instrument in C, B-flat, E-flat or F |

The **song facts** are tempo, time signature, key, length and the cover picture. The **form
chart** lists the sections (chapter 7) with their bar numbers, time, chords per bar and, when you
practised them, the tempo you reached. A part that does not exist yet never stops a handout: it
is left out and listed (*"Not included: tablature (not available yet)"*), with a link to make it,
such as **Make tablature** or **Detect chords**.

For melodic instruments the chords and key are transposed with the part: B-flat for trumpet,
clarinet, tenor and soprano sax; E-flat for alto and baritone sax; F for horn; C for flute, violin
and other concert-pitch instruments. The facts then say for example *"Part in B-flat (sounds a
major second lower)"*. Make the transposed part in the sheet music dialog first (chapter 16).

### Customise (Advanced and Expert)

**Customise...** in the menu opens **Customise the handout**: choose the family, tick the
**Parts** (also optional ones such as the practice progress from the practice log), pick the
**Sheet music** items, the **Transposition** for melodic instruments and the **Paper** (A4 or
Letter). The dialog shows which parts are missing and roughly how long the handout takes. Press
**Make handout**.

### Handouts in the library

A handout is a library item of the kind *Handouts* (chapter 14): preview it, download it, rename
or delete it like any other item. Its row shows for example *"Bass · 4 pages · A4"*.

- **Make again** makes it anew with the song's current lyrics, chords and notes and the same
  choices.
- A handout that failed shows **Retry**.
- At most **3 handouts of one song or setlist** are made at a time. A fourth answers *"Up to 3
  handouts of a song or setlist are made at a time; wait until one is ready."*
- From Advanced the item lists notes about the PDF:
  - *"A notation file could not be read: ..."*: that sheet music file is damaged or unusual; its
    pages are replaced by a note.
  - *"A notation file was left out because the handout reached its size limit: ..."*: a handout
    holds at most 600 pages of sheet music (and 128 MiB of sheet music files); further items
    become a note.
  - *"The cover picture was left out because it is not a JPEG or is larger than 40 megapixels:
    ..."*: the rest of the handout is complete.
  - Sheet music scaled to fit the page and characters the fonts cannot print are listed too.

A handout that is far too large to make (for example a long setlist with every part and very
long lyrics) is refused before it starts, with one of these messages:

- *"The handout is too large to send to the processing service (over 8 MiB). Leave out lyrics or
  songs."*
- *"The handout has more than 500,000 characters of text. Leave out lyrics or songs."*
- *"The handout has more than 30,000 sections, bars, chords and lyric lines. Leave out
  ingredients or songs."*
- *"The sheet music of the handout is larger than 128 MiB together. Choose fewer sheet music
  items."*

*"The processing service could not lay out this handout. Try again, or leave out an
ingredient."* means the worker refused the handout for another reason. *"The handout could not be written"* means making it failed or took longer than four
minutes; press **Retry**.

### Setlist handouts

On a setlist, **Setlist handout** makes one lesson PDF for the whole set and one family: a cover
with the setlist name and note, the running order, a contents page, and each song on new pages
with its family layout. It is kept in the library like a song handout. A setlist can hold up to
50 songs in a handout; for a longer one the dialog says *"A setlist handout can hold up to 50
songs."* An empty setlist has no handout menu.

The other button, **PDF for the stand** (chapter 9), stays the quick print of the set for the
music stand: an overview and one page per song with sections and lyrics, not stored. Use the
setlist handout for a lesson or a band rehearsal, the PDF for the stand on stage.

### Copyright notice

Handouts are for your own practice and your lessons. Every page footer says *"For personal
practice and teaching only"*; the notice is fixed and cannot be removed. The last page has the
full notice and the credits of the sources and models used. Chords, sheet music, tablature and
timings were made automatically and may contain errors; check them before a lesson. The text of
a handout is real, selectable text, but sheet music pages are pictures, so handouts are not fully
accessible to screen readers.

Handouts are made on your own computer, without any network access.

## Practice notes

The **Practice notes** card on the track page keeps one note per instrument family for the song,
for example what to practise on the bass this week. Choose the family, write the note (up to
4,000 characters) and press **Save note**. The note is printed in that family's handouts. Saving
an empty note removes it.

## During a backup

Checking stem quality, making a handout and saving a practice note all keep working while a
backup runs (chapter 22): they add new items and never delete or replace library files. Only
while a restore switches the library do they wait, like everything else.
