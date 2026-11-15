# The export library

Save any stem, detail stem or mix as an audio file in your personal **Library**, to play
it later, download it or use it elsewhere for your own practice.

## Saving to the library

- **From the track page**: the row **Save to library:** has one button per stem and per
  kept detail stem (for example *bass* or *snare*). A single stem is saved exactly as
  separated (*unprocessed (raw)*): no volume change, centred, no master volume.
- **Save all stems** (Advanced) saves every stem in one go; tick **Also save the detail
  stems** to include the detail stems of the track.
- **From the mixer**: press **Save to library** and choose under **What to save**:
  - **What I hear**: the channels you hear with the current mixer settings (mute, solo,
    volume, pan and master), also when the mix is not saved yet.
  - **Selected channels** (Advanced): the ticked channels at their own volume and pan,
    ignoring mute and solo, with the master volume applied. At least one channel must be
    ticked.

The metronome is never included. After **Save to library** in the dialog it confirms *"Saved "<name>" to the
library. It is being prepared in the background."* with a link **Open in library**. The
default name is the song title followed by the contents, for example *drums, raw* or
*vocals + bass, mixed*.

## Formats

| Format | Options (Advanced) | Default |
|---|---|---|
| FLAC (lossless) | 16-bit or 24-bit | Preselected; 16-bit for a single stem, 24-bit for a mix |
| WAV (uncompressed) | 16-bit or 24-bit | as FLAC |
| MP3 (small) | 192, 256 or 320 kbps | 320 kbps |

All files are 44.1 kHz stereo. FLAC and WAV files carry the title, artist and a comment; MP3
files have no tags. At Advanced level, **Part of the song** lets you save only **the loop
region** (when the mixer has one) or a **Start (s)** and **End (s)** time, at least 0.5
seconds long.

If a mix would clip, StemStudio lowers it just enough (peak at −0.1 dBFS) and the library
item says *"Lowered by X dB to avoid clipping"*.

## The Library screen

Open **Library** in the navigation. The total size of the library is shown at the top. Each
item shows its name, the contents, format, duration and size, the source track, its status
(*Queued*, *Rendering N %*, *Ready* or *Failed* with the reason) and the date it was created.
The status updates live. At Advanced level you can filter by track (**All tracks**) and kind
(**All kinds**) and use **Search items**.

| Action | Effect |
|---|---|
| Play | Plays the file in the browser (ready items) |
| Download | Saves the file under the item's name (ready items) |
| Rename | A new name of 1 to 120 characters |
| Delete | Removes the item and its file after you confirm; the track stays |
| Retry | Renders a failed item again |

Deleting a track keeps its library items; their source then shows the track title with
*(deleted)*, and such an item can no longer be rendered again.

## Messages you may see

- *"Nothing to export: no channel is audible"*, or *"Nothing to export: the selected
  channels are silent"*.
- *"Not enough disk space"*: less than 1 GiB is free on the data drive; delete items or free
  space first.
- An item that was being prepared when StemStudio restarted fails with *"interrupted by
  restart"*: use **Retry**.
- *"The track is still being processed; export it when it is completed."*

Library files are for your own practice, like the imports they come from.
