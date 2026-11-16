# Feature Specification: Library by Artist and Album

**Feature Branch**: `022-library-folders`

**Created**: 2026-11-16

**Status**: Draft (design only; no product code in this round)

**Input**: Owner request of 2026-11-16 (Dutch, translated): "When I save a stem of an imported
track, the library should automatically create - if not present yet - a folder with the
artist/band name, inside it a folder with the album the song is on, inside that a folder with the
song name, and store the stem there. Folder art is a must. It must be possible to search the
library, where the library filters automatically as characters are typed."

Owner clarifications (2026-11-16): the folders exist **in the app only** (a virtual hierarchy in
the StemStudio library; files stay in the app's internal storage, nothing is written to folders on
the user's disk); the live search matches **artist/band, album, song and stem/instrument** (all
four). MoSCoW: **Should** (owner request, `docs/MoSCoW.md` S13).

Builds on: 005 (matched artist, album, year, cover precedence, covers shown in-app only), 007 (the
asset library: save to library, play, download, rename, delete, filter), 001/002 (stem and detail
stem names such as `drums.snare`), 004 (design system, journey levels, help, a11y), 008/009/010
(scores and generated takes in the same library), 011 (agent tools). Overlaps with 013 (backup,
storage), 014 (setlists, shortcuts) and 015 (recorded takes) are resolved in
[research.md](research.md) R-11.

## Clarifications

### Session 2026-11-16

Answered autonomously by the architect (operating mode: autonomous; only genuine product
decisions go to the owner). Binding design: [ADR-036](../../docs/decisions/ADR-036-library-by-artist-and-album.md).

- Q: Which items are filed into folders? -> A: **every item of the library** (`/library`), not only
  single stems: stems and detail stems, "What I hear" and "Selected channels" mixes, stem packs,
  range exports, sheet music (008) and generated takes saved to the library (009/010). Recorded
  play-along takes (015) stay with their track and are not shown, as in the 007 list today.
- Q: Where do the names come from? -> A: per level, the first available of: a name the user set for
  this item; a name the user set for its track; the band info match (005: matched artist, album
  and recording title); the names the import reported (the cleaned YouTube/Spotify/file names that
  005 already uses for the lookup); "Unknown artist" / "Unknown album". The song name falls back to
  the track title.
- Q: Are folders stored? -> A: **No. Folders are derived** from the items: a folder exists exactly
  while at least one item is filed in it, so there are never empty folders and nothing to clean up.
  Each item stores where it is filed (so it keeps its place when its track is deleted).
- Q: "Metallica" and "metallica"? -> A: one folder. Names are compared ignoring case, accents,
  repeated spaces, curly versus straight quotes and dashes, and a leading "The " ("The Beatles" =
  "Beatles"). The folder shows the band info spelling when there is one, otherwise the spelling
  most items use.
- Q: Featuring and several artists? -> A: the folder is the main artist: "feat."/"ft."/"featuring"
  parts are left out of the folder name (the full credit stays searchable). Names joined with "&",
  "and", "," or "x" stay one artist ("Simon & Garfunkel"), because band names contain them; the user
  can change it.
- Q: Compilations? -> A: filed under the song's own artist, never under "Various Artists"; the
  album folder is the compilation's title, so the compilation appears in each of its artists.
- Q: The same song on several albums? -> A: each import is filed under the album it was matched to.
  Two imports of the same song on the same album share one song folder; each item still shows its
  source.
- Q: Songs that are not on an album, or with no match? -> A: "Unknown album" inside the artist;
  "Unknown artist" when the artist is unknown. Both are sorted last. Songs made from your own lyrics
  without a source song (010) are filed under "My songs" > "Songs from my lyrics" > the song title.
- Q: Can the user fix a wrong folder? -> A: yes (P2): "Edit library info" sets artist, album and song
  name for a track (all its items move) or for one item. Each name can be set back to automatic.
