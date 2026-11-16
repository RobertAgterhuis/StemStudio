# Troubleshooting and FAQ

## StemStudio does not open

- Check that all three services are running: in the terminal, `docker compose ps` should
  show *web*, *api* and *worker* as healthy.
- The health pages http://localhost:8080/health and http://localhost:8081/health should
  answer *ok*.
- Ports 8080 and 8081 must be free. Close other programs that use them.

## An import is refused or fails

| Message or symptom | What to do |
|---|---|
| The link is refused | Use a single YouTube video or a Spotify **track** link; playlists, albums and other sites are not supported |
| *No matching YouTube audio was found for this Spotify track.* | Find the song on YouTube and import that link |
| *Audio is longer than 15 minutes* | Use a shorter version or cut the file |
| File refused | Use MP3, WAV or FLAC, at most 200 MB |
| *The processing service is unavailable …* | Restart StemStudio (`docker compose up`); existing tracks keep working |
| YouTube downloads fail for many songs | Update StemStudio; the YouTube downloader needs regular updates |
| *interrupted by restart* | StemStudio was stopped during processing; press **Retry** |

## Processing is slow

- On a CPU, separating a 4-minute song takes several minutes and detail separation about
  half an hour. Use the GPU option (chapter 2) if you have an NVIDIA card.
- Jobs run one at a time; new imports go before detail separations.
- If the import screen says the CPU is used although you have a GPU, read the reason under
  **Processing device**. Common causes: StemStudio was started without the GPU option, the
  driver is too old, or another program (a game, a local AI model) uses the graphics memory.

## Detail separation fails

- *"Not enough memory for detail separation."*: give Docker at least 20 GB of memory.
- *"Detail model could not be downloaded; try again when online."*: the first detail
  separation needs an internet connection.
- *"Not enough disk space for detail separation."*: free some disk space.
- *"The standard stems of this track are no longer available in the worker; re-import the
  track."*

## The click does not match the music

Beat detection assumes a steady tempo and can place the first beat of the bar wrongly.
Switch to Advanced and use **BPM override**, **offset** and **beats per bar**, or **Reset**
to go back to the detected values.

## Speed or transpose is disabled or sounds wrong

- *Speed and transpose are unavailable*: the browser could not load the time-stretch module
  (the message names the reason). Use a current Chrome, Edge or Firefox and reload the mixer;
  playback keeps working at the original speed and key.
- *Smeary drums or chipmunk voices*: expected below 70 % or beyond 7 semitones (chapter 5,
  *Sound quality*).
- *Dropouts* in the Expert **Stretch** line: the computer could not keep up. Close other
  programs or collapse detail groups.
- *The speed trainer or count-in cannot be turned on*: the trainer and *Before every loop
  pass* need an enabled loop of at least 0.5 s; the count-in needs a beat grid or a manual BPM.

## Settings and help look wrong

**Reset help and tours** in the preferences restores the welcome panel, hints and tours. To
reset all preferences, clear the site data of StemStudio in your browser. In a private window
StemStudio cannot store preferences and stays at the Beginner level.

## The agent is not available

The agent panel shows the reason and the fix:

- *"Local AI not reachable"*: Ollama is not running on the computer that runs StemStudio.
  Start it (on Windows from the tray icon) and reopen the panel.
- *"Model qwen3.5:9b is not pulled."* with the fix `ollama pull qwen3.5:9b`: the
  administrator runs that command once (and `ollama pull qwen3.5:4b` for the fallback model).
- *"Model … does not support tool calling."*: choose `qwen3.5:9b` in **Settings**.
- *"Claude rejected the API key"*, *"Claude could not be reached"* or *"Claude is rate limited
  …"*: press **Switch to local** or choose Ollama in **Settings**.
- *"No Claude key is configured"* or *"The saved key cannot be read on this installation"*:
  enter the key in **Settings -> Composer agent -> Claude API key** (on the computer itself, as
  `http://localhost:8080`), or press **Switch to local**.
- *"The key store is not available"*: the folder for the encryption keys cannot be used; the
  administrator checks `StemStudio__DataProtection__KeyRingDir` (technical manual, chapter 3).
- The agent is set to **None** in **Settings**.

Everything else in StemStudio keeps working while the agent is unavailable. If the agent does
something you did not mean, press **Undo**; heavy and destructive steps always wait for your
approval.

## Frequently asked questions

**Does StemStudio send my music anywhere?** No. Audio is processed on your computer. Only
the online lookups for cover art and band info (chapter 7) send the title, artist, duration
and ISRC code, and lyrics lookups (chapter 8) the title, artist, album and duration; both
can be switched off. The optional Claude backend for the agent (chapter 13) sends text only,
never audio, and only when you select it.

**Can I separate more than six instruments?** Detail separation (chapter 5) splits drums,
vocals and the remaining instruments further. Several guitars of the same kind cannot be
separated from each other.

**Can I use StemStudio on several computers or with several people?** StemStudio is made for
one person on one computer. Do not expose its ports to the internet.

**How do I make a backup?** Ask your administrator, or see *Operations* in the technical
manual: your data lives in Docker volumes that can be backed up.

**Does an export keep my practice speed or key?** No. Exports to the library always use the
original speed and key; the practice tempo is only for listening and is saved with the mix.

**Which browsers are supported?** Chrome, Edge and Firefox are tested; Safari works on a
best-effort basis.
