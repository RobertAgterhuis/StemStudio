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

## A backup or restore does not start

- **"The backups folder is not available" or "read-only"**: create the folder (`./backups` next
  to `docker-compose.yml`, or the folder in `STEMSTUDIO_BACKUP_HOST_DIR`) and make it writable;
  on Linux `sudo chown 1654 ./backups` (chapter 16).
- **Not enough space**: the estimate needs the archive size plus a small reserve free in the
  backups folder; a restore needs the unpacked size free on the library volume.
- **A restore lists active jobs**: wait for the listed imports, splits, exports or generations,
  or cancel them, then restore again.
- **"Not compatible"**: the backup was made by a newer StemStudio. Update StemStudio first.
- **A previous library exists**: undo or discard it before the next replace restore.

## The library or its search shows something unexpected

- **An item is in the wrong folder, or under *Unknown artist* or *Unknown album***: use
  **Library info** on the song folder or the item to set the names (chapter 10). A name you set
  is never changed by StemStudio; automatic names follow the band info when it is found later.
- **The library says *Sorting your library (n items left)***: StemStudio is filing items that
  were saved before the folders existed, or after a restore of an older backup. Wait a moment;
  it finishes by itself.
- **The search finds nothing**: it needs every word you typed in the same result. Remove a
  word, check the spelling, or look at what the search covers (chapter 10). *guitar* does not
  find bass; use *bass*. A search of more than 100 characters is cut in the box.
- **A folder disappeared**: a folder exists only while it holds an item. Its items were
  deleted or filed elsewhere; search for the song by name.
- **I cannot find the flat list of items**: it is **All items**, at the Advanced level.

## The lyric assistant does not work

- **The buttons are disabled**: the sheet must be saved and its language must be Dutch or
  English. For *unknown* choose the language first (chapter 14).
- ***The assistant works on your own lyrics only***: the sheet matches the lyrics of a track in
  your library. If the text is your own, use **This is my own text**.
- ***Grammar, flow and writing help need the AI model***: the model is switched off in
  **Settings**, Ollama is not running, or the graphics card is busy with another job. The
  spelling check, the syllable counts and the rhyme finder still work. Fix the cause named in
  the pane (chapter 15 explains the backends) and press the button again.
- ***A check is running***: only one assist runs at a time; wait for it or press **Cancel**.
- **Suggestions are outdated**: you changed the text or the language after the check. Check
  again.
- **Nothing happens after pressing a button with Claude selected**: the notice *Your lyrics go
  to Anthropic* waits for **I agree**. Nothing is sent before you agree.
- **Dutch suggestions are weaker than English ones**: the local model is less good in Dutch;
  read every suggestion before you accept it.

## Frequently asked questions

**Does StemStudio send my music anywhere?** No. Audio is processed on your computer. Only
the online lookups for cover art and band info (chapter 7) send the title, artist, duration
and ISRC code, and lyrics lookups (chapter 8) the title, artist, album and duration; both
can be switched off. The optional Claude backend for the agent (chapter 15) sends text only,
never audio, and only when you select it.

**Can I separate more than six instruments?** Detail separation (chapter 5) splits drums,
vocals and the remaining instruments further. Several guitars of the same kind cannot be
separated from each other.

**How do I move my library to another computer?** Make a backup (chapter 16), copy the
archive into the backups folder of the other computer and restore it there. A stored Claude
key must be entered again on the other computer: its encryption keys never leave the first one.

**Can I use StemStudio on several computers or with several people?** StemStudio is made for
one person on one computer. Do not expose its ports to the internet.

**How do I make a backup?** Ask your administrator, or see *Operations* in the technical
manual: your data lives in Docker volumes that can be backed up.

**Does an export keep my practice speed or key?** No. Exports to the library always use the
original speed and key; the practice tempo is only for listening and is saved with the mix.

**Does the lyric assistant send my lyrics anywhere?** Not by default: the dictionary, the
syllable counter, the rhyme finder and the local model run on your computer. Only when you
select Claude and press **I agree** in the notice is the text of the sheet sent to Anthropic,
for the check you asked for (chapter 14).

**Does the library create folders on my disk?** No. Artist, album and song folders exist inside
StemStudio only; downloads, exports and backups are not changed (chapter 10).

**Which browsers are supported?** Chrome, Edge and Firefox are tested; Safari works on a
best-effort basis.