- Q: What happens when band info is found later? -> A: items whose names were automatic move to the
  new folders on their own; names the user set are never changed by a lookup.
- Q: Folder art? -> A: a song folder shows its track's picture (005 precedence); an album folder
  shows the album cover (the best picture among its songs, 005 precedence); an artist folder shows
  its album covers as a mosaic (one cover when it has one album). StemStudio has no artist photos
  (005 left them out for licence reasons), so there is no band picture; folders without any
  picture show a lettered placeholder. Pictures are shown in the app only (005 FR-014).
- Q: Live search: what and how fast? -> A: it starts with the first character and updates while
  typing (short pause of 200 ms), ignores case and accents, and finds artists, albums, songs and
  items by artist, album, song, stem or instrument (including detail stems such as "snare" or
  "hi-hat" and the name of the item). Matches are highlighted and every result shows its path.
  Clearing the search returns to the folder you were in. With 5,000 items, results appear within
  0.4 s of the last key press.
- Q: Does the old flat list disappear? -> A: no; it stays as "All items" (Advanced) with its filters.
- Q: Beginner path? -> A: Beginner sees folders with art, the breadcrumb, the search box and the
  item actions (play, download, rename, delete) and "Edit library info" (owner decision
  2026-11-16: available at every level). Sorting, the flat list and "only in this folder" are
  Advanced.
- Q: Should the track list (`/tracks`) be grouped too, and can a whole folder be downloaded? -> A:
  no (owner, 2026-11-16): `/tracks` stays ungrouped and there is no folder archive download; both
  are out of scope.
- Q: Is anything written to disk folders or exported? -> A: no. Downloads keep the 007 behaviour
  (one file per item, named after the item). Folder art is never part of a download.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Saved items are filed automatically (Priority: P1)

The musician imports "Battery" by Metallica, saves the bass and the snare to the library and saves
"What I hear" from the mixer. In the library they find a "Metallica" folder, inside it "Master of
Puppets", inside that "Battery", holding the three items. Saving more items of the same song adds
them to the same folder; nothing has to be created by hand. Items saved before this feature are
filed the same way the first time StemStudio starts with it.

**Why this priority**: The literal owner request; every other story needs the filing.

**Independent Test**: Import two songs of one album and one song of another artist (fake stack),
save stems of each, open the library and check the folders and their contents; start a library
with items from before the feature and check that every item is filed.

**Acceptance Scenarios**:

1. **Given** a track whose band info was found (artist "Metallica", album "Master of Puppets", song
   "Battery"), **When** the user saves the `bass` stem, **Then** the item is in
   Library > Metallica > Master of Puppets > Battery, and the save confirmation names that path
   with a link to the song folder.
2. **Given** that folder exists, **When** the user saves `drums.snare` and "What I hear" of the same
   track, **Then** both appear in the same song folder and no second "Metallica" or "Battery"
   folder is created.
3. **Given** a second track whose artist is reported as "metallica " (lower case, trailing space),
   **When** its stem is saved, **Then** it is filed in the existing "Metallica" folder.
4. **Given** a track without band info (lookup "not found") imported from YouTube as
   "Motörhead - Ace of Spades (Official Video)", **When** a stem is saved, **Then** it is filed under
   "Motörhead" > "Unknown album" > "Ace of Spades".
5. **Given** a track with no artist at all (an uploaded file without tags), **When** a stem is
   saved, **Then** it is filed under "Unknown artist" > "Unknown album" > the track title.
6. **Given** a library with items saved before this feature, **When** StemStudio starts with it,
   **Then** every visible item is filed within 10 seconds for 5,000 items, while the library shows
   "Sorting your library" until done.
7. **Given** a sheet music item (008), a restyled take saved to the library (009) and a song from
   own lyrics (010), **When** the library is opened, **Then** the score and the restyled take are in
   the song folder of their source track and the own-lyrics song is under "My songs" > "Songs from
   my lyrics".
