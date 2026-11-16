# StemStudio website: copy deck (consumer redesign)

Owner brief of 2026-11-16: the home page becomes a showcase for musicians ("what can I do with
this?"), developer content moves to a separate page. Owner decision: no approval round; this deck
is written first and the site is built from it straight away.

The English strings live in `site/src/i18n/en.json` (home page) so a Dutch version can be added as
`nl.json` later; this file is the readable version for review. Every claim is taken from the
current site, `README.md`, `docs/STATUS.md`, `docs/ROADMAP.md` or the manuals under
`docs/public/src`; the source is noted in brackets. Claims that could not be verified are listed at
the end and are **not** on the site.

## Home page (`index.html`)

### Header

- Brand: StemStudio (logo: one strand splitting into six stem colours)
- Navigation: How you'll use it / Features / Get started / For developers
- Beat indicator: four small lights, the first one accented (decorative, hidden from screen readers)
- Theme switch: "Switch to light theme" / "Switch to dark theme"

### 1. Hero

- **Take the song apart. Play your part.**
- StemStudio splits a song into its stems, the separate parts of the recording: vocals, drums,
  bass, guitar, piano and other. Mute your instrument and play along with the real band. [README intro]
- Primary button: **Get started** (to `#quick-start`)
- Secondary button: **See it in action** (to `#screens`)
- Trust line: Free and open source (MIT licence). Runs on your own computer. [README, manual 2]
- Animation label: One mixed waveform splits into six coloured stems: vocals, drums, bass,
  guitar, piano and other.
- Mini mixer heading: **Try the mixer**
- Mini mixer text: This is how StemStudio's mixer works. Press M to mute a part, S to hear only
  that part. No sound on this page: it shows the idea, the app plays the music.
- Live status (for screen readers too): "Playing: vocals, drums, bass, guitar, piano, other." /
  "Playing: … Muted: guitar." / "Solo: drums."

### 2. How you'll use it (`#use`)

- **How you'll use it**
- Four musicians, four practice sessions, one app.

**Guitar: Mute the guitar. Keep the band.**
1. Import the song and open the mixer.
2. Press M on the guitar strip. The band plays on without it, and you take the guitar part.
3. Stuck on the solo? Press Set A where it starts and Set B where it ends, then turn the loop on.
   The A-B loop repeats just that passage until your fingers know it. [manual 5]

**Drums: Lose the drummer. Keep the groove.**
1. One click on the preset Play along: drums mutes the drums and turns the click on. [manual 5]
2. The click follows the beat StemStudio found in the song, with an accent on the first beat of
   every bar. [README, manual 5]
3. Click slightly off? At the Advanced level you correct the BPM (beats per minute, the tempo),
   the offset and the beats per bar. [manual 3, 5]

**Vocals: Sing it like the record.**
1. The Karaoke preset mutes the vocals in one click. [manual 5]
2. Synced lyrics run in a banner under the mixer, and a ball bounces from word to word in time
   with the music. [README feature 006]
3. Want a guide voice? Keep the vocals and pull their fader down instead. [manual 5: fader 0-100 %]

**Teacher and student: Bring the song to the lesson.**
1. Turn a stem into a practice lead sheet: a PDF to print, plus MusicXML and MIDI for your
   notation software. [README feature 008]
2. Save a named mix for each lesson: every fader, mute, loop and click setting comes back
   exactly as you left it. [README mixes]
3. Export a stem or your own mix as FLAC, WAV or MP3 to practise with at home. [README feature 007]

### 3. Three steps (`#steps`)

- **Three steps to your first play-along**
1. **Import a song.** Paste a YouTube or Spotify link, or drop an MP3, WAV or FLAC file. [README]
2. **Split into stems.** StemStudio separates the song into six stems on your own computer, then
   splits drums, vocals and the rest further where the song allows it. [README, manual 5]
3. **Play along.** Open the mixer, mute your part and press Play. All stems stay perfectly in
   sync. [manual 5]

### 4. Feature showcase (`#screens`, alias `#features`)

- **Everything you need to learn a song**
- Five sides of the same app. The screens follow the theme you picked for this page.

**Hear every part** (covers README: Stem separation, Detail separation, Mixer)
- Benefit: Six stems, each in its own colour, each on its own channel strip.
- Every stem gets a fader, pan, mute, solo and a level meter. [README]
- Then StemStudio splits further where the song allows it: kick, snare, toms, hi-hat and
  cymbals; lead and backing vocals; strings, brass, keys and more. [README, manual 5]
- Honest note: Separation is never perfect. You will hear some bleed between stems, and guitar
  and piano quality varies per song. [manual 5, README limitations]
- Screen: Mixer with detail groups.

**Practice smarter** (covers: Metronome, Mixes)
- Benefit: Loop the hard bit, keep time, come back tomorrow where you left off.
- A click on the song's own beat, with its own volume. [README]
- Practice presets set up an exercise in one click: Play along: drums, Karaoke, Focus: vocals,
  Reset mix. [manual 5]
- Save, load, duplicate and rename named mixes: all channel, master, click and loop settings. [README]
- Screen: Expert mixer.

**Learn the song** (covers: Synced lyrics, Sheet music, Cover art and band info)
- Benefit: Read along, sing along, play from the page.
- Karaoke banner with synced lyrics and a bouncing ball. [README]
- A lead sheet per instrument: PDF, MusicXML and MIDI, for vocals, bass, guitar, keys, piano and
  drums. [README feature 008]
- Cover art and band facts from open data, right next to your song. [README feature 005]
- Honest note: Sheet music is a practice aid; it can miss or add notes. [README limitations]
- Screen: Track with cover and band info.

**Your library** (covers: Import, Library and exports)
- Benefit: Every song you practise, ready when you are.
- Import from a YouTube link, a Spotify track link or your own MP3, WAV or FLAC (up to 200 MB
  and 15 minutes). [README, manual 16]
- Save stems, stem packs and mixes as FLAC, WAV or MP3. [README]
- Screens: Import, Tracks, Library.

**Create** (covers: Style transfer, Songs from your own lyrics, AI composer agent)
- Benefit: Optional extras for when you want to play with the song itself.
- Restyle a song, or part of it, with one of 24 preset styles or your own. Needs an NVIDIA
  graphics card. [README feature 009]
- Write lyrics and generate a sung song; turn the vocal into your own voice, only after your
  explicit consent. Needs an NVIDIA graphics card. [README feature 010]
- Type a command in English or Dutch, such as "mute the drums and loop the chorus", and a local AI
  model turns it into steps and carries them out with StemStudio's own tools. Heavy and destructive steps ask
  you first. [README feature 011]
- Every generated take is labelled AI-generated. [README]
- Screen: the style library (cropped to the preset list).

### 5. Grows with you (`#levels`; covers README: Design system and journeys)

- **Starts simple. Grows with you.**
- Pick a level when you start, switch whenever you like. Your songs and mixes stay the same. [manual 3]
- **Beginner**: Guidance, hints and big practice presets. A rail shows the four steps: Import,
  Separate, Mix, Save. A short tour on every screen.
- **Advanced**: The full set of practice controls: pan, typed loop times, click corrections,
  search and sort.
- **Expert**: Numeric entry for volume and pan, level meters in dB, job details, keyboard hints
  and the shortcut overlay.
- Footnote: Light and dark theme, keyboard shortcuts and in-app help at every level. [README feature 004]

### 6. Privacy (`#privacy`, contains `#legal`)

- **Runs on your own computer.**
- No account. No cloud. Your songs, stems and mixes stay on your machine. [manual 2]
- No account or password: StemStudio is made for one person on one computer. [manual 2]
- Your audio is processed on your computer and never uploaded. [manual 16]
- Cover art, band info and lyrics are looked up with only the song's title, artist, album,
  duration and ISRC (a recording's ID code). Switch the lookups off whenever you like. [README, manual 16]
- The optional Claude backend for the agent sends text only, never audio, and only when you
  choose it. [manual 16]
- This website sets no cookies and loads no trackers. [current site]
- **Personal practice and privacy** (short notice): StemStudio is made for your own practice.
  Import only songs you may use, and respect copyright and the terms of YouTube and Spotify.
  StemStudio never bypasses copy protection: for a Spotify link it reads the track details and
  finds a matching public recording on YouTube. Covers, band texts and lyrics stay inside the app
  and never end up in exports or sheet music. [README notice]

### 7. Speed (`#gpu`; covers README: GPU acceleration)

- **Got an NVIDIA card? Split a song in minutes.**
- With a recent NVIDIA graphics card, StemStudio splits a 4-minute song, detail stems included,
  in a few minutes instead of half an hour. [manual 2]
- Race: CPU 31.5 min, NVIDIA GPU 3.7 min (numbers read from `docs/STATUS.md` at build time).
- Caption: Reference song of 4:22, standard and detail separation, measured on an RTX 5070 Ti. The
  stems are the same either way. [STATUS 003, current site]
- No NVIDIA card? StemStudio works on the CPU too. The six standard stems of a 4-minute song take
  several minutes, the detail split about half an hour. [manual 16]
- If the card is busy with a game or another AI program, StemStudio uses the CPU for that step and
  tells you why. You never lose a job. [manual 2]
- Button: **Race again**

### 8. Coming soon (`#soon`)

- **Coming soon**
- In development now. Details can still change before they arrive.
- **Practice tempo**: Slow a song down without changing its pitch, or move it to another key. With
  a speed trainer and a count-in. [ROADMAP 012]
- **Record yourself**: Record your part over the mix, keep your takes and listen back. [ROADMAP 015]
- **Tablet and remote**: A music-stand layout for your tablet and a remote control over your home
  network. [ROADMAP 018]

### 9. Get started (`#quick-start`, aliases `#start`, `#manuals`)

- **Get started**
- StemStudio runs in Docker on your own computer. Four steps, about ten minutes plus the first
  download. (TODO: "ten minutes" is not in the sources; left out of the site.)
- Final text: StemStudio runs in Docker on your own computer. Set it up once, then it is one
  command to start.
- **What you need**
  - A Windows, macOS or Linux computer with about 5 GB of free disk space, plus 50 to 150 MB for
    each song. [manual 2]
  - Chrome, Edge or Firefox. Safari works on a best-effort basis. [manual 2]
  - Optional: an NVIDIA graphics card for much faster splitting. [manual 2]
  - For detail stems, Docker needs at least 20 GB of memory (Docker Desktop: Settings, Resources). [manual 2]
1. **Install Docker.** Docker is an app that runs StemStudio's parts in separate, ready-made boxes
   called containers, so you don't have to install anything else. Use Docker Desktop on Windows or
   macOS, or Docker Engine with Compose on Linux. Link: Get Docker (docs.docker.com).
2. **Download StemStudio.** Download the code from GitHub as a ZIP file and unpack it. Link:
   Download the ZIP. (Or clone it with Git.)
3. **Start it.** Open a terminal in the StemStudio folder and run the command below. The first
   start builds everything and takes several minutes. [README quick start, manual 2]
   Command (from the README): `docker compose up --build`
   NVIDIA card: `docker compose -f docker-compose.yml -f docker-compose.gpu.yml up --build`
4. **Split your first song.** Open http://localhost:8080 and split your first song.
- Small print: The first split downloads the separation model; after that StemStudio works offline,
  except for imports from YouTube or Spotify. Stop with Ctrl+C; your songs stay. [manual 2]
- **Read the guide**: User Manual (PDF, pages and size from the build manifest).

### 10. Footer

- StemStudio on GitHub / MIT licence / Third-party licences / User Manual (PDF) / Technical Manual
  (PDF) / For developers
- Generated from commit `abc1234` of YYYY-MM-DD.

## Developer page (`developers.html`)

Keeps the technical tone of the old home page and loses nothing:

- **For developers**: what StemStudio is built on, how it fits together, how it is checked.
- Features in detail (all README feature bullets plus the delivered roadmap features, as before)
- How it works (architecture diagram), tech stack
- Quick start incl. the useful variants (README), running without Docker pointer
- GPU acceleration (README text plus the measured bars)
- Feature status (complete roadmap timeline, delivered with date / in development)
- Manuals (User Manual and Technical Manual tiles)
- Open source and licences ("What StemStudio is built on", model-weights notice)
- Design decisions (all ADRs)
- Personal practice and privacy (full README notice)

## What moves where

| Old home section (id) | New place |
|---|---|
| Hero (`#top`) | Home hero, new copy, canvas "split" + mini mixer |
| What it does, 15 cards (`#features`) | Home: 5 themes in `#screens` (alias `#features`) plus `#levels` and `#gpu`; full card list on `developers.html#features` |
| Screens (`#screens`) | Home feature showcase (one screen per theme) |
| How it works (`#how`) | `developers.html#how` (old link redirects) |
| Quick start (`#start`) | Home `#quick-start` (alias `#start`); README variants on `developers.html#run` |
| GPU acceleration (`#gpu`) | Home `#gpu` (race); README details on `developers.html#gpu` |
| Feature status (`#status`) | `developers.html#status` (old link redirects); home shows only "Coming soon" |
| Manuals (`#manuals`) | Home `#manuals` ("Read the guide"); both tiles on `developers.html#manuals`; PDF URLs unchanged (`manuals/*.pdf`) |
| Open source and licences (`#licences`) | `developers.html#licences` (old link redirects); footer links |
| Design decisions (`#decisions`) | `developers.html#decisions` (old link redirects) |
| Personal practice and privacy (`#legal`) | Home `#legal` (short, friendly) inside `#privacy`; full notice on `developers.html#legal` |

## Animation moments (one sentence each)

1. **The split (hero)**: one grey waveform scrolls in from the left and, at the split point, peels
   apart into six waveforms in the stem colours, each with its own rhythm, like the separation the
   app performs.
2. **Living mixer (hero)**: six channel strips with meters that pulse on the beat and faders that
   drift gently; a muted strip goes quiet and dims, a soloed one keeps playing alone.
3. **Interactive mini demo (hero)**: the visitor presses M or S on any strip and immediately sees
   which parts still play; a live text says the same for screen readers.
4. **Metronome pulse (whole page)**: four beat lights in the header, the first accented like the
   app's click, all page motion keeps this one tempo (96 BPM, 1.6 flashes per second).
5. **Scroll reveals**: sections and screenshots slide and fade in once, the scenario steps light up
   one after another, and the three steps flow Import, Split, Play with a connecting strand drawing
   between them.
6. **Screens (scroll-driven where supported)**: screenshots settle into place as they scroll into
   view.
7. **GPU race**: when the speed section comes into view, the CPU and GPU bars race at their real
   ratio (3.7 against 31.5 minutes, sped up), and "Race again" restarts it.
8. **Micro-interactions**: buttons and scenario panels lift slightly on hover with a glow in their
   stem colour; switching light and dark cross-fades the page.
9. **Reduced motion**: every moment above shows its end state, nothing loops, and the mini mixer
   still answers clicks without movement.

## Claims left out (could not be verified in the sources)

- "No tracking" for the app itself: the sources say no accounts, local processing and no cloud
  upload, and the site sets no cookies or trackers, but no source states the app has no telemetry
  in those words. The site says "no cloud" and "this website ... loads no trackers" instead.
- "Download StemStudio" as an installer: there is no installer; the button says "Get started" and
  the steps explain Docker.
- "Docker Desktop is free": not in the sources; Docker is described without a price.
- Any time for the whole setup ("ten minutes"): not measured anywhere.
- The download size of the first separation model: the README says about 50 MB, the user manual
  "a few hundred MB"; the site gives no number.
- Exact ranges of the coming-soon features (50-120 %, +-12 semitones): specified, not built;
  left out so nothing is promised before release.
- Dates or order of the coming-soon features.
