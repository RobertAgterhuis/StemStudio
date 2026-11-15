# Hand-off: frontend / GitHub Pages website

## What was built

A static website for <https://robertagterhuis.github.io/StemStudio/>, generated from the repository
on every push to `main`:

- `site/src/` page source: `index.html` (hero with the animated "one grey strand splits into six
  stem colours" signature, architecture diagram as inline SVG, placeholders for generated
  sections), `css/site-tokens.css` (maps every colour onto the app's `--ss-*` tokens),
  `css/site.css`, `js/theme.js` (theme before first paint), `js/site.js` (theme switch, copy
  buttons), `content/site.json` (gallery picks and the two feature descriptions that have no other
  source).
- `tools/site/` generator: `build.mjs` (writes `site/dist`), `check.mjs` (build + validation),
  `review.mjs` (axe + screenshots), `lib/sources.mjs` (README, ROADMAP, STATUS, ADRs, licences,
  manuals), `lib/render.mjs` (section HTML), `lib/md.mjs` (markdown-it from tools/docs, GitHub link
  rewriting, copy-button code blocks), `lib/png.mjs` (paints over the visual-test magenta masks),
  `lib/serve.mjs` (static server under `/StemStudio/`, OS-chosen port, never 8080/8081/9080/9081/9180),
  `test/site.test.mjs` (node:test).
- `.github/workflows/pages.yml`: push to `main` + `workflow_dispatch`; build job (setup-node,
  `npm ci` in tools/docs, unit tests, `check.mjs`, configure-pages, upload-pages-artifact `site/dist`),
  deploy job (deploy-pages, `pages: write`, `id-token: write`, environment `github-pages`);
  concurrency group `pages` without cancelling a running deployment. Actions pinned to SHAs:
  configure-pages v6.0.0 `45bfe019…`, upload-pages-artifact v5.0.0 `fc324d35…`, deploy-pages v5.0.1
  `368f8252…` (released 2026-03-25, 2026-04-10, 2026-09-01).
- Wiring: `scripts/test-all.{ps1,sh}` steps "site npm ci (tools/docs markdown-it)", "site unit
  tests", "site build + check"; `ci.yml` quality job step "Website builds from the repository sources".
- README links the website (badge + line) and lists the generator under quality gates.

How it stays current: every section except the hero copy, the architecture diagram and the
privacy text is read at build time (feature cards and quick start from README, Delivered/In
development with dates from ROADMAP + STATUS via `tools/docs/lib/status.mjs`, 8.5x from the STATUS
spike row, ADR titles/status from `docs/decisions`, licence groups from THIRD-PARTY-LICENSES, PDFs
and page counts from `docs/public`, screenshots from `e2e/visual/__screenshots__` and
`docs/design-review/004`, commit SHA/date in the footer). A source that changes shape fails the
build instead of dropping a section.

## Files changed

`site/src/**`, `tools/site/**`, `.github/workflows/pages.yml`, `.github/workflows/ci.yml`,
`scripts/test-all.ps1`, `scripts/test-all.sh`, `README.md`, `docs/THIRD-PARTY-LICENSES.md`
(Documentation build: website note + Pages actions), `docs/design-review/site/*.png`, this note.

## Tests and coverage

- `node --test tools/site/test/site.test.mjs`: 6/6 passed. Coverage (`--experimental-test-coverage`):
  png.mjs 100 % line / 93.0 % branch, md.mjs 86.6 / 92.3, sources.mjs 76.4 / 100 (the file-reading
  helpers are exercised end to end by `check.mjs`, not by unit tests).
- `node tools/site/check.mjs`: passes (23 files; links, anchors, assets, CSS url() resolve; no
  external resources; no literal colours; all 11 roadmap features, 20 ADRs and 12 screenshots present).
  Mutation check: a `#fff`/`white` added to site.css is reported.
- `node tools/site/review.mjs`: axe (wcag2a/aa, 21a/aa, 22aa) **0 violations** in light and dark at
  1440, 390 and 320 px; no horizontal page scroll; no console errors.
- Screenshots: `docs/design-review/site/home-1440-light.png`, `home-1440-dark.png`,
  `home-390-light.png`, `home-390-dark.png` (reviewed).
- actionlint 1.7.12 (Docker image `rhysd/actionlint:1.7.12`) on pages.yml and ci.yml: clean.

## Quality checks

`tools/site` is outside the SIG gate's default roots and `.mjs` is not scanned by lizard; a manual
lizard run on copies (as `.js`) shows every unit within the hard limits (max 26 NLOC `sections`,
max CCN 8, max 4 parameters).

## Dependencies added

None. The generator reuses tools/docs' markdown-it 15.0.2 (MIT); the review script reuses e2e's
`@playwright/test` 1.65.1 (Apache-2.0) and `@axe-core/playwright` 4.14.0 (MPL-2.0). GitHub Actions:
configure-pages 6.0.0, upload-pages-artifact 5.0.0, deploy-pages 5.0.1 (all MIT).

## Docs impact

None for the manuals (no app behaviour change). The README links the website.

## Open issues / requests

- Screenshots from the visual baselines use fixture data (hash titles, blue placeholder covers);
  the Tracks and Mixer shots come from the 004 design review with real titles. Fresh owner
  screenshots of the real stack would look better; swap them in `site/src/content/site.json`.
- `docs/THIRD-PARTY-LICENSES.md`: the `nvidia-ml-py` row sits under "Detail separation model
  weights" (after the "Not covered" paragraph), so the site counts it in that group. CTO to move it.
- The theme defaults to dark without JavaScript (tokens.css switches on `html[data-theme]` only).

## Next owner

CTO: publish with `scripts/publish-github.sh`; the first push to `main` runs `pages.yml`.