8. **Given** a track whose saved items exist, **When** the track is deleted, **Then** its items stay
   in their folders (shown as "source track deleted").
9. **Given** the last item of a song folder is deleted, **When** the library is shown, **Then** the
   song folder is gone, and so are the album and artist folders when they became empty.

---

### User Story 2 - Browse folders with art (Priority: P1)

The user opens the library and sees artist folders as tiles with pictures, the number of albums
and items. They open an artist, then an album, then a song, see where they are in a breadcrumb,
go back with one click, and play, download, rename or delete items inside the song folder as
before.

**Why this priority**: Folder art is a must (owner); browsing is how the folders are used.

**Independent Test**: With a seeded library (artist with three albums with covers, artist with one
album, artist without pictures), open each level, check the art, counts, breadcrumb and keyboard
navigation, then play, download, rename and delete an item inside a song folder.

**Acceptance Scenarios**:

1. **Given** the library root, **When** it opens, **Then** artist folders are shown A-Z (a leading
   "The" is ignored for sorting), each with its art, name, number of albums and items, and
   "Unknown artist" and "My songs" come last.
2. **Given** an artist with three albums that have covers, **When** its tile is shown, **Then** the
   tile shows a mosaic of the album covers; an artist with one album shows that cover; an artist
   without any picture shows a lettered placeholder.
3. **Given** an album folder, **When** it is shown, **Then** its tile and header show the album
   cover following the 005 precedence (a picture the user chose for one of its songs wins).
4. **Given** a song folder, **When** it opens, **Then** it shows the track's picture, a breadcrumb
   "Library > Metallica > Master of Puppets > Battery", the items in instrument order with the 007
   actions, and links to open the track and the mixer while the track exists.
5. **Given** any folder, **When** the user switches between grid and list view, **Then** the same
   folders are shown with art, and the choice is remembered on this device.
6. **Given** Advanced level, **When** the user sorts albums by year, name, recently added or size,
   **Then** the order follows the choice; Beginner sees name order (albums by year).
7. **Given** a keyboard-only user with a screen reader, **When** they move through tiles and the
   breadcrumb, **Then** every tile is one link announced as e.g. "Metallica, artist, 3 albums, 12
   items", focus is visible, and the breadcrumb is a navigation landmark with the current page
   marked.
8. **Given** a broken or missing picture, **When** the tile renders, **Then** the placeholder is
   shown instead and the layout does not shift.
9. **Given** a link to a folder that no longer exists (its items were deleted or moved), **When** it
   is opened, **Then** the user sees "This folder is empty now" with links to the library and the
   search.

---

### User Story 3 - Find anything by typing (Priority: P1)

The user types "sna" in the library search: while typing, the library shows the matching items
(every saved snare) with their path, plus any artist, album or song whose name matches. Typing
"metallica bass" narrows to Metallica's bass items. "motorhead" finds "Motörhead". Clearing the
search brings back the folder they were in.

**Why this priority**: Explicit owner requirement; with many songs browsing alone is too slow.

**Independent Test**: In the seeded library type one character at a time and check the results
after each pause, with one query per instrument family, an accented name typed without accents, a
two-word query, a query without matches, and the keyboard and screen-reader behaviour.

**Acceptance Scenarios**:

1. **Given** the library, **When** the user types one character, **Then** results replace the
   folder view within 0.4 s of the last key press, grouped as Artists, Albums, Songs and Items,
   each with its count.
2. **Given** the query "motorhead", **When** results show, **Then** "Motörhead" is found, and the
   matched part is highlighted (not by colour alone).
3. **Given** one query per instrument family - "vocal", "drum", "bass", "guitar", "piano",
   "other" - and one per detail stem family - "snare", "lead vocals", "strings", "acoustic guitar"
   - **When** each is typed, **Then** every item containing that stem or detail stem is found,
   including "What I hear" mixes that contain it.
4. **Given** the query "metallica bass", **When** results show, **Then** only items matching both
   words (in any of artist, album, song, instrument or item name) are listed.
