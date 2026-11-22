# Play-along recording

Record yourself over the mix: sing the vocals, play the guitar or bass part, the keys or the
drums, and hear back how your take sits against the original. StemStudio lines every take up with
the stems, keeps it on this computer and lets you compare it with the original part.

## Before you start

- Open StemStudio **on the computer it runs on**, as `http://localhost:8080` or
  `http://127.0.0.1:8080`. Browsers allow the microphone only there (a *secure context*); from
  another device in your network the panel says *Not on this computer*.
- Use **Chrome or Edge**. Firefox may refuse audio interfaces that do not run at 44.1 kHz.
- Use **headphones**. Speakers would be recorded together with you.
- Set the input level (gain) on your interface or microphone. StemStudio cannot change it; its
  input meter shows whether the level is right.

## Your first take

1. Open the mixer of a song and press **Record** in the toolbar. The recording panel explains that
   recordings stay on this computer: the audio goes only to the StemStudio server on this machine,
   never to the internet.
2. Press **Allow microphone** and accept the browser's prompt (see *Microphone permission*
   below).
3. Choose your **input**: a USB microphone or an input of your audio interface. StemStudio
   remembers it. Check that the input meter moves while you play and does not show clip.
4. Choose **what you record**: vocals, guitar, bass, keys, drums, other, or none. *Mute the
   original part while recording* keeps the original stem silent, so you hear yourself instead.
5. Press **Record** (<kbd>R</kbd>). After the count-in (one bar by default) you play along; press
   **Stop** (<kbd>R</kbd> again). While it records, the panel shows the recording state and the
   elapsed time.

The take plays at once in the **Take** strip next to the stems and appears in the **Takes**
panel. *Saved* means the StemStudio server has stored it as lossless FLAC (24-bit, 44.1 kHz). A
song can have up to 200 takes. A silent take is refused; a very quiet one is marked *quiet*.

When the panel is closed, <kbd>R</kbd> opens it first, because the input must be chosen before
you can record. Closing the panel or the mixer releases the microphone.

## Connecting your instrument

| Instrument | How to connect | Part |
|---|---|---|
| Vocals | A USB microphone appears as its own input. A dynamic or condenser microphone goes into an interface input; condensers need phantom power (the 48V switch). Keep a hand's width from the microphone and set the gain so loud notes stay below clip | **Vocals** |
| Electric guitar or bass (DI) | Plug the instrument into an interface input marked **Hi-Z** or **Instrument**, or put a microphone in front of the amp. Mono is right for one instrument; at the Advanced level choose **mono input 1** or **mono input 2** for the input you use | **Guitar** or **Bass** |
| Acoustic and other instruments | A microphone about 30 cm from the instrument, in a quiet room. Choose the part you replace, or **None** when you add something that is not in the song; then nothing is muted | **Other / acoustic** or **None** |
| Keys and synths (stereo) | The keyboard's left and right outputs into two interface inputs, **stereo** (Advanced). An acoustic piano: one microphone (mono) or two (stereo) | **Keys / piano** |
| Drums | An electronic kit: its stereo output into two inputs, **stereo**. An acoustic kit: one overhead microphone (mono) or a stereo pair. Turn the gain down, drum hits clip easily | **Drums** |

Browsers record **at most two channels** of an interface. Echo cancellation, noise suppression and
automatic gain are switched off, so you record the plain signal.

## Microphone permission

StemStudio asks for the microphone only after you press **Allow microphone**. The browser then
shows its own prompt. When something is wrong the panel says what and what to do:

| Message | What to do |
|---|---|
| **Blocked** | Click the lock or site icon in the address bar, allow the microphone for this site, then **Try again** |
| **No input found** | Connect the microphone or interface and **Try again** |
| **In use** | Another program (a video call, a recording program) holds the input. Close it and **Try again** |
| **Disconnected** | The input was unplugged while the panel was open; a running take is kept up to that moment and marked *partial* |
| **Not on this computer** | Open StemStudio on the computer it runs on (`localhost` or `127.0.0.1`) |
| **Browser not supported** | Use Chrome or Edge |

## Latency calibration

Sound needs a moment to travel out of the speakers or headphones and back in through the input.
StemStudio moves every take back by that **latency**, so it lines up with the stems.

1. In the recording panel press **Calibrate latency**.
2. Best: connect an output of your interface to one of its inputs with a cable. Without a cable,
   hold a headphone cup against the microphone (a clap test is less exact).
3. Press **Start**. StemStudio plays a few clicks and listens for them. Within a few seconds the
   result appears, for example *25.0 ms (8 of 8 clicks, spread 0.1 ms)*. It is saved for this
   input and used for every take.

