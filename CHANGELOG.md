# Changelog

What changed for StemStudio users. Technical details are in `docs/STATUS.md` and the manuals in `docs/public/`.

## [Unreleased]

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

- The *Play along* practice presets now also mute the detail channels of the family, so an expanded drum group no
  longer stays audible.

### Fixed

- A detail-model download that is larger than expected is stopped instead of filling the disk.
