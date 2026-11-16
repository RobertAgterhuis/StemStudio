# tools/site: StemStudio website generator

Builds the GitHub Pages site (<https://robertagterhuis.github.io/StemStudio/>) into `site/dist`
(git-ignored) from the page source in `site/src` and the repository itself:

Two pages:

- `index.html` (template `site/src/index.html`): the home page for musicians. Its text lives in
  `site/src/i18n/<lang>.json` (`{{t.key}}` placeholders); `site/COPY-DECK.md` is the readable copy
  deck with sources. A Dutch page is added as `nl.json` plus `"nl"` in `site.json` `locales`
  (built into `nl/index.html`; `check.mjs` fails until its keys match English).
- `developers.html` (template `site/src/developers.html`): architecture, tech stack, all
  features, quick-start variants, GPU, feature status, manuals, licences, ADRs, full notice.
  Old home anchors that moved here (`#how`, `#status`, `#decisions`, `#licences`, listed in
  `site.json` `movedAnchors`) are redirected by `site.js`.

| On the page | Source |
|---|---|
| Home: start commands, GPU race numbers, manual size | README "Quick start" code blocks, the reference-song spike row in `docs/STATUS.md`, `docs/public/build-manifest.json` |
| Home: feature themes | `site.json` `featureMap` places every README "Features" bullet in a home section |
| Home: Coming soon | `site.json` `comingSoon` (roadmap ids); the build fails once one is delivered in STATUS |
| Home: screens | `site/src/content/site.json` picks light/dark pairs (optionally cropped) from `e2e/visual/__screenshots__/**` and `docs/design-review/004/`; the visual-test magenta masks are painted over (`lib/png.mjs`) |
| Developers: feature cards | README "Features" bullets + delivered features of `docs/ROADMAP.md` |
| Developers: feature status timeline | `docs/ROADMAP.md` + `docs/STATUS.md` (same rules as the manuals, `tools/docs/lib/status.mjs`) |
| Developers: quick start, GPU text, notices | README sections, rendered with copy buttons |
| Manuals | `docs/public/*.pdf` (copied to `manuals/`, URLs unchanged) + page counts from `docs/public/build-manifest.json` |
| Design decisions | `docs/decisions/ADR-*.md` (title + status) |
| Licences | `docs/THIRD-PARTY-LICENSES.md` (component count per group) |
| Colours, font, logo | the app's `tokens.css`, Archivo variable font and `favicon.svg`, copied unchanged |

Because everything is read at build time, the Pages workflow (`.github/workflows/pages.yml`, on
every push to `main`) keeps the site current without manual edits.

```bash
(cd tools/docs && npm ci)                    # markdown-it (shared with the manuals build)
node --test tools/site/test/site.test.mjs    # unit tests of the helpers
node tools/site/build.mjs                    # build site/dist
node tools/site/check.mjs                    # build + validate (test-all and CI)
(cd e2e && npm ci) && node tools/site/review.mjs   # axe + screenshots into docs/design-review/site
```

`check.mjs` fails when a local link, image or CSS `url()` is missing, an anchor has no target (also
across pages), anything is loaded from another server or a link leaves for a host outside the
allow-list (GitHub, Docker's install guide, the open-data attribution sites, localhost), the site's
CSS/JS contains a literal colour or the HTML a `style`/colour attribute, scripts use cookies, a
roadmap feature or ADR is missing from the developer page, a gallery screen, README feature or
kept anchor (`#quick-start`, `#start`, `#features`, ...) is missing from the home page, a
coming-soon feature is already delivered, the home page uses sales or hype words, or a locale's
keys differ from English.

`review.mjs` serves `site/dist` under `/StemStudio/` on a free port chosen by the OS (never
8080/8081/9080/9081/9180), runs axe (WCAG 2.2 AA tags) on both pages in light and dark at 1440,
390 and 320 px, fails on horizontal page scroll or console errors, and saves both pages at 1440
and 390 px.

Page rules: no frameworks, no external requests, no cookies, no trackers; a strict CSP (scripts,
styles, fonts and images from the site only); the theme follows the system and can be switched
(choice kept in `localStorage`, cross-fade through View Transitions). Motion: vanilla CSS and JS
only, transform/opacity (and one canvas), every loop on one beat (`--site-beat`, 96 BPM), paused
off screen; `prefers-reduced-motion: reduce` shows every end state and nothing loops.
