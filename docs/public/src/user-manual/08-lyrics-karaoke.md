# Synced lyrics and the karaoke ball

Sing along with time-synced lyrics in a banner at the bottom of the mixer.

## The karaoke banner

- The current line is large and centred, the next line smaller below it and the previous
  line faded above.
- Sung words change colour and a **ball** bounces onto each word exactly when it should be
  sung. The ball follows the real playback position, also when you pause, seek or loop.
- Before the first line, and in instrumental breaks longer than five seconds, three
  countdown dots appear (one per second) and the banner says *instrumental*.
- The credit on the banner shows where the lyrics come from: *Lyrics: LRCLIB* or *Lyrics:
  entered by you*, with a note when the word timing was estimated or aligned
  automatically.
- With the system setting *reduce motion* switched on, the ball does not jump; the current
  word is highlighted instead.

Instrumental songs have no banner; the track page says *"Instrumental - no lyrics"*.

## Where the lyrics come from

During import StemStudio looks up synced lyrics in the free LRCLIB database, using the
song's title, artist, album and duration. The duration must match within two seconds. When
online lookups are switched off, no lyrics are looked up; you can still paste your own.

When only plain (unsynced) lyrics are found, or when you paste your own text, StemStudio can
**align** them to the vocal stem with a speech model:

- With a GPU this happens automatically after the stems are ready (on the reference
  machine the alignment itself takes a few seconds; loading the model about 20 seconds).
- On a CPU you press **Align lyrics**; the button shows how long it will take. You can
  cancel it.
- The first alignment downloads the speech model (1.6 GB).

## Your controls

| Control | Level |
|---|---|
| Show or hide the banner (remembered) | All levels |
| Text size S, M or L (remembered) | Advanced |
| Offset from −5 to +5 seconds, ±50 ms per key press, and reset | Advanced |
| Search again, paste lyrics (up to 20,000 characters), remove lyrics | Advanced |
| Timing origin, alignment controls, low-confidence words | Expert |

Use the **offset** when the words come consistently too early or too late.

## Messages you may see

- *"Lyrics could not be retrieved; try again later."* with **Search again**.
- *"The vocal stem is silent, so the lyrics cannot be aligned."*
- *"The speech model could not be downloaded; try again when online."*
- *"The lyrics could not be aligned to the vocals."* with **Try again**.

## Copyright

Lyrics are copyrighted. StemStudio shows them inside the app only and stores them locally;
they are never exported, mixed into audio files, logged or shared.
