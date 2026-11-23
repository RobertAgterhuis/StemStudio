# Hand-off: audio-ingest / 020 polish (T104)

Lane WORKER-IO, branch `020-lane-worker-io`.

## SC-004 timing (T104)

Measured in the worker image (`e2e020/worker:pre020`, Python 3.12, Pango 1.56.3, with the
pinned WeasyPrint 70.0 wheels; the rebuilt 020 image of devops T012 has the same libraries),
script `.claude/scratchpad/020-round/worker-io/exp/timing.py`, one run each:

| Case | Pages | Time | Budget |
|---|---|---|---|
| Song handout, every ingredient, 2 notation files (4 + 1 pages) | 7 | **0.67 s** | 15 s |
| 20-song setlist handout, every song with the same 2 notation files | 123 | **2.98 s** (336 KiB) | 90 s |

Both well within budget (P0-4 spike: 0.9 s and 8.0 s). The default suite also asserts the
20-song budget (`test_a_20_song_setlist_renders_within_the_budget`, 2.0 s without notation).

## Final checks (whole lane)

- Worker default suite (Windows host): 3983 passed, 27 skipped, 12 deselected; package 99 %
  (the misses are `handouts/fetcher.py` and `handouts/layout.py`, which need Pango).
- Linux image: `tests/handouts` + input guard 98 passed, `handouts` 100 % line / 100 % branch.
- `ruff check`, `ruff format --check`, `mypy --strict src tests scripts` (511 files): clean.
- `tools/quality/run_all.py` (jobs, handouts, api, main + tests): PASS, 0 errors; jscpd output
  read: no production clones; remaining warnings are soft complexity (6-7) in `notation._open`,
  `notation._checked_sizes`, `glyphs.missing_characters`, `document.setlist_view`,
  `input_guard.resolve_contained`, and route-test clones like the 014 ones.
- `worker/scripts/sync_handout_style.py --check`: assets untouched (no edits under `assets/`).

## Docs impact (for T105)

Help/user manual: handouts print the fixed notice on every page, missing characters are listed
as a warning, unreadable notation files become a note page, notation on another paper size is
scaled. Technical: renderer reads only listed files, no network; WeasyPrint needs Pango
(`libpango-1.0-0`, `libpangoft2-1.0-0`, plus `libharfbuzz-subset0`) and DejaVu Sans.

## Security fixes (review of the handout renderer)

Each finding red -> green, test first. Evidence from the review probes
(`.claude/scratchpad/020-round/secreview/`), rerun after the fixes on the Windows host.

| # | Finding | Red / green | Fix | Evidence |
|---|---|---|---|---|
| 2 (MEDIUM) | pypdf `LimitReachedError` at the merge failed the whole handout | `a1cc6ba6` / `0f32b081` | the check parses every content stream; any parser error is `notation-unreadable`, the job completes; the merge maps `PyPdfError` to the fixed failure text | API test: a 2.1 M-token notation PDF -> completed job with `notation-unreadable` |
| 1 (HIGH) | check decoded but did not parse; merge of 10 pages x 1.8 M tokens took 373 s / 1.8 GB | `cb9b9597` / `05517e4a` (+ `8e182aa3` typing) | `notation.PARSE_LIMITS` (pypdf `Configuration`): 200 000 tokens per content stream, 16 MiB decoded per stream (zlib, LZW, run-length, Brotli), 64 MiB decoding work; applied to check and merge | `bomb_n.pdf` (10 x 1.8 M tokens, 54 KiB) unreadable after 0.26 s; `bomb.pdf` (70 MB inflate) after 0.02 s; tests: 3 x 1.5 M-token pages < 3 s, a render with 2 such pages < 3 s with the warning |
| 1 (HIGH) | no per-job cap, same path re-read, no wall clock | `b51a2cb2` / `269d3dcf` | `renderer.RenderLimits`: page sizes cached per path per job; at most 600 frame pages and 128 MiB of distinct notation files per job (further items are notes with the existing `notation-unreadable` warning); deadline 240 s checked before every notation file and at every progress step (fails with "The handout could not be written"); the merge goes source by source, one reader at a time | unit tests with small limits and a fake clock |
| 3 (MEDIUM) | no job-level size limits, no body limit | `53977aa0` / `44b368bf`, budget test `20cad721` | `api.body_limit.BodyLimit` (ASGI, declared or chunked) on `POST /v1/handout-jobs`; `jobs.handout_limits` checked in `HandoutService.create`; both answer 422 `ValidationFailed` (`errors.body` / `errors.document`) | a 50-song setlist at 96 % of the text and cell limits (chords over lyrics, the costliest shape) renders in 35 s in the worker image (SC-004: 90 s) |
| 4 (LOW) | 300-char song title -> 300-char bookmark, result invalid after the render | `6f8e6b88` / `d917c84b` | `pdf_facts` shortens titles to 200 characters (199 + ellipsis) | Linux test with a 300-character title validates `HandoutResult` |
| 5 (LOW) | merged pages carried any object hung from the resources | `781c0cab` / `b2fd33ed` | `handouts/pdf_resources.py`: resource categories only, drawing keys only in every dictionary below them (names inside a category and Type 3 `/CharProcs` stay); at most 50 000 objects per page, else unreadable | probe: no `/JavaScript`, embedded file, attachment, URI or open action in the merged PDF; real Verovio/CairoSVG and WeasyPrint pages lose no key |
| 6 (accepted TOCTOU) | artwork JPEG size not bounded | `b0cc76d0` / `f26c5c99` | `resources.artwork_fits`: Pillow reads the header; over 40 MP or no JPEG -> artwork left out | tests with forged SOF headers (48 MP, 4.2 GP) and a non-JPEG |

