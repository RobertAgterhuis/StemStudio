# 005 Harden the worker's ffmpeg/ffprobe intake against disguised playlists
- Status: proposed · Date: 2026-11-19 · Area: worker / ingest (upload + download normalisation) · Priority: P1

## Problem statement
Every user upload and every yt-dlp download is decoded by `FfmpegNormalizer`
(`worker/src/stemstudio_worker/ingest/normalizer.py`), called from `jobs/runner.py:129`. The upload is
accepted on its **file extension only** (`ingest/upload.py:21` `ALLOWED_EXTENSIONS = {"mp3","wav","flac"}`,
checked at `upload.py:62-63`); its content is never identified. ffprobe and ffmpeg are then started with
format auto-detection and no protocol restriction:

- `normalizer.py:132-134` (`_probe`): `ffprobe -v error -select_streams a:0 ... file:<path>` — no `-f`, no
  `-protocol_whitelist`, no `-format_whitelist`.
- `normalizer.py:140-145` (`_convert`): `ffmpeg -nostdin ... -i file:<path> ...` — same.

The `file:` prefix (`normalizer.py:156-158`) only stops the *name* from being read as an option or protocol.
It does not stop the *content* from selecting a demuxer: a text file named `x.mp3` that starts with
`#EXTM3U` (HLS), `ffconcat version 1.0` or a DASH/ SDP manifest is probed as that format, and those demuxers
open further URLs (other local files, or network URLs, depending on the ffmpeg build and demuxer defaults).

The same team already identified and closed exactly this attack in the generator:
`generator/src/stemstudio_generator/audio/samples.py:3-12` ("playlists such as HLS `.m3u8`, ffconcat or DASH
manifests disguised with an audio extension ... never reach ffmpeg"), with magic-byte identification and
`-protocol_whitelist file -f <demuxer>` at `samples.py:125-128`. Evidence that the worker has no equivalent:
`grep -rn "protocol_whitelist|format_whitelist" worker/src` finds nothing; `grep -rln "EXTM3U|m3u8|ffconcat"
worker/` finds no test.

Minor, same theme (generator): ids are checked with `re.compile(r"^[0-9a-f]{32}$").match(...)`
(`generator/src/stemstudio_generator/api/routes.py:28,79,87,150`, `api/voice_samples.py:42,73`). `$` also
matches before a trailing newline (`python -c "...match('a'*32+'\n')"` prints `True`), so `<id>%0A` passes the
guard. Today the lookup then misses, so it is not exploitable, but it is not the full-match the code intends;
the worker uses `fullmatch` everywhere (`jobs/json_store.py:61`).

## Impact
- Security exposure on the main user-facing intake: a crafted upload can make the worker's ffmpeg read other
  files inside the container (data directory, other users' cached stems) or reach out to network hosts
  (SSRF from the worker's network position) — the very class of issue the generator's security review closed.
- The worker processes untrusted media from two sources (uploads, YouTube downloads), so the exposure is on
  the most-used path, not an optional service.

## Risk
- Leaving it: medium likelihood (well-known ffmpeg HLS/concat tricks, public PoCs), high impact on a
  multi-tenant data directory.
- The change: low to medium. Too strict a `-format_whitelist` could refuse a container yt-dlp delivers today
  (webm/opus, m4a/mp4). Mitigated by deriving the list from what the fetcher actually produces and by the
  characterisation tests below. Rollback is a one-commit revert.

## Effort
S (1–2 days): one module, plus tests. Assumes the generator's magic-byte approach (`samples.py`) can be
reused as is or lifted into a shared helper (see the shared-runtime proposal).

## Step-by-step plan
1. Safety net: characterisation tests in `worker/tests/ingest/test_normalizer.py` that pin the current
   argument lists, and new failing tests: an upload named `*.mp3` whose content is `#EXTM3U` with a
   `file:`/`http:` entry, one with `ffconcat version 1.0`, and one with a DASH manifest; each must end in
   `AudioDecodeError` without ffmpeg being started (assert on the fake `CommandRunner`). Add one
   `@pytest.mark.integration` test with the real ffmpeg.
2. Identify the container by magic bytes before probing (RIFF/WAVE, fLaC, ID3/MPEG frame sync for uploads;
   plus EBML/webm, `ftyp`/mp4, OggS for downloads). Reject anything else with `DECODE_FAILED_MESSAGE`.
3. Pass the identified demuxer explicitly (`-f wav|flac|mp3|matroska|mov|ogg`) to both ffprobe and ffmpeg, and
   add `-protocol_whitelist file` to both invocations (ffprobe accepts the same option).
4. Optionally also add `-format_whitelist` with the same set as defence in depth.
5. Generator: replace `_ID.match` with `_ID.fullmatch` (and drop `^...$`) in `api/routes.py` and
   `api/voice_samples.py`; add a test with a trailing `\n` id.
6. Verify: `pytest worker/tests/ingest` and the generator API tests green; run one real YouTube and one real
   upload end-to-end (integration marker). Roll back by reverting the commit.

## Dependencies and related proposals
- Related: 021-shared-runtime-package (the container sniffing and `CommandRunner` would live there).
