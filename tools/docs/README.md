# tools/docs: public manuals build

Builds `docs/public/StemStudio-User-Manual.pdf` and `docs/public/StemStudio-Technical-Manual.pdf`
from the Markdown chapters in `docs/public/src/<manual>/` and writes
`docs/public/build-manifest.json` (SHA-256 of every input).

```
cd tools/docs
npm ci
npm run browser   # once: Playwright's Chromium headless shell
npm run build
```

`node tools/docs/check.mjs` (no install needed) fails when the inputs no longer match the
manifest, a PDF is missing, or a style sheet in `styles/` contains a raw colour. It runs in
`scripts/test-all.*` and in the CI quality job (constitution 1.3.1).

- `build.mjs`: renders every manual (cover, contents with page numbers, chapters).
- `lib/inputs.mjs`: the input list and hashing (shared with `check.mjs`; no dependencies).
- `lib/status.mjs`: the feature status table from `docs/ROADMAP.md` and `docs/STATUS.md`.
- `lib/markdown.mjs`, `lib/html.mjs`, `lib/pdf.mjs`: Markdown, templates, printing.
- `styles/doc-tokens.css`, `styles/print.css`: print style on top of the app's
  `src/StemStudio.Web/wwwroot/css/tokens.css` (light theme); no literal colours.

Page numbers in the footer count from the contents page; the cover is unnumbered.