5. **Given** a result, **When** it is shown, **Then** it carries its path (Artist > Album > Song)
   as links, and the item actions (play, download, rename, delete) work in the results.
6. **Given** the user opened a song folder and searched, **When** they clear the search (clear
   button or Escape), **Then** the same song folder is shown again with focus in the search box.
7. **Given** a query without matches, **When** results are empty, **Then** the user sees "No
   matches for '...'" with what the search looks at and a "Clear search" button.
8. **Given** a screen-reader user, **When** results change, **Then** the result count is announced
   once after typing pauses (polite), and Arrow Down moves from the search box to the first result.
9. **Given** 5,000 items, **When** the user types quickly, **Then** only the results of the latest
   text are shown (no flicker of older results) and the page stays responsive.
10. **Given** Advanced level inside a folder, **When** "Only in this folder" is ticked, **Then**
    results are limited to that folder.

---

### User Story 4 - Put items in the right folder (Priority: P2)

A YouTube import was filed under "Unknown album". The user opens the song folder, chooses "Edit
library info", types the album "Ace of Spades", and all items of that song move to Motörhead > Ace
of Spades > Ace of Spades. For one generated take they set another song name. Later, when band
info is found for another track, its automatic names update by themselves; the names the user
typed are kept.

**Why this priority**: Automatic data is sometimes wrong or missing; without a fix the folders lose
trust. P2 because the automatic path already files every item.

**Independent Test**: Edit the album of a track, check that all its items moved and the old empty
folders disappeared; edit one item; set a field back to automatic; complete a band info lookup
for another track (fake worker) and check that its items moved while edited names stayed.

**Acceptance Scenarios**:

1. **Given** a song folder of an existing track, **When** the user sets the album in "Edit library
   info" and saves, **Then** every item of that track that has no own names is filed under the new
   album, and the view follows to the new song folder.
2. **Given** an item, **When** the user sets its song name, **Then** only that item moves.
3. **Given** a name the user set, **When** they tick "Automatic" for it and save, **Then** the
   automatic name is used again.
4. **Given** a track with automatic names, **When** its band info is found later (artist "Metallica"
   instead of the reported "MetallicaTV"), **Then** its items move to the matched folders without
   user action.
5. **Given** a name the user set, **When** a later lookup finds other names, **Then** the user's name
   stays.
6. **Given** invalid input (empty after trimming, longer than 200 characters, control characters),
   **When** the user saves, **Then** a readable message is shown and nothing changes.
7. **Given** a track whose items have been filed, **When** the user corrects title or artist for the
   band info lookup (005), **Then** the dialog offers "Look up band info again" and the result
   follows rule 4 or 5.

---

### User Story 5 - The composer agent knows the folders (Priority: P3)

In the agent panel the user asks "which stems do I have of Metallica?" or "put the Ace of Spades
items on the album Ace of Spades". The agent browses and searches the folders and, after approval
when needed, changes the library info.

**Why this priority**: The agent (011) already lists exports; folders make its answers match what
the user sees. P3 because the UI covers the need.

**Independent Test**: Agent evaluation cases on the fake stack: list an artist's items, search an
instrument, open a folder, set an album (approval shown when the run is tainted), undo it.

**Acceptance Scenarios**:

1. **Given** the library page, **When** the user asks for an artist's items, **Then** the agent uses
   the library search or browse tools and answers with the folder names; names are treated as
   untrusted text (the run becomes tainted).
2. **Given** a tainted run, **When** the agent wants to change library info, **Then** an approval
   card shows the exact names first; Undo restores the previous names.

### Edge Cases

- A name that is only punctuation, emoji or spaces after cleaning: the fallback label is used
  ("Unknown artist", "Unknown album", the track title, or "Untitled song").
- Very long names (up to 500 characters from 005): folder names are cut to 200 characters with an
  ellipsis in the display; the comparison uses the cut name.
