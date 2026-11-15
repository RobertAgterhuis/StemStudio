# Keeping the documentation current

## The rule

The constitution (version 1.3.1, Governance) states:

> Every feature that changes user-visible behaviour or technical setup MUST update docs/public
> sources (user and/or technical manual) and regenerate the PDFs before its integration wave
> closes.

The same rule is part of `docs/AGENT-PROTOCOL.md`, and every open feature's `tasks.md` has a
CTO task "Update docs/public manuals and regenerate PDFs" in its integration wave. When a
feature is delivered, its chapters lose the *In development* marker and the feature status
table switches to *Delivered* automatically once `docs/STATUS.md` records the feature.

## Sources

```
docs/public/
  src/user-manual/*.md          chapters, in file-name order, plus manual.json
  src/technical-manual/*.md     chapters, in file-name order, plus manual.json
  StemStudio-User-Manual.pdf
  StemStudio-Technical-Manual.pdf
  build-manifest.json           SHA-256 of every input, page counts
tools/docs/
  build.mjs, check.mjs, lib/    the build and the check
  styles/                       print style, token-based
```

Each chapter starts with one `#` heading; `##` headings are numbered and listed in the
contents. An HTML comment containing the word `feature-status` (see `lib/status.mjs`) inserts
the generated feature table.
Screenshots are referenced with relative paths, for example to `docs/design-review/004/` or
the Playwright visual baselines, and become part of the build inputs automatically.

## Style

The PDFs use the 004 design system: the light "day session" theme, Archivo (self-hosted from
the web app) with its width axis, the six stem colours and the "one strand becomes six"
signature on the cover and chapter openers. The build loads the app's own
`src/StemStudio.Web/wwwroot/css/tokens.css`; `tools/docs/styles/doc-tokens.css` only maps
print roles onto those tokens. No style sheet of the docs may contain a literal colour.

## Regenerating the PDFs

```
cd tools/docs
npm ci
npm run browser     # once: installs Playwright's Chromium
npm run build       # writes both PDFs and build-manifest.json
```

Commit the changed sources, both PDFs and the manifest together.

## The check

`node tools/docs/check.mjs` (`npm run check`) needs no installed packages. It fails when the
SHA-256 of the current inputs differs from `build-manifest.json` (and lists the changed
files), when a PDF is missing, or when a docs style sheet contains a raw colour. The inputs are
all Markdown sources, the referenced images, the app tokens, font and logo, `docs/ROADMAP.md`,
`docs/STATUS.md` and the build code. The check runs in `scripts/test-all.ps1`,
`scripts/test-all.sh` and as a step of the CI quality job, so a feature cannot close with
stale manuals.
