/**
 * Builds the StemStudio website into site/dist from the repository sources (README, ROADMAP,
 * STATUS, ADRs, licences, manuals, screenshots) and the page sources in site/src:
 * - index.html: the home page for musicians (text from site/src/i18n/<lang>.json);
 * - developers.html: architecture, feature status, quick-start variants, licences and ADRs.
 * Needs tools/docs' markdown-it: `cd tools/docs && npm ci`, then `node tools/site/build.mjs`.
 * The GitHub Pages workflow (.github/workflows/pages.yml) runs this on every push to main.
 * @module
 */
import { copyFileSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { abs } from "../docs/lib/inputs.mjs";
import { fences, fill } from "./lib/i18n.mjs";
import { REPO_URL, SITE_URL, codeBlock, markdown, repoLink } from "./lib/md.mjs";
import { cleanScreenshot } from "./lib/png.mjs";
import { adrList, featureCards, gpuSection, licences, manualTiles, screen, timeline } from "./lib/render.mjs";
import * as src from "./lib/sources.mjs";

/** Output folder (git-ignored through the generic dist/ rule). */
export const DIST = "site/dist";

/** The pages: template in site/src -> file in dist. The default locale is written to the root. */
export const PAGES = [
  { template: "site/src/index.html", out: "index.html", localised: true },
  { template: "site/src/developers.html", out: "developers.html", localised: false },
];

/** Files copied unchanged: [source, path in dist]. The app's tokens, font and logo are reused as-is. */
const ASSETS = [
  ["src/StemStudio.Web/wwwroot/css/tokens.css", "assets/tokens.css"],
  ["src/StemStudio.Web/wwwroot/fonts/archivo/Archivo-Variable-latin.woff2", "assets/archivo.woff2"],
  ["src/StemStudio.Web/wwwroot/fonts/archivo/OFL.txt", "assets/archivo-OFL.txt"],
  ["src/StemStudio.Web/wwwroot/favicon.svg", "assets/favicon.svg"],
  ["site/src/css/site-tokens.css", "assets/site-tokens.css"],
  ["site/src/css/site.css", "assets/site.css"],
  ["site/src/css/home.css", "assets/home.css"],
  ["site/src/css/dev.css", "assets/dev.css"],
  ["site/src/js/theme.js", "assets/theme.js"],
  ["site/src/js/site.js", "assets/site.js"],
  ["site/src/js/home.js", "assets/home.js"],
];

/** @returns {Record<string, any>} site.json */
export const siteContent = () => JSON.parse(src.readText("site/src/content/site.json"));

/** @param {string} lang locale @returns {Record<string, unknown>} the locale dictionary */
export const locale = (lang) => JSON.parse(src.readText(`site/src/i18n/${lang}.json`));

/** @param {string} rel path inside dist @returns {string} absolute path (parent folder created) */
function out(rel) {
  const path = abs(`${DIST}/${rel}`);
  mkdirSync(abs(`${DIST}/${rel.split("/").slice(0, -1).join("/")}`), { recursive: true });
  return path;
}

/** @param {Buffer} png PNG bytes @returns {{ width: number, height: number }} size from the IHDR chunk */
const pngSize = (png) => ({ width: png.readUInt32BE(16), height: png.readUInt32BE(20) });

/**
 * Copies the gallery screenshots (visual-test masks painted over, optionally cropped) into dist/screens.
 * @param {{ id: string, light: string, dark: string, crop?: import("./lib/png.mjs").Crop }[]} entries site.json gallery
 * @returns {Parameters<typeof screen>[0][]} screens with their published images
 */
function publishScreens(entries) {
  return entries.map((entry) => {
    const images = {};
    for (const theme of ["light", "dark"]) {
      const png = cleanScreenshot(readFileSync(abs(entry[theme])), entry.crop);
      const rel = `screens/${entry.id}-${theme}.png`;
      writeFileSync(out(rel), png);
      images[theme] = { src: rel, ...pngSize(png) };
    }
    return { id: entry.id, images };
  });
}

/** @param {ReturnType<typeof src.manuals>} list manuals @returns {typeof list} the list, PDFs copied */
function publishManuals(list) {
  list.forEach((m) => copyFileSync(abs(`docs/public/${m.file}`), out(`manuals/${m.file}`)));
  return list;
}

/** @param {number} bytes size @returns {string} e.g. "1.4 MB" */
const megabytes = (bytes) => `${(bytes / 1_000_000).toFixed(1)} MB`;

/**
 * Values shared by both pages.
 * @returns {Record<string, string>} placeholder values
 */
function sharedValues() {
  const readme = src.readText("README.md");
  const status = src.readText("docs/STATUS.md");
  const build = src.buildInfo();
  const list = publishManuals(src.manuals());
  const manual = (id) => list.find((m) => m.file.includes(id)) ?? (() => { throw new Error(`manual ${id} not found`); })();
  const user = manual("User-Manual");
  const tech = manual("Technical-Manual");
  const gpu = src.gpuNumbers(status);
  const quick = src.subsections(src.section(readme, "Quick start"));
  const gpuText = [...quick.subsections].find(([t]) => t.startsWith("NVIDIA GPU"))?.[1] ?? "";
  const startCmd = fences(quick.intro)[0];
  const gpuCmd = fences(gpuText)[0]?.split("\n")[0];
  if (!startCmd || !gpuCmd) throw new Error("README quick-start commands not found");
  return {
    siteUrl: SITE_URL,
    repoUrl: REPO_URL,
    licenceUrl: repoLink("LICENSE"),
    thirdPartyUrl: repoLink("docs/THIRD-PARTY-LICENSES.md"),
    zipUrl: `${REPO_URL}/archive/refs/heads/main.zip`,
    userManual: `manuals/${user.file}`,
    techManual: `manuals/${tech.file}`,
    userPages: String(user.pages),
    userSize: megabytes(user.bytes),
    startCmd: codeBlock(startCmd),
    gpuCmd: codeBlock(gpuCmd),
    gpuCpu: String(gpu.cpu),
    gpuGpu: String(gpu.gpu),
    gpuFactor: String(gpu.factor),
    gpuSong: gpu.song,
    gpuRatio: String(Math.round((gpu.gpu / gpu.cpu) * 1000) / 1000),
    gpuBar: String(Math.max(20, Math.round((gpu.gpu / gpu.cpu) * 1000))),
    commitLink: `<a href="${REPO_URL}/commit/${build.sha}"><code>${build.sha.slice(0, 7)}</code></a>`,
    sha: build.sha,
    shortSha: build.sha.slice(0, 7),
    date: build.date,
    year: build.date.slice(0, 4),
    _readme: readme,
    _status: status,
    _manuals: list,
  };
}

/**
 * Values of the developer page (the former home page's technical sections).
 * @param {Record<string, any>} shared shared values
 * @returns {Record<string, string>} placeholder values
 */
function developerValues(shared) {
  const content = siteContent();
  const readme = shared._readme;
  const features = src.roadmapFeatures(src.readText("docs/ROADMAP.md"), shared._status, content.featureFallbacks);
  const quick = src.subsections(src.section(readme, "Quick start"));
  const legal = readme.split("\n## ")[0].match(/^> [\s\S]*?(?=\n\n|\n$)/m)?.[0];
  if (!legal) throw new Error("README personal-practice notice (blockquote before the first section) not found");
  return {
    features: featureCards(src.boldBullets(src.section(readme, "Features")), features),
    quickStart: markdown().render(quick.intro),
    gpu: gpuSection(src.gpuNumbers(shared._status), [...quick.subsections].find(([t]) => t.startsWith("NVIDIA GPU"))?.[1] ?? ""),
    timeline: timeline(features),
    manuals: manualTiles(shared._manuals),
    licences: licences(src.section(readme, "Licences"), src.licenceSections(src.readText("docs/THIRD-PARTY-LICENSES.md"))),
    adrs: adrList(src.adrs()),
    legal: markdown().render(legal),
  };
}

/**
 * Values of the home page in one locale.
 * @param {Record<string, unknown>} dict locale dictionary
 * @param {ReturnType<typeof publishScreens>} shots published screens
 * @param {string} lang locale
 * @param {string} root path from the page to the site root ("" or "../" for a locale folder)
 * @returns {Record<string, string>} placeholder values
 */
function homeValues(dict, shots, lang, root) {
  const content = siteContent();
  const words = dict.screens;
  const values = { lang, root, movedAnchors: content.movedAnchors.join(" ") };
  for (const shot of shots) {
    if (typeof words[shot.id] !== "string") throw new Error(`missing locale string t.screens.${shot.id} (alt text)`);
    const images = Object.fromEntries(Object.entries(shot.images).map(([t, i]) => [t, { ...i, src: root + i.src }]));
    values[`shot.${shot.id}`] = screen({ ...shot, images }, words[shot.id], words);
  }
  return values;
}

/**
 * Builds the whole site.
 * @returns {{ files: number }} summary
 */
export function buildSite() {
  rmSync(abs(DIST), { recursive: true, force: true });
  ASSETS.forEach(([from, to]) => copyFileSync(abs(from), out(to)));
  const shared = sharedValues();
  const shots = publishScreens(siteContent().gallery);
  const publicValues = Object.fromEntries(Object.entries(shared).filter(([k]) => !k.startsWith("_")));
  const [defaultLang, ...others] = siteContent().locales;
  for (const page of PAGES) {
    const template = src.readText(page.template);
    if (!page.localised) {
      writeFileSync(out(page.out), fill(template, { ...publicValues, root: "", ...developerValues(shared) }, {}, page.template), "utf8");
      continue;
    }
    for (const lang of [defaultLang, ...others]) {
      const dict = locale(lang);
      const root = lang === defaultLang ? "" : "../";
      const html = fill(template, { ...publicValues, ...homeValues(dict, shots, lang, root) }, dict, page.template);
      writeFileSync(out(lang === defaultLang ? page.out : `${lang}/${page.out}`), html, "utf8");
    }
  }
  return { files: ASSETS.length + PAGES.length };
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  buildSite();
  console.log(`StemStudio website built into ${DIST}`);
}