- Two different bands with the same name (MusicBrainz disambiguation): they share one folder; the
  user can rename one via library info (documented limitation).
- The matched artist is "Various Artists" (a compilation match): ignored for the folder; the
  reported artist is used.
- Non-Latin scripts (Cyrillic, Greek, CJK): kept as they are; case and width are folded where the
  script has them; sorting uses the invariant culture order.
- A track that is re-imported (new track id): its new items join the existing folders by name.
- An item whose track and source item are both gone: it keeps its last place.
- A user picture chosen for one song of an album: it becomes that album's cover (005 precedence:
  the user picture ranks first). The user can reset it on the track page.
- A picture of a deleted track: deleted with the track (005 FR-016); its folders use another
  song's picture or the placeholder.
- Items still rendering or failed: filed at once, shown with their status inside the song folder
  and in the search results.
- The search text contains only spaces or more than 100 characters: spaces are ignored (folder view
  stays); longer text is cut at 100 characters in the box.
- More than 20 results in a group: the first 20 by relevance are shown with "Show all N".
- The library is filed while the user browses (start-up backfill): the folders show "Sorting your
  library (n items left)" and refresh once done.
- A deep link `/library/<folderId>` from before a rename: "This folder is empty now".
- Library opened in the "All items" view: the 007 list with its filters, plus each item's path.

## Requirements *(mandatory)*

### Functional Requirements

**Filing (US1)**

- **FR-001**: Every library item except recorded play-along takes (015) MUST be filed in exactly one
  song folder inside one album folder inside one artist folder.
- **FR-002**: Filing MUST happen automatically when an item is created (export, stem pack, sheet
  music, generated take saved to the library, and any later origin), in the same save, so the item
  never appears unfiled.
- **FR-003**: Each level's name MUST be chosen by this precedence: the item's own name for the level
  (set by the user) > the track's library info for the level (set by the user) > the band info match
  (005: matched artist, album, recording title; only when the lookup status is "found") > the names
  reported by the import (005 lookup names, then the raw track artist/title) > the fallback
  ("Unknown artist", "Unknown album", the track title). For an item without a source track the
  source item's place is used; for an item without either, the 010 rule applies (FR-006).
- **FR-004**: Names MUST be compared by a normalised key: Unicode compatibility decomposition,
  accents removed, case folded, the letters ß, æ, ø, ł, đ and þ mapped to ss, ae, o, l, d and th,
  curly quotes and dashes unified, whitespace collapsed and trimmed, and a leading "The " removed.
  Two names with the same key MUST share one folder. A key that is empty uses the fallback.
- **FR-005**: The artist key MUST leave out featuring parts ("feat.", "ft.", "featuring", also in
  brackets) and MUST NOT split on "&", "and", "," or "x". A matched artist "Various Artists" MUST be
  ignored for filing.
- **FR-006**: Items without a source track and without a source item (e.g. a song from own lyrics)
  MUST be filed under "My songs" > "Songs from my lyrics" > their title.
- **FR-007**: Folders MUST be derived from the filed items: a folder exists while it holds at least
  one item; no empty folder is ever shown or stored.
- **FR-008**: Each item MUST keep its place when its track is deleted.
- **FR-009**: Items with automatic names MUST be re-filed when their track's band info is found or
  changes, when the track's library info changes, or when the filing rules change version; names set
  by the user MUST never be changed automatically.
- **FR-010**: On the first start with this feature (and after a restore of an older backup) every
  item MUST be filed by a background step that is idempotent and finishes within 10 s for 5,000
  items; until it finishes the library MUST show how many items are left.
- **FR-011**: The folder's displayed name MUST be the band info spelling when at least one item in it
  was filed from a match, otherwise the spelling most of its items use (ties: the earliest filed).
- **FR-012**: The save confirmation of 007 (and of 008/009 saves) MUST show the folder path and link
  to the song folder.

**Browsing and art (US2)**

