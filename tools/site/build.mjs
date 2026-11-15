/**
 * Builds the StemStudio website into site/dist from the repository sources (README, ROADMAP,
 * STATUS, ADRs, licences, manuals, screenshots) and the page source in site/src.
 * Needs tools/docs' markdown-it: `cd tools/docs && npm ci`, then `node tools/site/build.mjs`.
 * The GitHub Pages workflow (.github/workflows/pages.yml) runs this on every push to main.
 * @module
 */
import { copyFileSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { abs } from "../docs/lib/inputs.mjs";
import { REPO_URL, SITE_URL, markdown } from "./lib/md.mjs";
import { cleanScreenshot } from "./lib/png.mjs";
import { adrList, featureCards, gallery, gpuSection, licences, manualTiles, timeline } from "./lib/render.mjs";
import * as src from "./lib/sources.mjs";

/** Output folder (git-ignored through the generic dist/ rule). */
export const DIST = "site/dist";

/** Files copied unchanged: [source, path in dist]. The app's tokens, font and logo are reused as-is. */
const ASSETS = [
  ["src/StemStudio.Web/wwwroot/css/tokens.css", "assets/tokens.css"],
  ["src/StemStudio.Web/wwwroot/fonts/archivo/Archivo-Variable-latin.woff2", "assets/archivo.woff2"],
  ["src/StemStudio.Web/wwwroot/fonts/archivo/OFL.txt", "assets/archivo-OFL.txt"],
  ["src/StemStudio.Web/wwwroot/favicon.svg", "assets/favicon.svg"],
  ["site/src/css/site-tokens.css", "assets/site-tokens.css"],
  ["site/src/css/site.css", "assets/site.css"],
  ["site/src/js/theme.js", "assets/theme.js"],
  ["site/src/js/site.js", "assets/site.js"],
];

/** @param {string} rel path inside dist @returns {string} absolute path (parent folder created) */
function out(rel) {
  const path = abs(`${DIST}/${rel}`);
  mkdirSync(abs(`${DIST}/${rel.split("/").slice(0, -1).join("/")}`), { recursive: true });
  return path;
}

/** @param {Buffer} png PNG bytes @returns {{ width: number, height: number }} size from the IHDR chunk */
const pngSize = (png) => ({ width: png.readUInt32BE(16), height: png.readUInt32BE(20) });

/**
 * Copies the gallery screenshots (visual-test masks painted over) into dist/screens.
 * @param {{ id: string, title: string, caption: string, light: string, dark: string }[]} entries site.json gallery
 * @returns {Parameters<typeof gallery>[0]} gallery items with their published images
 */
function publishScreens(entries) {
  return entries.map((entry) => {
    const images = {};
    for (const theme of ["light", "dark"]) {
      const png = cleanScreenshot(readFileSync(abs(entry[theme])));
      const rel = `screens/${entry.id}-${theme}.png`;
      writeFileSync(out(rel), png);
      images[theme] = { src: rel, ...pngSize(png) };
    }
    return { id: entry.id, title: entry.title, caption: entry.caption, images };
  });
}

/** @param {ReturnType<typeof src.manuals>} list manuals @returns {typeof list} the list, PDFs copied */
function publishManuals(list) {
  list.forEach((m) => copyFileSync(abs(`docs/public/${m.file}`), out(`manuals/${m.file}`)));
  return list;
}

/** @returns {Record<string, string>} placeholder values for site/src/index.html */
function sections() {
  const content = JSON.parse(src.readText("site/src/content/site.json"));
  const readme = src.readText("README.md");
  const status = src.readText("docs/STATUS.md");
  const features = src.roadmapFeatures(src.readText("docs/ROADMAP.md"), status, content.featureFallbacks);
  const quick = src.subsections(src.section(readme, "Quick start"));
  const legal = readme.split("\n## ")[0].match(/^> [\s\S]*?(?=\n\n|\n$)/m)?.[0];
  if (!legal) throw new Error("README personal-practice notice (blockquote before the first section) not found");
  const build = src.buildInfo();
  return {
    siteUrl: SITE_URL,
    repoUrl: REPO_URL,
    features: featureCards(src.boldBullets(src.section(readme, "Features")), features),
    gallery: gallery(publishScreens(content.gallery)),
    quickStart: markdown().render(quick.intro),
    gpu: gpuSection(src.gpuNumbers(status), [...quick.subsections].find(([t]) => t.startsWith("NVIDIA GPU"))?.[1] ?? ""),
    timeline: timeline(features),
    manuals: manualTiles(publishManuals(src.manuals())),
    licences: licences(src.section(readme, "Licences"), src.licenceSections(src.readText("docs/THIRD-PARTY-LICENSES.md"))),
    adrs: adrList(src.adrs()),
    legal: markdown().render(legal),
    sha: build.sha,
    shortSha: build.sha.slice(0, 7),
    date: build.date,
  };
}

/**
 * Builds the whole site.
 * @returns {{ files: number }} summary
 */
export function buildSite() {
  rmSync(abs(DIST), { recursive: true, force: true });
  ASSETS.forEach(([from, to]) => copyFileSync(abs(from), out(to)));
  const values = sections();
  const html = src.readText("site/src/index.html").replace(/\{\{(\w+)\}\}/g, (match, key) => {
    if (!(key in values)) throw new Error(`unknown placeholder ${match} in site/src/index.html`);
    return values[key];
  });
  writeFileSync(out("index.html"), html, "utf8");
  return { files: ASSETS.length + 1 };
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  buildSite();
  console.log(`StemStudio website built into ${DIST}`);
}