If it fails, the dialog explains why: no clicks came back, too few, they arrived at different
times, or the delay is longer than one second. Follow the tip and try again. At the Advanced level
you can **enter the latency manually** (0 to 1,000 ms in steps of 0.1 ms); the Expert level also
shows it in samples. Without a calibration StemStudio uses the browser's estimate and says so.
*Your audio setup changed* appears when the output latency moved by more than 5 ms: calibrate
again.

## Monitoring

**Monitoring** plays your input on the output while you record. It is off by default.

- Use headphones: with speakers the input feeds back and is recorded too.
- The monitored sound arrives a little late (the round-trip latency). Direct monitoring on your
  interface or amp has no delay and is better where you have it.
- At the Advanced level you set the monitoring volume. Monitoring is never part of a take or an
  export.

## Loop takes

Set an A-B loop around a passage (chapter 5, or loop a section, chapter 7), switch the loop on and
press **Record**. Every pass becomes its own take of the loop; a last pass of at least 1 second is
kept and marked **partial**. A session stops after 50 passes or 30 minutes.

At the Advanced level the Takes panel groups the passes of one session and shows **Pass 1**,
**Pass 2** and so on. Star the best pass and choose **Delete the other passes** to keep only that
one.

## The Takes panel

The Takes panel lists the takes of the song, newest first: name (for example *Take 3 - Bass*),
part, length and status (*Saving*, *Saved* or failed with **Try again**). For each take you can:

- **select** it, so it plays in the Take strip, and play it from its start;
- **rename** it (1 to 80 characters) and **star** it;
- **delete** it; its files are removed from the disk;
- **export** it (see below).

At the Expert level each take also shows its dropouts, clipped samples and the input device.

## Compare original, take or both

When a take is in the **Take** strip:

- **My take** plays your take and mutes the original part.
- **Original** plays the original part and mutes your take.
- **Both** plays both.

Press <kbd>C</kbd> to cycle. The switch is sample-exact and leaves your own mute and solo settings
alone. A take recorded with part **None** has only an on/off switch. The Take strip has volume,
mute, solo, a meter and, at the Advanced level, pan; its settings are saved with the mix (chapter
6).

## Adjusting the timing (Expert)

At the Expert level the Take strip has a **timing** slider from −200 to +200 ms in steps of
0.1 ms. You hear the shift at once; **Apply** stores the take again with the new timing, **Reset**
goes back. A positive value moves the take later. The take keeps playing from its old audio until
the new one is saved. At the Advanced level the strip shows *Timing adjusted by X ms* for a stored
adjustment. For a constant offset, calibrating the latency is the better fix.

## Saving a take to the library

In the Takes panel choose **Export** on a take:

- **Take only** saves the take itself, trimmed to where you played.
- **Take + mix** saves what you hear: the mix with your take, including the compare choice.

The formats are those of other exports (FLAC, WAV, MP3; chapter 14). The item appears in the
**Library** with a name that starts with the take's name. A take that is still being saved can be
exported once it shows *Saved*. The mixer's own **Save to library** does not include the take;
take exports always start from the Takes panel. Like every export, it uses the original speed and
key.

## Speed, key and recording

Takes are recorded at the song's **original speed and key**, so they line up sample-exactly with
the stems. When the speed is not 100 %, the song is transposed or the speed trainer runs, **Record**
is disabled with the reason; **Set to 100 %** restores the original speed and key in one click.
Playing takes back works at every speed and transpose: a take follows the stems like any other
channel.

## Privacy and storage

- Recordings stay on this computer. The audio goes only to the StemStudio server on this machine,
  never to the internet.
- Takes are never offered to the AI composer agent (chapter 21) and cannot be used as a voice
  sample (chapter 19).
- Takes are kept with their song, not in the library list; their exports are library items.
  Deleting a song deletes its takes; the confirmation says how many.
- A stereo take uploads about 16 MB per minute (mono about half); the stored FLAC is usually
  smaller. A take can be as long as the song, up to 15 minutes. **Library care** (chapter 22)
  counts takes as *recordings*, and a backup includes them unless you leave that part out.

## Levels

| Level | What you see |
|---|---|
| Beginner | Record, input choice, input meter, count-in, guided calibration, the Takes panel, compare, export |
| Advanced | Also mono input 1, mono input 2 or stereo, monitoring volume, manual latency, grouped loop passes, take pan, timing note |
| Expert | Also latency in samples, the timing adjustment, dropouts, clipped samples and the device per take |
