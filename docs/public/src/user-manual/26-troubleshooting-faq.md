# Troubleshooting and FAQ

## StemStudio does not open

- Check that all three services are running: in the terminal, `docker compose ps` should
  show *web*, *api* and *worker* as healthy.
- The health pages http://localhost:8080/health and http://localhost:8081/health should
  answer *ok*.
- Ports 8080 and 8081 must be free. Close other programs that use them.
- Another device in your network cannot open StemStudio: that is on purpose (see
  *Getting started*, "Only this computer can open StemStudio"). When port 8081 answers only
  *Bad Request*, the address you used is not in `STEMSTUDIO_API_ALLOWED_HOSTS`; on this
  computer use http://localhost:8081.
- A script or tool that changes data through http://localhost:8081 gets *403 Cross-site request
  refused*: send the header `Origin: http://localhost:8080` with the request.

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
  on Linux `sudo chown 1654 ./backups` (chapter 22).
- **Not enough space**: the estimate needs the archive size plus a small reserve free in the
  backups folder; a restore needs the unpacked size free on the library volume.
- **A restore lists active jobs**: wait for the listed imports, splits, exports or generations,
  or cancel them, then restore again.
- **"Not compatible"**: the backup was made by a newer StemStudio. Update StemStudio first.
- **A previous library exists**: undo or discard it before the next replace restore.

## The library or its search shows something unexpected

- **An item is in the wrong folder, or under *Unknown artist* or *Unknown album***: use
  **Library info** on the song folder or the item to set the names (chapter 15). A name you set
  is never changed by StemStudio; automatic names follow the band info when it is found later.
- **The library says *Sorting your library (n items left)***: StemStudio is filing items that
  were saved before the folders existed, or after a restore of an older backup. Wait a moment;
  it finishes by itself.
- **The search finds nothing**: it needs every word you typed in the same result. Remove a
  word, check the spelling, or look at what the search covers (chapter 15). *guitar* does not
  find bass; use *bass*. A search of more than 100 characters is cut in the box.
- **A folder disappeared**: a folder exists only while it holds an item. Its items were
  deleted or filed elsewhere; search for the song by name.
- **I cannot find the flat list of items**: it is **All items**, at the Advanced level.

## The lyric assistant does not work

- **The buttons are disabled**: the sheet must be saved and its language must be Dutch or
  English. For *unknown* choose the language first (chapter 20).
- ***The assistant works on your own lyrics only***: the sheet matches the lyrics of a track in
  your library. If the text is your own, use **This is my own text**.
- ***Grammar, flow and writing help need the AI model***: the model is switched off in
  **Settings**, Ollama is not running, or the graphics card is busy with another job. The
  spelling check, the syllable counts and the rhyme finder still work. Fix the cause named in
  the pane (chapter 21 explains the backends) and press the button again.
- ***A check is running***: only one assist runs at a time; wait for it or press **Cancel**.
- **Suggestions are outdated**: you changed the text or the language after the check. Check
  again.
- **Nothing happens after pressing a button with Claude selected**: the notice *Your lyrics go
  to Anthropic* waits for **I agree**. Nothing is sent before you agree.
- **Dutch suggestions are weaker than English ones**: the local model is less good in Dutch;
  read every suggestion before you accept it.

## Sections, score or setlists look wrong

- **The section lane says it is still finding the sections, or shows *Try again***: the
  processing service was busy or unavailable. Wait a moment or press **Try again**; restart
  StemStudio when the worker is down (chapter 7).
- **The boundaries are a little off, or the lane says the tempo grid is uncertain**: the song has
  no steady beat. Move the boundaries in **Edit sections** (Advanced).
- **The labels are wrong**: they are a guess from the music. Rename the sections or change their
  kind (Advanced). Your edits stay until you choose **Detect again** (Expert).
- ***The sections were changed in another window***: the newer version is loaded; make your
  changes again.
- **The score panel lists no sheet music**: make sheet music on the song page first (chapter 16).
  Sheet music made from a library item without its song cannot follow the playback.
- **The marked bar runs ahead or behind**: the sheet music was made with a tempo you typed; make
  it again when the song has a reliable beat grid, or follow it by ear (chapter 8).
