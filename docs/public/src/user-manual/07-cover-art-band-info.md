# Cover art and band info

<span class="planned">In development</span> Feature 005. This chapter describes the
specified behaviour.

Every track gets a picture, and you can read about the artist without leaving StemStudio.

## Where the picture appears

- A large cover in the mixer header and on the track page.
- A small thumbnail on each row of the track list.

StemStudio picks the first picture that is available, in this order:

1. A picture you uploaded yourself.
2. The Spotify album image.
3. The cover embedded in an uploaded MP3 or FLAC file (WAV files have none).
4. The front cover of the matching album from the Cover Art Archive.
5. The YouTube video thumbnail, cropped to a square.
6. A placeholder with the song's initials on a colour derived from the title.

The picture appears as soon as the download is finished (a short **Saving cover** stage).
When a better album cover is found later, it replaces a YouTube thumbnail everywhere.

## Band info

Press **Band info** in the mixer or on the track page. The panel shows:

- Artist name and type (band or person), country, active years and genres.
- Members (bands only, up to 12) and the official website.
- Album and year.
- A short excerpt from Wikipedia with its attribution (article link, *Wikipedia
  contributors*, licence *CC BY-SA 4.0*, *Excerpt, unmodified*).
- The data sources: MusicBrainz and Wikidata, plus the Cover Art Archive when the picture
  came from there.

Band info is fetched once per artist and refreshed after 30 days. Beginners see the picture
and a short summary; Advanced and Expert users see all facts and sources.

## When the wrong band is found

If StemStudio finds no confident match, the panel says *"No band information found"* and
offers **Correct title and artist**. Fix the spelling and search again. After three failed
attempts you see *"Band information could not be retrieved; try again later."* with a retry
button.

## Your own picture (Advanced)

Upload a JPEG, PNG or WebP picture of up to 10 MB. It is cropped to a square. **Use
automatic picture** goes back to the automatic choice. An invalid file shows an error and
keeps the current picture.

## Privacy

Only the title, artist, album, duration and ISRC code are sent to MusicBrainz, the Cover Art
Archive, Wikidata and Wikipedia. No personal data is sent. Covers and texts are copyrighted:
they are shown inside StemStudio only and never exported. Online lookups can be switched off
by the administrator; StemStudio then works completely offline and the panel explains how
to switch them back on.