- **FR-013**: The library MUST open at the artist level and offer artist, album and song levels with a
  breadcrumb (Library > Artist > Album > Song); each level MUST be addressable by URL.
- **FR-014**: Artist tiles MUST show the art, name, number of albums and items; album tiles the art,
  name, year when known, number of songs and items; song tiles the art, name and number of items.
  Each folder MUST show its total size.
- **FR-015**: Song folder art MUST be its track's picture; album folder art MUST be the best picture
  among its songs' tracks by the 005 precedence (ties: the most recently changed track); artist
  folder art MUST be a mosaic of up to four album covers (one cover when only one album has one);
  folders without any picture MUST show a placeholder with the first letter of the name (a question
  mark for unknown folders) on a colour of the design system derived from the name.
- **FR-016**: Folder art MUST reuse the stored 005 pictures (no new picture files, no new downloads),
  load lazily with fixed dimensions, use the large picture on high-density screens, and fall back to
  the placeholder when a picture fails to load.
- **FR-017**: Pictures in folder art MUST be shown in the app only, never added to downloads or
  exports; the personal-practice notice of 005 covers them.
- **FR-018**: The default order MUST be: artists A-Z by name without a leading "The", with "Unknown
  artist" and "My songs" last; albums by year (unknown years last), then name; songs A-Z; items in
  the instrument order of the mixer (standard stems, then detail stems, then mixes, sheet music and
  generated takes), then newest first. Advanced users MUST be able to sort folders by name, year
  (albums), recently added, size or number of items.
- **FR-019**: A grid view (default) and a list view MUST be offered at every level; the choice MUST be
  remembered per device.
- **FR-020**: Inside a song folder the 007 item actions (play, download, rename, delete with
  confirmation, try again for failed items, status updates without reload) MUST work unchanged, plus
  links to the track and its mixer while the track exists.
- **FR-021**: The 007 flat list MUST stay available as "All items" (Advanced) with its filters, and
  each row MUST show the item's path.

**Live search (US3)**

- **FR-022**: The search box MUST be visible at every level and filter from the first character,
  updating 200 ms after the last key press; Enter MUST search at once; only the results of the latest
  text MUST be shown.
