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

### Changed

- Development: `scripts/test-all.{sh,ps1} --affected` and the pull-request CI run only the checks
  a change needs (refactor proposal 001); integration, pre-merge and `main` stay full.
- The *Play along* practice presets now also mute the detail channels of the family, so an expanded drum group no
  longer stays audible.

### Fixed

- A detail-model download that is larger than expected is stopped instead of filling the disk.
