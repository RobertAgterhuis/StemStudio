# Hand-off: audio-ingest / 020 foundational (T018, T019)

Lane WORKER-IO, branch `020-lane-worker-io` (from `ca82a055`).

## What was built

- `handouts/resources.py`: `AllowList` of the files a handout render may read: the packaged
  Archivo WOFF2 plus the job's artwork (library paths matching `ports.ARTWORK_PATTERN`). A URL is
  served only when it is a plain `file:` URL (no host, query or fragment) naming an allowed real
  path exactly; the file must still be a regular file at the same real path when read (a symlink
  swapped in later is refused) and at most 16 MiB; only `.woff2` and `.jpg` can ever be allowed.
  Notation PDFs are never served to the layout (pypdf merges them, T071).
- `handouts/fetcher.py`: `AllowListFetcher`, a subclass of `weasyprint.urls.URLFetcher`
  (WeasyPrint 70 API, P0-4) with `allowed_protocols=("file",)`, no redirects, 1 s timeout; it
  overrides `fetch(url, headers=None)` and never calls the base `fetch`, so no network handler
  runs. It records `fetched` / `refused` URLs (renderer spy, T072).

## Commits

| Task | Red | Green |
|---|---|---|
| T018 / T019 | 4df361c8 | f4f10a49 (+ complexity fix in 870b8182) |

## Tests and coverage

`tests/handouts/test_fetcher.py` (27 cases). Hostile cases: `http(s)://`, `ftp://`, every
`data:` URI (images too: refused, stricter than "data: beyond images"), `javascript:`, relative
and absolute plain paths, `file:///etc/passwd`, `file://server/share` (UNC on Windows),
`file://localhost/...`, `..` traversal (plain and `%2E%2E`), query/fragment, a symlink swapped in
after the allow-list was built, a symlinked job file, oversized and missing files, unknown types,
non-artwork job files. With WeasyPrint: CSS `@import`, `@font-face url()`, `background url()`,
`<link>`, `<img>`, `<iframe>`, `<object>` with remote, `data:` and foreign `file:` URLs: only the
allowed artwork is fetched.

| Run | Result | Line | Branch |
|---|---|---|---|
| Linux worker image (Pango 1.56.3, WeasyPrint 70.0), `--cov=stemstudio_worker.handouts` | 27 passed | 100 % (78) | 100 % (10) |
| Windows host (no Pango) | 22 passed, 5 skipped (`needs_weasyprint`) | - | - |

The Windows host cannot import WeasyPrint (no Pango); as with libcairo in 008, the tests that need
it are marked `needs_weasyprint` and run in the Linux image and CI. Coverage numbers of `handouts`
are measured in the Linux image (`wio020/worker:test`, a local tag of `e2e020/worker:pre020`, with
the pinned WeasyPrint 70 wheels on `PYTHONPATH`).

## Quality checks

`ruff check`, `ruff format --check`, `mypy --strict`: clean. `run_all.py`: PASS (see
stem-separation foundational); `handouts` is "not in the dependency map" (warning; quality-gate
T009/T010 adds it).

## Findings for other lanes

- **devops (T012)**: WeasyPrint 70 warns at import in the current image: "HarfBuzz-Subset will be
  required by future versions of WeasyPrint". Install `libharfbuzz-subset0` next to
  `libpango-1.0-0` / `libpangoft2-1.0-0`.

## Dependencies

None new (WeasyPrint 70.0 pinned by the architect).

## Deviations

None. Decision: all `data:` URIs refused (no data images are needed; attack surface removed).
