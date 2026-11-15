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

The core product has no secrets. The only secret is the optional `ANTHROPIC_API_KEY` of the
agent (chapter 3): environment only, never in files or command lines, never logged, stored or
returned.

## Agent safety <span class="planned">In development</span>

Safety is enforced in code, not left to the model (research finding: a small local model
obeyed an injected "delete" instruction):

- The tool registry is the allow-list (at most 32 tools per request). There are no shell,
  file, URL-fetch, raw HTTP, SQL or settings tools.
- Every tool has a fixed risk class. Heavy and destructive actions always need approval;
  approvals are bound to a hash of the exact arguments and expire after 24 hours.
- External text (lyrics, biographies, help) reaches the model only in `untrusted_text` fields
  and **taints** the run; after that every change or job needs approval. URLs and lyrics must
  literally come from the user's own messages.
- Tool results are capped at 4 KB and never contain lyrics, biographies, paths or secrets.
- Per run: 12 model turns, 20 tool calls, 3 invalid calls; Claude budget 60,000 input and
  8,000 output tokens.

## Privacy

Audio never leaves the machine. Online lookups (005; lyrics in 006) send only title, artist,
duration and ISRC, with a User-Agent that contains no personal data, and can be switched off
with `STEMSTUDIO_ONLINE_LOOKUPS=off`. Lyrics are never logged or included in list responses.
Voice samples (010) stay local, require recorded consent and are deleted with their profile.
The Claude backend receives text only.
