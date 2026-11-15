# tools/site: StemStudio website generator

Builds the GitHub Pages site (<https://robertagterhuis.github.io/StemStudio/>) into `site/dist`
(git-ignored) from the page source in `site/src` and the repository itself:

| On the page | Source |
|---|---|
| Feature cards | README "Features" bullets + delivered features of `docs/ROADMAP.md` |
| Feature status timeline (Delivered with date / In development) | `docs/ROADMAP.md` + `docs/STATUS.md` (same rules as the manuals, `tools/docs/lib/status.mjs`) |
| Quick start, GPU text, personal-practice and model-weights notices | README sections, rendered with copy buttons |
| GPU numbers (8.5x) | the reference-song spike row in `docs/STATUS.md` |
| Screens | `site/src/content/site.json` picks light/dark pairs from `e2e/visual/__screenshots__/**` and `docs/design-review/004/`; the visual-test magenta masks are painted over (`lib/png.mjs`) |
| Manuals | `docs/public/*.pdf` (copied) + page counts from `docs/public/build-manifest.json` |
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

`check.mjs` fails when a local link, image or CSS `url()` is missing, an anchor has no target,
anything is loaded from another server or a link leaves for a host outside the allow-list
(GitHub, the open-data attribution sites, localhost), the site's CSS/JS contains a literal colour
or the HTML a `style`/colour attribute, scripts use cookies, or a roadmap feature, ADR or gallery
screen is missing from the page.

`review.mjs` serves `site/dist` under `/StemStudio/` on a free port chosen by the OS (never
8080/8081/9080/9081/9180), runs axe (WCAG 2.2 AA tags) in light and dark at 1440, 390 and 320 px,
fails on horizontal page scroll or console errors, and saves the home page at 1440 and 390 px.

Page rules: no frameworks, no external requests, no cookies, no trackers; a strict CSP (scripts,
styles, fonts and images from the site only); the theme follows the system and can be switched
(choice kept in `localStorage`); the hero animation respects `prefers-reduced-motion`.