- **The setlist PDF cannot be made**: *"Add songs first"* means the setlist is empty. When the
  processing service is unavailable, restart StemStudio and try again (chapter 9).

## Stem quality or a handout looks wrong

- **A clean-sounding stem is rated Poor for bleed**: instruments that hold notes together on the
  same beats can read as bleed. Every rating is a rough indication for now (chapter 23); trust your
  ears.
- **A watery stem shows few artefacts**: artefacts are estimated from dropouts only; a watery
  sound is not measured yet.
- **No quality card on a track**: tracks separated into 4 stems get no report.
- **"wait until one is ready"**: at most 3 handouts of one song or setlist are made at a time.
- **Sheet music is missing from a handout**: open the item in the library at Advanced level; it
  lists files that could not be read or were left out because the handout reached its size
  limit (600 pages of sheet music).


- **<kbd>R</kbd> or <kbd>C</kbd> no longer change the speed or the count-in**: they are the
  recording keys now. *Original speed and key* is <kbd>0</kbd>, the count-in
  <kbd>Shift</kbd>+<kbd>K</kbd> (chapter 24).
- **No single-letter key works**: **Single-key shortcuts** is switched off in Preferences.
- **A key cannot be chosen**: the browser, a screen reader or the lyrics editor needs it, or
  another command uses it; StemStudio names that command.
- **The pedal does nothing**: use **Learn left pedal** or **Learn right pedal** when it sends other
  keys than <kbd>PgUp</kbd> and <kbd>PgDn</kbd>, and switch **Pedal mode** on when a button or
  slider has the focus. Pedals never work in a text field.
- **Your own keys are gone**: they are stored in this browser. Another browser, a private window
  or cleared site data starts with the default keys.

## Recording does not work

- **Not on this computer**: open StemStudio as `http://localhost:8080` on the computer it runs
  on. Browsers allow the microphone only there (chapter 11).
- **Blocked**, **No input found** or **In use**: allow the microphone for the site, connect the
  input, or close the other program that holds it, then **Try again**.
- **Browser not supported** or no interface found: use Chrome or Edge. Firefox may refuse
  interfaces that do not run at 44.1 kHz.
- **Record is disabled**: the speed is not 100 %, the song is transposed or the speed trainer
  runs. Press **Set to 100 %**.
- **The take is late or early**: calibrate the latency again, especially when *Your audio setup
  changed* appears. At the Expert level you can shift a single take by up to 200 ms.
- **You hear yourself twice or with an echo**: switch monitoring off and use the direct monitoring
  of your interface, and use headphones.
- **The take is refused as silent, or marked quiet or clipped**: set the gain on the interface so
  the meter moves without reaching clip.
- **A take shows an error**: press **Try again** in the Takes panel.

## Frequently asked questions

**Does StemStudio send my music anywhere?** No. Audio is processed on your computer. Only
the online lookups for cover art and band info (chapter 12) send the title, artist, duration
and ISRC code, and lyrics lookups (chapter 13) the title, artist, album and duration; both
can be switched off. The optional Claude backend for the agent (chapter 21) sends text only,
never audio, and only when you select it.

**Can I separate more than six instruments?** Detail separation (chapter 5) splits drums,
vocals and the remaining instruments further. Several guitars of the same kind cannot be
separated from each other.

**How do I move my library to another computer?** Make a backup (chapter 22), copy the
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
for the check you asked for (chapter 20).

**Does the library create folders on my disk?** No. Artist, album and song folders exist inside
StemStudio only; downloads, exports and backups are not changed (chapter 15).

**Is my practice log or a recording sent anywhere?** No. The practice log is stored in
StemStudio's own database and recordings on the StemStudio server, both on this computer. Takes
are never given to the AI agent and cannot be used as a voice sample (chapters 10 and 11).

**Can I record at a slower speed?** No. Recording works at the original speed and key so the
take lines up with the stems; playing it back works at any speed and key (chapter 11).

**Which browsers are supported?** Chrome and Edge are tested; Firefox and Safari work on a
best-effort basis. For play-along recording use Chrome or Edge.