- **FR-023**: Matching MUST ignore case and accents (same folding as FR-004 without the "The" and
  featuring rules), split the text into words (at most 8) and require every word to be found as a
  part of at least one field: artist (folder name and the item's full artist credit), album, song,
  stem or instrument, or item name.
- **FR-024**: The stem/instrument field MUST contain, for every channel in the item: the stem or
  detail stem name (`drums.snare` as "drums snare"), its display label ("Snare", "Hi-hat", "Lead
  vocals", "Acoustic guitar", ...) and a short, equal list of everyday words per instrument family
  (e.g. "voice" for vocals, "keyboard" for piano); for sheet music the instrument profile; for
  generated takes "mix" plus the stems they contain when known.
- **FR-025**: Results MUST be grouped as Artists, Albums, Songs and Items, each ordered by relevance
  (whole name > start of a word > inside a word, then name) and limited to 20 with "Show all N".
  A folder MUST be a result only when at least one word matches its own name. Each result MUST show
  its path and highlight the matched parts with a marker that does not rely on colour alone.
- **FR-026**: Clearing the search (button or Escape) MUST restore the folder that was open, with focus
  in the search box; the search text MUST be kept in the URL so Back and reload restore it.
- **FR-027**: The search MUST be accessible: a labelled search field, results in lists with group
  headings, a polite live announcement of the result count once typing pauses, Arrow Down from the
  field to the first result, and visible focus.
- **FR-028**: Without matches the library MUST say so, list what the search looks at and offer "Clear
  search"; a failed search MUST show a readable reason and keep the previous results.
- **FR-029**: Advanced users MUST be able to limit the search to the open folder.
- **FR-030**: With 5,000 items the results MUST appear within 0.4 s of the last key press (95th
  percentile on the reference machine), and the server part of a search MUST take at most 50 ms.

**Library info (US4)**

- **FR-031**: The user MUST be able to set, for a track and for a single item, the artist, album and
  song name used for filing; each name 1-200 characters after trimming, without control characters;
  each name MUST be resettable to automatic.
- **FR-032**: Changing a track's library info MUST re-file all its items without own names, and the
  view MUST follow to the new folder.
- **FR-033**: The library info dialog MUST show for each name whether it is automatic and where the
  automatic name comes from (band info, import, or fallback), and offer the 005 "Look up band info
  again" for tracks.

**Agent (US5)**

- **FR-034**: The composer agent MUST be able to browse a folder, search the library, open a folder
  in the UI and set library info; folder and item names MUST reach the model only as untrusted text
  (the run is tainted), and setting library info MUST be undoable and need approval in a tainted run.

**Cross-cutting**

- **FR-035**: Every new screen part MUST follow the 004 design system and journey levels: Beginner
  sees folders, art, breadcrumb, search, item actions and library info (owner decision 2026-11-16:
  "Edit library info" at every level); sorting, "All items" and "Only in this folder" are
  Advanced. Each new area MUST have an in-app help topic.
- **FR-036**: Every instrument family MUST be served equally by the search (FR-024 has one entry per
  family) and the acceptance tests MUST contain at least one search case per family.
- **FR-037**: No folder or file MUST be created on the user's disk outside the app's internal
  storage; nothing about the folders MUST leave the machine.
- **FR-038**: Library info and filing MUST be part of the 013 backup (they live in the database);
  folder art MUST need nothing extra in a backup.

### Key Entities

- **Placement** (per item): artist, album and song names with their keys and where each name came
  from (item, track, match, import, fallback), the album year when known, the filing-rules version.
- **Library info** (per track and per item): optional artist, album and song names set by the user.
- **Folder** (derived, not stored): level (artist, album, song), id derived from the keys, display
  name, counts, size, art.
- **Search result**: grouped folder hits and item hits, each with path and highlighted parts.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100 % of saved items appear in a song folder at once after saving (no unfiled item in
  any test run).
- **SC-002**: Names that differ only in case, accents, spacing, quotes/dashes or a leading "The"
  land in one folder in 100 % of the normalisation test cases (at least 40 cases).
- **SC-003**: A library of 5,000 items from before the feature is fully filed within 10 s of start.
- **SC-004**: With 5,000 items, results show within 0.4 s of the last key press (95th percentile),
  folder pages load within 0.3 s, and the API answers a search within 50 ms (95th percentile).
- **SC-005**: Every folder shows art: a picture when any of its songs has one, otherwise the
  placeholder; no broken image icon in any visual test.
- **SC-006**: At least one search case per instrument family (vocals, drums, bass, guitar, piano,
  other) and per detail stem group finds every matching item and no item without that stem.
- **SC-007**: A first-time user (Beginner) finds a saved stem of a named song by browsing or search
  in under 30 seconds in the usability walk-through (qa script).
- **SC-008**: New screens pass the automated accessibility checks (axe, WCAG 2.2 AA) with no serious
  or critical findings, at every level and in both themes.

## Assumptions

- 005's lookup data (matched names, lookup names, album, year) is available for tracks imported since
  005; older tracks get it when 005 schedules their own lookup (FR-012 of 005); until then their
  items use the import names.
- MusicBrainz recording positions (track numbers) are not stored; songs are sorted by name.
- The library is single-user and local; at most about 10,000 items are expected (5,000 is the
  performance target).
- The 013 backup copies the database, so the new data is backed up without a format change.
- A future feature may add licensed artist photos; it would become the first choice of artist art.
- Out of scope (owner confirmed 2026-11-16 for the track list and the folder archive): writing
  folders to disk, downloading a whole folder as an archive, renaming a whole
  folder in one step (use library info per track), grouping the track list (`/tracks`) by artist,
  album artist / disc / track number, user-made folders or playlists (setlists are 014).
