# The export library

<span class="planned">In development</span> Feature 007. This chapter describes the
specified behaviour.

Save any stem, detail stem or mix as an audio file in your personal **Library**, to play
it later, download it or use it for sheet music and style transfer.

## Saving to the library

- **From the track page**: **Save to library** next to each stem and each kept detail stem,
  or **Save all stems** to save them in one go (optionally with the detail stems). A single
  stem is saved exactly as separated: no volume change, centred, no master volume.
- **From the mixer**: **Save to library**, then choose:
  - **What I hear**: the current mixer settings, also when they are not saved yet.
  - **Selected channels** (Advanced): the ticked channels at their own volume and pan,
    ignoring mute and solo, with the master volume applied.

The metronome is never included.

## Formats

| Format | Options | Default |
|---|---|---|
| FLAC | 16 or 24 bit | Preselected; 16 bit for a single stem, 24 bit for a mix |
| WAV | 16 or 24 bit | |
| MP3 | 192, 256 or 320 kbit/s | 320 kbit/s |

All files are 44.1 kHz stereo. At Advanced level you can also save only **the loop region**
or a start and end time (at least half a second).

If a mix would be too loud and clip, StemStudio lowers it just enough and the library item
says *"Lowered by X dB to avoid clipping"*.

## The Library screen

Open **Library** in the navigation. Each item shows its name, source track, contents (for
example *drums + bass, mixed*), format, duration, size, date and status (queued, rendering,
ready or failed). The total size of the library is shown at the top. You can filter by track
and kind and search by name; the status updates live.

| Action | Effect |
|---|---|
| Play | Plays the file in the browser |
| Download | Saves the file under the item's name |
| Rename | 1 to 120 characters |
| Delete | Removes the item and its file after you confirm |
| Try again | Repeats a failed export |

Deleting a track keeps its library items; they then show *source track deleted*. A single
stem is usually ready within 15 seconds and a mix within 30 seconds.

## Messages you may see

- *"Nothing to export: no channel is audible"* or *"… the selected channels are silent"*.
- The export is refused when less than 1 GB of disk space would remain.
- *"failed (interrupted by restart)"*: use **Try again**.

Library files are for your personal practice. The copyright notice on the screen repeats
this rule.
