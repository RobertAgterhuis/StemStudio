# Changelog

What changed for StemStudio users. Technical details are in `docs/STATUS.md` and the manuals in `docs/public/`.

## [Unreleased]

### Added (feature 020, insights and handouts)

- **Stem quality**: after every six-stem separation and detail split, the track page rates every stem (Good, Fair,
  Poor, Probably not in this song, Not rated) with a short reason; the mixer shows the rating as a badge per strip.
  From Advanced you see the bleed (with its main source) and the artefacts behind a rating, at Expert the index values
  and the analyzer version. The ratings are estimates and labelled a *rough indication* for now.
- **Quality tips**: up to two tips per stem, with a button where it helps: **Split further**, **Open detail stem** or
  **Mute** (opens the mixer with the stem muted, nothing saved). There is no GPU tip: the GPU gives the same stems.
- **Lesson handouts**: one click on the track page makes a PDF for Band overview, Vocals, Guitar, Bass, Keys, Drums or
  Melodic instruments (C, B-flat, E-flat or F) with the song facts, the form chart with bars, chords over the lyrics,
  the chord chart, the family's sheet music or tablature and your practice notes. Missing parts are listed with a link
  to make them. From Advanced, **Customise...** chooses the parts, the sheet music, the transposition and the paper.
- **Setlist handouts**: one lesson PDF for a whole set (up to 50 songs) and one family, with a cover, the running order
  and contents. The 014 **PDF for the stand** stays the quick print of a set.
- **Handouts in the library**: a new kind *Handouts* with preview, download, **Make again** and **Retry**; notes about
  sheet music that was left out or could not be read.
- **Practice notes**: one note per family per song on the track page, printed in that family's handout.

### Known limits (feature 020)

- Every quality rating is a rough indication until it is validated on real multitrack music. Artefacts are estimated
  from dropouts only, and instruments that hold notes together on the same beats can read as bleed. Tracks separated
  into 4 stems get no report.
- At most 3 handouts of one song or setlist are made at a time; a handout holds at most 600 pages of sheet music.

### Added (feature 016, instrument views)

- **Practise as ...**: choose the instrument you play in the mixer; StemStudio mutes it in the song (including its
  detail channels, also when they are collapsed) and opens the view made for it. The choice is offered again next
  time, but never applied without a click.
- **Chords**: chords are found automatically once a song is separated, shown in a chords lane in the mixer, above the
  karaoke words and as a chord sheet on the track page. Chords follow the practice transpose and are spelled for the
  key. From Advanced you can correct a chord, and download the sheet as ChordPro.
- **Song key**: the key stands next to the tempo, with key changes, the key you hear after a transpose and the written
  key of your own part.
- **Capo mode** for guitar: up to three suggested capo positions, the chord shapes to play with the capo, and a capo
  badge.
- **Tablature** for guitar and bass sheet music: notation and tab or tab only, tunings such as drop D, DADGAD and 5- or
  6-string bass, and a capo.
- **Drum trainer and Drumless**: one click mutes all drums and starts the metronome with a count-in; five hit lanes
  show what the drummer plays, with a guide piece you can keep audible and the drum notation next to it.
- **Singing feedback**: a pitch view of the melody and your voice, a score per lyric line and a summary at the end.
  Everything you sing stays in your browser.
- **Parts for your instrument**: sheet music written for B-flat, E-flat and F instruments, other clefs, and the number
  of notes out of range; new strings, brass and woodwinds profiles.
- **MIDI per channel**: download the notes of a single channel, for every instrument family.

### Security (refactors 003 and 004)

- **Only this computer by default**: Docker publishes the web app (8080) and the API (8081) on `127.0.0.1`, so other
  devices in the network can no longer reach them. `STEMSTUDIO_BIND_ADDRESS=0.0.0.0` opens them on purpose (with the
  T7 warning in `.env.example` and the manuals).
- **API host allow-list**: the API answers `400` to a host name other than `localhost`, `127.0.0.1`, `[::1]` and `api`
  (DNS rebinding). `STEMSTUDIO_API_ALLOWED_HOSTS` (`StemStudio__AllowedHosts`) adds names, for example the LAN address
  when the ports are opened.
- **Writes need the app's Origin or a local caller**: a request that changes data without an `Origin` header is
  accepted only from this computer (loopback) or the web container; a script on the host now sends
  `Origin: http://localhost:8080`. Forwarded headers are never trusted.
- The API warns at start-up when it listens beyond loopback outside a container, and worker, api and web drop all
  Linux capabilities and run with `no-new-privileges`.

### Changed

- Development: `scripts/test-all.{sh,ps1} --affected` and the pull-request CI run only the checks
  a change needs (refactor proposal 001); integration, pre-merge and `main` stay full.
- The *Play along* practice presets now also mute the detail channels of the family, so an expanded drum group no
  longer stays audible.

### Fixed

- A detail-model download that is larger than expected is stopped instead of filling the disk.
