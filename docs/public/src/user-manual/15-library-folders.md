# Library by artist and album

Everything you save to the library (chapter 14) is filed automatically in folders:
**Artist > Album > Song**, for example *Metallica > Master of Puppets > Battery*. You do not
create, move or name folders yourself. Open **Library** in the navigation to browse them, or
type a word in the search box to find an artist, an album, a song or a stem such as "snare".

The folders exist inside StemStudio only. Nothing is written to folders on your disk, and
nothing about them leaves your computer.

## Browsing

The library opens at the artist level. Each level has its own address, so the **Back** button
of the browser and a reload bring you to the same place. The path at the top (*Library >
Artist > Album*) takes you back one or more levels; on a narrow screen it is shortened.

| Folder | Shows |
|---|---|
| Artist | Picture, name, number of albums and items, total size |
| Album | Picture, name, year when known, number of songs and items, total size |
| Song | Picture, name, number of items, total size |

Press **Grid** or **List** to change the layout. The choice is remembered on this device (in
this browser) and applies to every level.

Artists are sorted A to Z (a leading "The" is ignored), albums by year and songs A to Z.
**Unknown artist** and **My songs** come last. Beginners see this fixed order; at the Advanced
level you can change it (see *Levels* below).

Each tile is one link. A screen reader announces it as the name, the level and the counts, for
example "Master of Puppets, album, 1986, 8 songs, 21 items".

## Inside a song folder

A song folder lists its items, with the actions of chapter 14: **Play**, **Download**,
**Rename**, **Delete** (after you confirm) and **Retry** for a failed item. Status changes show
without reloading the page. The items come in the order of the mixer: the six standard stems,
the detail stems, mixes, sheet music and generated takes, then newest first.

While the track still exists the folder also offers **Open the track** and **Open in the
mixer**. When you delete a track, its items stay in the library and keep their place.

When you delete the last item of a folder, the folder disappears; there are no empty folders.

## Where the names come from

For each level StemStudio takes the first name it has, in this order:

1. A name you set yourself for the item (see *Correcting names* below).
2. A name you set for the track.
3. The artist, album and title found by the band info lookup (chapter 12), when that lookup
   found a match.
4. The names reported by the import (YouTube, Spotify or the file tags).
5. A fallback: *Unknown artist*, *Unknown album* and the title of the track.

Names that differ only in capitals, accents, spaces, quotes, dashes or a leading "The" share
one folder, so *Motörhead* and *MOTORHEAD* are the same artist. "feat." parts in an artist
name are left out when filing; "Various Artists" is ignored. A song made from your own lyrics
(chapter 18) is filed under **My songs > Songs from my lyrics**.

The name shown on a folder is the spelling from the band info when one of its items was filed
from a match, otherwise the spelling most of its items use. Names that StemStudio chose
follow along when the band info of a track is found or changes later; a name you typed is
never changed automatically.

## Sorting your library (n left)

The first time you start StemStudio after this feature was added, and after you restore an
older backup (chapter 21), StemStudio files all existing items in the background. Until that is
done the library shows **Sorting your library (n items left)** and updates by itself. You can
keep working; a library of 5,000 items is normally ready within seconds. New items are filed in
the same moment they are saved, so they never wait.

## Folder pictures

- A **song** folder shows the picture of its track.
- An **album** folder shows the best picture among its songs. A picture you chose for one of
  its tracks wins.
- An **artist** folder shows up to four album covers as a mosaic, or the one cover when it has
  one album.
- A folder without any picture shows its first letter on a colour (a question mark for
  unknown folders).

To change a picture, open the track and choose another one (chapter 12); the folders follow.
StemStudio has no artist photos. Pictures are shown in the app only: they are never part of a
download, an export or a backup.

## Searching the library

The search box (**Search the library**) is on every level. Results appear while you type,
shortly after the last key; **Enter** searches at once.

- Press <kbd>/</kbd> on a library page to jump to the box (not while you type in a field).
- Press <kbd>Arrow Down</kbd> in the box to move to the first result.
- Press <kbd>Esc</kbd> or the clear button to go back to the folder you were in; focus returns
  to the box. The text is kept in the address, so Back and reload restore the search.

Results are grouped as **Artists**, **Albums**, **Songs** and **Items**, each with its count and
at most 20 entries; **Show all** lists the rest. Every result shows its path as links, and the
matched parts are underlined and bold (so you do not depend on colour). The item actions work
in the results. The number of results is announced politely once you pause typing.

### What the search looks at

- The artist, album and song names, and the name of each item.
- Stems and instruments: *vocals*, *drums*, *bass*, *guitar*, *piano* and *other*, the detail
  stems such as *snare*, *hi-hat*, *lead vocals*, *strings* or *acoustic guitar*, and everyday
  words such as *voice* or *keyboard*. Every instrument family is found the same way.
- Mixes are found by the stems they contain (for example *What I hear*); sheet music by its
  instrument profile.

Capitals and accents do not matter. With several words, every word must be found somewhere in
the same result: *metallica bass* finds the bass items of Metallica. Use at most eight words
and at most 100 characters. A family name such as "bass guitar" or "drum kit" counts only as a
whole phrase, so *guitar* alone never finds bass.

Without matches StemStudio says so, lists what the search looks at and offers **Clear
search**. If a search fails, the reason is shown and the previous results stay.

## Correcting names: Library info

A wrong or missing name files items in the wrong place. Fix it with **Library info**, at every
level:

- **Edit library info** on a song folder changes the names for all items of that track.
- **Library info** on an item row changes only that item.

The dialog has a field for the artist, the album and the song, each with an **Automatic** box.
The label next to a box shows the automatic name and where it comes from (*from band info*,
*from the import* or *fallback*). Type your own name (1 to 200 characters, no control
characters) or tick **Automatic**. **Reset** makes all three automatic again; **Cancel** or
<kbd>Esc</kbd> closes the dialog without saving.

After you save, the items move at once and the library follows them to their new folder. If
the artist or title of the track itself is wrong, use **Look up band info again** on the track
page (a link in the dialog); the automatic names then follow.

## Levels

| Part | Beginner | Advanced | Expert |
|---|---|---|---|
| Folders, pictures, path, Grid and List | yes | yes | yes |
| Search, the <kbd>/</kbd> shortcut | yes | yes | yes |
| Item actions and **Library info** | yes | yes | yes |
| **Order** (name, year, recently added, size, number of items) | fixed order | yes | yes |
| **Only in this folder** (search) | no | yes | yes |
| **All items** (flat list with filters) | no | yes | yes |

**Order** offers *Name A-Z*, *Recently added*, *Size* and *Number of items* for the folders of
the level you are in, and for albums also *Year* (the default). The choice is remembered per
level on this device.

**All items** is the flat list of chapter 14 with its filters (**All tracks**, **All kinds**,
**Search items**); every row now shows its path. **Back to the folders** returns to the artist
level. Links from before this feature (a confirmation that opened a single item) open this list
with that item highlighted.

## After saving

The confirmation of an export, a stem pack or a sheet music score names the folder it was filed
in, for example *Saved to Library > Metallica > Master of Puppets > Battery*, with a link to
each level and **Open in library**, which opens the song folder at that item.

## Backups and the agent

Filing and the names you set live in the database, so a backup (chapter 21) keeps them; folder
pictures need nothing extra. The AI composer agent (chapter 20) can browse a folder, search
the library, open a folder for you and change library info. Folder and item names count as
text from outside, so after the agent has read them it asks before changing anything.