Refactor: `ba9ecfb2` (complexity of the limit counters).

### Limits chosen (backend lane: please mirror in `ContractLimits`)

| Limit | Value | Where | Over it |
|---|---|---|---|
| `POST /v1/handout-jobs` body | 8 MiB (8 388 608 bytes) | `api.body_limit`, `jobs.handout_limits.MAX_HANDOUT_BODY_BYTES` | 422, `errors.body` |
| Text of the document (every string) | 500 000 characters | `MAX_HANDOUT_TEXT_CHARS` | 422, `errors.document` |
| Layout cells: sections + bars + chords in bars + legend chords + lyric lines + lyric chords + setlist entries | 30 000 | `MAX_HANDOUT_CELLS` | 422, `errors.document` |
| Distinct notation PDFs together | 128 MiB | `MAX_HANDOUT_NOTATION_BYTES` (create) and `RenderLimits.notation_bytes` (run) | 422 at create; a note + `notation-unreadable` at run |
| Frame pages per job | 600 | `renderer.MAX_NOTATION_FRAMES` | a note + `notation-unreadable` |
| Tokens per content stream | 200 000 | `notation.PARSE_LIMITS` | file unreadable |
| Objects below one page's resources | 50 000 | `pdf_resources.MAX_RESOURCE_OBJECTS` | file unreadable |
| Render wall clock | 240 s | `renderer.RENDER_DEADLINE_SECONDS` | job failed, "The handout could not be written" |
| Artwork | 40 MP, JPEG | `resources.MAX_ARTWORK_PIXELS` | artwork left out, no warning |

Measured: a dense engraved page (Verovio + CairoSVG, 16ths in chords) has about 72 k tokens
and merges in about 0.25 s; a 50-song request at the text and cell limits renders in 35 s.

### Needs a contract or backend decision

- Contract text (architect): `POST /v1/handout-jobs` should name the job limits and the 8 MiB
  body limit; they answer the existing 422 `ValidationFailed` (no 413 was added, so the
  contract's response list is unchanged).
- Items over the frame or byte cap reuse `notation-unreadable` ("A notation file could not be
  read: <title>"); a separate code (e.g. `notation-omitted`) would need a contract change.
- Oversized artwork is left out silently: the contract has no artwork warning code.
- Backend: mirror the four request limits above before it sends a job, so users get a readable
  message instead of a 422 from the worker.

### Checks after the fixes

- Worker default suite (Windows host): 4032 passed, 29 skipped, 12 deselected; package 99 %
  (misses: `handouts/fetcher.py`, `handouts/layout.py`, two renderer lines, all Pango-only, and
  the symlink branch of `input_guard`). Job layer `jobs/handout_*`, `api/handout_routes.py`,
  `api/body_limit.py`, `api/problems.py`: 100 % line and branch.
- Linux image (`wio020/worker:test`): `tests/handouts` + limits + input guard 137 passed,
  `handouts/**` 100 % line / 100 % branch.
- `ruff check`, `ruff format --check`, `mypy --strict src tests scripts` (517 files): clean.
- `tools/quality/run_all.py` (jobs, handouts, api, main + their tests): PASS, 0 errors; jscpd
  output read: no production clones. New soft warnings: complexity 6 in `pdf_resources._child`,
  `body_limit._read_within`, `handout_limits._chars`; unit size of the test fixture `_hooked`.
- New dependencies: none (Pillow and pypdf were already direct dependencies).
