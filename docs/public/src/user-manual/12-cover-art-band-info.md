# Cover art and band info

Every track gets a picture, and you can read about the artist without leaving StemStudio.

## Where the picture appears

- A large cover on the track page and in the mixer header.
- A small thumbnail on each row of the track list.

StemStudio picks the first picture that is available, in this order:

1. A picture you uploaded yourself.
2. The Spotify album image.
3. The cover embedded in an uploaded MP3 or FLAC file (WAV files have none).
4. The front cover of the matching album from the Cover Art Archive.
5. The YouTube video thumbnail.
6. A placeholder with the song's initials on a colour derived from the title.

Every picture is cropped to a square. It appears as soon as the download is finished (a
short **Saving cover** stage). When the band lookup later finds an album cover, it replaces a
YouTube thumbnail everywhere; it never replaces a Spotify, embedded or own picture.

## Band info

Press **Band info** on the track page or in the mixer. While StemStudio searches, the panel
shows *"Searching for band information..."*. When the artist is found it shows:

- At every level: **Name**, **Type** (Group, Person, Orchestra, Choir, Character or Other),
  **Active** years, **Album** with its year, and a short excerpt from Wikipedia.
- At Advanced and Expert also: **Country**, **Genres**, **Members** (groups only, up to 12),
  **Website** and the data sources (*MusicBrainz (CC0), Wikidata (CC0), Wikipedia (CC BY-SA
  4.0)*, plus *Cover Art Archive* when the picture came from there).

The Wikipedia excerpt always carries its attribution: *From the Wikipedia article* (link)
*by Wikipedia contributors* (link to the article history), *licensed under CC BY-SA 4.0*
(link), *Excerpt, unmodified.* The excerpt language follows the order the administrator set
(English by default).

Artist information is stored once per artist and refreshed after 30 days. When one source
cannot be reached, the panel still shows what was found, with a short note such as
*"Wikipedia could not be reached."*

## When nothing or the wrong band is found

StemStudio only shows a band when the title, artist and duration match; it never guesses.
If there is no confident match the panel says *"No band information found. Check the title
and artist, then search again."* If the services could not be reached it says *"Band
information could not be retrieved; try again later."* In both cases you can:

- Press **Search again** to repeat the lookup.
- Press **Correct title and artist**, fix the **Title** and **Artist** (up to 500 characters
  each) and press **Search again**. The corrected names are used for the search only.

## Your own picture (Advanced)

Press **Choose picture** on the track page and pick a JPEG, PNG or WebP file of up to 10 MB.
The upload starts at once (*"Uploading picture..."*) and the picture is cropped to a square.
**Use automatic picture** goes back to the automatic choice. An invalid file shows the reason
(for example *"Only JPEG, PNG and WebP pictures are supported."*) and keeps the current
picture.

## Privacy

Only the song title, artist, duration and ISRC code are sent to MusicBrainz; the Cover Art
Archive, Wikidata and Wikipedia receive only the identifiers found there. No personal data is
sent. Pictures and band texts are copyrighted: they are shown inside StemStudio only and never
exported.

Online lookups can be switched off by the administrator. The panel then says *"Online lookups
are switched off."* with a link **How to switch online lookups on**; StemStudio fetches no
pictures from these services and no band information, and everything else works as before.
