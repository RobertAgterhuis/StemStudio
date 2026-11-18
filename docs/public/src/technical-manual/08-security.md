# Security

StemStudio is a single-user application for one computer. Its threat model assumes a trusted
local user and untrusted **input**: links, uploaded files, data from online sources and, from
feature 011, text that a language model reads or writes.

## Network exposure

- Only the web app (8080) and the API (8081) have host ports. The worker, generator and
  singer are reachable only on the compose network.
- There is no authentication. **Do not expose the ports to the internet** or to an untrusted
  network.
- The API's CORS policy allows only `GET` from the configured web origin (stem streaming).

## Input validation

- Import URLs must fully match the allowed YouTube and Spotify track patterns
  (`source-url-vectors.json`); control characters are rejected.
- Uploads are limited by extension and size (200 MiB) and stored under content hashes.
- Subprocesses (ffmpeg, yt-dlp) receive argument lists, never a shell. Server-side paths are
  derived from GUIDs and hashes, so user input never forms a path.
- From 005: outbound HTTP only to allow-listed hosts over HTTPS, at most three re-validated
  redirects and a 10 MB cap; images are decoded with a 40-megapixel cap and stripped of
  metadata.
- From 007: render inputs must match an allow-list of library paths and resolve inside the
  library root; the worker mounts the API data read-only.

## Secrets

The core product has no secrets. The only secret is the optional Claude key of the agent
(chapter 3): from `ANTHROPIC_API_KEY` (never in files or command lines) or entered in Settings and
stored encrypted with Data Protection; never logged, returned or stored in plain text.

### Threat model of the stored Claude key (ADR-035)

| Threat | Protected | How |
|---|---|---|
| Copy of the SQLite database | yes | ciphertext and last four only; the key ring is in another volume or directory |
| Backup archive or export (013) | yes | contains the ciphertext, never the key ring |
| The `api-data` volume (also the worker's read-only `/library` mount) | yes | `api-keys` is mounted only into `api` |
| Logs, problem details, SSE events, screenshots, password managers | yes | write-only API, fixed problem texts, HTTP logging off for the route, `autocomplete="off"`, the page never renders the value |
| A malicious web page in the owner's browser (CSRF) | yes | `PUT`/`DELETE` need a CORS preflight; the API allows only `GET` from the web origin (pinned by a test) |
| Another LAN device before feature 018 | partly | it can never read the key; direct API calls and forwarded headers are refused (peer must be loopback or the `web` service); through the Web page a forged host name can overwrite or delete the stored key. Accepted until 018 binds the ports to `127.0.0.1` |
| Full host access (admin/root, Docker Desktop's VM disk, a disk image, malware running as the owner) | **no** | with the database and the key ring the key can be decrypted; on Linux the ring is not encrypted at rest; DPAPI does not stop malware running as the Windows user |
| Memory of the running API or Web process | **no** | the key is in memory while a Claude client exists and briefly in the Blazor circuit during entry |
| Lost key volume, restore on another computer | availability only | `stored-unreadable` with "Enter the key again" |

Key writes (`PUT`/`DELETE /api/agent/settings/claude-key`) and the Claude **Test key** are
accepted only from loopback or the `web` service with a loopback host name and no foreign
`Origin`; the Web page enables the controls only when opened as `localhost`, `127.0.0.1` or
`[::1]`. After a leak, revoke the key in the Anthropic console first, then replace it in
Settings.

## Agent safety

Safety is enforced in code, not left to the model (research finding: a small local model
obeyed an injected "delete" instruction):

- The tool registry is the allow-list (at most 32 tools per request). There are no shell,
  file, URL-fetch, raw HTTP, SQL or settings tools.
- Every tool has a fixed risk class. Heavy and destructive actions always need approval;
  approvals are bound to a hash of the exact arguments and expire after 24 hours.
- External text (track titles from YouTube or Spotify, lyrics, biographies, help) reaches the
  model only in `untrusted_text` fields and **taints** the run; after that every change or job
  needs approval (owner decision: titles taint, so a change after a library search always
  asks). URLs and lyrics must literally come from the user's own messages, and an import URL
  still has to pass the API's YouTube/Spotify allow-list.
- When the size of a job cannot be checked beforehand, the step is treated as heavy and the
  card says so (`risk-check-failed`). Artist-imitation arguments are refused before any card
  (`artist-imitation`), with the same lexicon as 009.
- Tool results are capped at 4 KB and never contain lyrics, biographies, paths or secrets.
- Per run: 12 model turns, 20 tool calls, 3 invalid calls; Claude budget 60,000 input and
  8,000 output tokens.
- In the library, `Agent/Policy` may reference only `Agent.Model`, `Agent.Tools` and the
  data-only `Agent.Resources` (quality-gate rule); the Agent never references Domain, so the
  imitation and lyrics-provenance checks are mirrors of the Domain rules (`MIRRORS`, owner
  decision 2026-11-16).
- Evaluation of the real model (`tools/agent-eval`, chapter 9): the injection cases 22-24 pass;
  in no run did a non-read call execute without approval after the run was tainted.

## Backup archives (013)

Restores treat every archive as hostile until it has passed the validation order of
data-model 3.4: file name pattern and no link; ZIP limits (2,000,000 entries, manifest at most
256 MiB, format major 1); per entry the layout rules (no `..`, absolute or drive paths, links,
duplicates, unlisted entries); known migrations; free space; extraction only through
`GetFullPath` containment into the staging folder with size-bounded, hashed writes (never
`ExtractToDirectory`/`ExtractToFile`, enforced by the quality gate); then the staged database
(integrity, migration history, no triggers or views, stored paths inside the layout) and the
consent proofs. Nothing outside staging changes before all checks pass; the hostile corpus
H1-H17 (`tests/fixtures/backup/archives`) proves it, and Verify runs the same read-only checks.
Archives never contain the Data Protection key ring, configuration or environment values; a
stored Claude key travels only as ciphertext and needs the original key ring. Archives are not
encrypted: they hold the library and voice samples, so the backups folder must be private to
the user. One library job runs at a time; backups stop below a free-space floor; clean-up plans
expire after 10 minutes and every file is classified again right before it is deleted, so
originals are never removed.

A restore or merge never uses the archived database schema: it builds a fresh database with the
app's own migrations up to the archive's level and copies only row data into it (column lists from
the fresh schema), so no archived trigger, view or index can reach the live library; archived
databases are opened read-only with `trusted_schema=OFF` and SQLite's defensive mode. State-changing
`/api` requests with a foreign `Origin` or `Sec-Fetch-Site: cross-site` are refused (403), so a web
page cannot start an undo or verify in the background. While a restore switches libraries, the API
opens the live database read-write without create mode, so a request during the switch fails
instead of creating an empty database; only start-up creates it (see Operations, "Interrupted
library switch").

## Library names and search (022)

Artist, album and song names come from online lookups, tags in uploaded files and from the user, so
they are **untrusted text**. They are never used in a file path, a SQL statement or markup:

- Names are cut to 200 characters (with "…") and stripped of control characters before they are
  stored; the key is computed from the cut name with regular expressions that have timeouts.
  Folder and item pictures are never built from names.
- A folder id must match `^(ar|al|so)[0-9a-f]{16}$` before it is used, in the
  API, in the Web app (also before it requests a folder) and in the agent loopback, which escapes
  the id into one path segment (`../tracks` stays one segment).
- The search runs in memory over folded strings: no `FromSqlRaw`, no `LIKE`. `q` is limited to 100
  characters and 8 words (`422 library-query-invalid` beyond that), the result size to 200 per group
  and highlights to 32 per item.
- The Web app renders names as text (no `MarkupString`) and accepts a picture URL in folder art only
  when it is an API artwork path (`/api/tracks/...`, no `..`, no spaces, no absolute or
  protocol-relative URL); anything else shows the placeholder.
- `PUT`/`DELETE` of library info are state-changing `/api` requests: a foreign `Origin` or
  `Sec-Fetch-Site: cross-site` gets `403`, and a restore switch answers `503 library-maintenance`.
  Path-, markup- and SQL-like names are stored verbatim as display names and create no file or folder.
- Agent: folder and item names reach the model only as `untrusted_text` and taint the run;
  `set_library_info` is a `change` tool, so a tainted run needs approval, and it has an inverse.

## Lyric assistant (023)

Threat model: the sheet text, the model's answer and the agent's arguments are all untrusted.

- **Model output is data.** The assistant model has no tools. Its answer must validate against the
  closed schema `lyric-assist-output` 1.0.0; every anchor is found again in the real saved text,
  items on markers or blank lines are dropped, and the types, counts and edit distance are limited
  per kind (a correction may change at most 60 % of a line). Proposal (400 characters), explanation
  (300), note (300) and model name (100) are length-capped and at most 400 suggestions are stored per
  assist. Text enters a sheet only through accept, undo or restore of **stored** suggestions of the
  same sheet; a hostile-output test proves it. Explanations are rendered as text.
- **Prompt data is delimited.** The sheet, title, instruction and misses go into the user prompt
  between `<<<NAME boundary>>>` delimiters with 128 random bits per request; a boundary that occurs
  in the data is refused. The system prompt holds only fixed text and trusted values. Rhyme and
  syllable claims are recomputed deterministically; claims that fail are dropped.
- **Own lyrics only.** No endpoint accepts imported (006) track lyrics. Before every assist (and again
  when it runs, after a save in between) a guard compares the sheet with the stored library lyrics:
  a sheet is refused (`not-own-lyrics`) when at least 3 lines of 5 or more words **and** 25 % of its
  lines match, or when 8 lines match. The own-work override covers only the matched track, needs the
  typed statement and has no agent tool; it never disables the next two rules.
- **Verbatim withholding and imitation.** Generated text (proposals and explanations) containing a
  run of 7 or more words or a full line of 5 or more words of library lyrics is withheld and counted
  (lyrics outside the library cannot be detected). Instructions pass the artist-imitation guard
  (lexicon plus library artist names) and are refused (`imitation`), with or without an override.
- **Claude only when selected, after a notice.** The notice (`lyric-claude-notice.v1`) must be
  acknowledged before any model call and is checked again when the assist runs; the backend is pinned
  per assist (a call on another backend is refused, so there is no silent switch to Claude);
  per-assist caps of 20,000 input and 4,000 output tokens are checked before every call, retries
  included. The key is reached only through the key provider. Without Claude **no request leaves
  the API**: a failing HTTP handler in the tests proves it for every assist kind.
- **One assist at a time**, also under concurrent creates (runner admission); the runner waits on the
  library gate. Event streams have no concurrency cap (single-user app, accepted): they close after
  30 minutes without events, send keep-alives, close when the sheet is deleted and bound `after`;
  a reconnect after the terminal event closes at once.
- **Inputs are bounded**: raw text 65,536 characters, instruction 200, 100 ids per request, words of at
  most 40 characters, 200 dictionary misses per check. Problem details never echo sheet text, and no
  sheet text or secret reaches the logs (the runner logs the exception type only).
- **Agent.** Tool results carry ids, line numbers, categories and counts, never text; the sheet title
  is `untrusted_text`; `apply_lyric_suggestions` always needs approval (reason `lyrics-edit`), takes
  only ids of open suggestions of that sheet and shows before and after read from the stored
  suggestions; `find_rhymes` needs a word that is in a user message; the `origin` of an assist
  started through the agent is a log label only.

## Practice flow (014)

Untrusted input: worker results, request bodies and the browser preferences record.

- **Worker results are capped.** Section results: at most 64 sections, labels trimmed and cut
  at 40 characters, unknown kinds become `other`, unknown warning codes and snaps are dropped.
  Score views: 1-200 pages and 1-2,000 bars, else the view fails. Files downloaded from the
  worker are size-capped while they stream: **8 MiB per score SVG page** and **16 MiB per
  setlist PDF** (`WorkerClient.MaxScoreViewPageBytes`, `MaxSetlistDocumentBytes`); a larger PDF
  answers `503 document-failed`.
- **Score pages are inert.** Page paths are built from the asset GUID and an integer page
  number (1 to the page count) and checked for containment in the data folder; pages are written
  atomically and served as `image/svg+xml` under `Content-Security-Policy: default-src 'none';
  style-src 'unsafe-inline'; font-src data:` with `nosniff`, so a script in an SVG cannot run even
  when the page is opened directly. The Web app shows them as images.
- **The score-view job reads only** `assets/<uuid>/score.musicxml` (a new line in the worker's
  library path allow-list); section jobs only the track's stem and detail-stem paths. The
  setlist document request carries its content inline, with no paths.
- **PDF file name**: only letters, digits, space, `-`, `_` and `.`, trimmed, at most 80
  characters (tested with `../`, quotes, CR/LF header injection and Unicode).
- **Practice log is local.** Practice data is stored only in SQLite, is never logged above debug
  level, and no practice endpoint makes an outgoing request (a spy-handler test proves it). The
  log can be switched off (writes then answer `409 practice-log-off`) and deleted per track or
  completely; a track delete removes its sessions.
- **Concurrency**: one lock each around check, submit and save for detection and score views,
  and around find and insert of a practice session; section edits use the `revision` check.
- **Key map**: the browser preferences record is validated field by field in TypeScript and C#
  (at most 16 KB); reserved chords (focus, activation, screen reader and browser keys) and Alt
  chords can never be bound.

## Play-along recording (015)

- **Audio stays on the machine.** The browser sends captured audio only to the local API; the
  capture works only in a secure context (`localhost` or `127.0.0.1` on the StemStudio
  computer), so it never crosses the network in plain HTTP. An E2E network assertion checks that
  recording and saving contact no origin other than the web app and the API. Takes are never
  offered to the agent (no tool accepts `take`), never part of an LLM request, and are
  registered in the audio-hash index, so a take is refused as a voice sample (`422 library-audio`).
- **WAV upload validation.** The two take upload endpoints have their own body limit
  (`StemStudio:Recording:MaxUploadBytes`, default 256 MiB, plus multipart overhead) and stream
  the body: the WAV header is checked **before any data is stored** (RIFF/WAVE, PCM or
  `WAVE_FORMAT_EXTENSIBLE` with PCM, 1-2 channels, 44,100 Hz, 24 bits, `blockAlign` = 3 x
  channels, at most 64 KiB of chunks before `data`, `data` a multiple of the block size), the data
  is spooled to disk without buffering, and the metadata JSON is read with a 64 KiB cap. An
  unreadable file is `400 invalid-wav`, another format `415 unsupported-wav-format`, too large
  `413`; hostile input leaves no file behind. The API scans the samples once (peak, clipped
  samples, all zero) and refuses silent takes, takes outside the track and a length that does not
  match the frames.
- **Paths** of take files come from the asset GUID only; the render job request is a normal 007
  request whose single input `assets/<assetId>/capture.wav` passes the worker's allow-list.
- **Limits**: 200 takes per track, free disk of 1 GiB plus twice the upload size, names 1-80
  characters without control characters, device labels at most 200 characters.
- **Library gate**: deleting, batch deleting and replacing take audio respect the 013 write gate;
  take assets cannot be renamed, deleted or retried through the generic asset endpoints
  (`409 managed-by-take`).

## Privacy

Audio never leaves the machine. Online lookups (005) send only title, artist, duration and
ISRC, LRCLIB lyrics lookups (006) title, artist, album and duration, with a User-Agent that contains no personal data, and can be switched off
with `STEMSTUDIO_ONLINE_LOOKUPS=off`. Lyrics are never logged or included in list responses.
Voice samples (010) stay local, require recorded consent and are deleted with their profile.
The Claude backend receives text only. The lyric assistant (023) sends the text of a sheet to Anthropic only when Claude is the selected backend and the notice was acknowledged; otherwise its dictionaries, syllable counter, rhyme finder and local model run on the computer and nothing leaves it. Library folders (022) send nothing anywhere. The practice log (014) is stored only in the local
database and setlist PDFs are made by the local worker; play-along recordings (015) go only from
the browser to the local API and are stored as library files on this computer.
