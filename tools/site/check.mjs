/**
 * Builds the website and validates the result (exit 1 on any problem):
 * - every local link, image, script, style sheet and CSS url() resolves to a file in site/dist,
 *   every anchor (in-page or on another page of the site) has a target;
 * - nothing is loaded from another server; links leave the site only for GitHub, Docker's install
 *   guide, the documented open-data attribution sites and localhost (the app's own address);
 * - no literal colours in the site's CSS/JS and no style or colour attributes in the HTML
 *   (colours come from the app tokens), no cookies;
 * - developer page: every feature of docs/ROADMAP.md and every ADR appears;
 * - home page: every gallery screen appears, every README feature bullet is placed in a section
 *   (site.json featureMap), "Coming soon" lists only undelivered roadmap features, the anchors
 *   that moved to the developer page exist there, and no sales or hype words are used;
 * - every locale has the same keys as English.
 * Needs tools/docs' markdown-it: `cd tools/docs && npm ci`, then `node tools/site/check.mjs`.
 * @module
 */
import { existsSync, readFileSync } from "node:fs";
import { abs, listFiles } from "../docs/lib/inputs.mjs";
import { DIST, PAGES, buildSite, locale, siteContent } from "./build.mjs";
import { bannedWords, featureCoverage } from "./lib/i18n.mjs";
import { escapeHtml } from "./lib/md.mjs";
import { adrs, boldBullets, readText, roadmapFeatures, section } from "./lib/sources.mjs";
import { deliveredOn } from "../docs/lib/status.mjs";

/** Hosts a link may point to (attribution, source and install links; never loaded resources). */
const ALLOWED_HOSTS = new Set([
  "github.com",
  "robertagterhuis.github.io",
  "localhost",
  "docs.docker.com",
  "musicbrainz.org",
  "coverartarchive.org",
  "www.wikidata.org",
  "en.wikipedia.org",
  "creativecommons.org",
]);

const RAW_COLOUR = /#[0-9a-fA-F]{3,8}\b|\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch|color)\(|:\s*[^;{}]*\b(?:white|black|red|green|blue|gray|grey|orange|yellow|purple|pink)\b/;

/** @param {string} text source @returns {string} the text with comments blanked (line numbers kept) */
const stripComments = (text) => text.replace(/\/\*[\s\S]*?\*\//g, (c) => c.replace(/[^\n]/g, ""));

/** @returns {string[]} literal colours in the site's own style sheets and scripts */
function colourProblems() {
  return [...listFiles("site/src/css"), ...listFiles("site/src/js")].flatMap((file) =>
    stripComments(readText(file))
      .split("\n")
      .flatMap((line, i) => (RAW_COLOUR.test(line.replace(/"[^"]*"|'[^']*'/g, '""')) ? [`${file}:${i + 1}: literal colour; use a --site-*/--ss-* token`] : [])),
  );
}

/** @param {string} html page @returns {Set<string>} element ids */
const idsOf = (html) => new Set([...html.matchAll(/\sid="([^"]+)"/g)].map((m) => m[1]));

/**
 * Checks one URL found in a page.
 * @param {string} url href or src value
 * @param {string} attr "href" or "src"
 * @param {Set<string>} ids element ids of the page itself
 * @param {Map<string, Set<string>>} pageIds element ids per built page (path in dist)
 * @returns {string | null} problem or null
 */
function urlProblem(url, attr, ids, pageIds) {
  if (url.startsWith("#")) return ids.has(url.slice(1)) ? null : `anchor ${url} has no target`;
  if (/^https?:/.test(url)) {
    if (attr === "src") return `external resource ${url} (load nothing from other servers)`;
    const host = new URL(url).hostname;
    return ALLOWED_HOSTS.has(host) ? null : `external link to ${host} is not on the allow-list`;
  }
  if (/^[a-z]+:/i.test(url)) return `unsupported URL scheme in ${url}`;
  const [path, anchor] = url.split("#");
  const file = path.split("?")[0] || "index.html";
  if (!existsSync(abs(`${DIST}/${file}`))) return `missing file ${url}`;
  if (anchor && pageIds.has(file) && !pageIds.get(file).has(anchor)) return `anchor #${anchor} has no target on ${file}`;
  return null;
}

/**
 * @param {string} html page
 * @param {Map<string, Set<string>>} pageIds element ids per built page
 * @returns {string[]} problems with links, resources and attributes
 */
function htmlProblems(html, pageIds) {
  const ids = idsOf(html);
  const urls = [...html.matchAll(/\s(href|src)="([^"]*)"/g)].map((m) => urlProblem(m[2].replace(/&amp;/g, "&"), m[1], ids, pageIds));
  const external = [...html.matchAll(/<(link|script)\b[^>]*\s(?:href|src)="(https?:[^"]+)"/g)].filter((m) => !/rel="canonical"/.test(m[0])).map((m) => `<${m[1]}> loads ${m[2]}`);
  const styles = /\sstyle="/.test(html) ? ["inline style attribute (blocked by the page's CSP)"] : [];
  const colours = [...html.matchAll(/\s(?:fill|stroke|color|stop-color)="(?!none|currentColor)[^"]*"/g)].map((m) => `colour attribute ${m[0].trim()}`);
  const placeholders = [...html.matchAll(/\{\{[\w.-]+\}\}/g)].map((m) => `unreplaced placeholder ${m[0]}`);
  const duplicates = [...html.matchAll(/\sid="([^"]+)"/g)].map((m) => m[1]).filter((id, i, all) => all.indexOf(id) !== i).map((id) => `duplicate id "${id}"`);
  return [...urls.filter(Boolean), ...external, ...styles, ...colours, ...placeholders, ...duplicates];
}

/** @returns {string[]} url() references in the CSS that do not resolve */
function cssProblems() {
  return listFiles(`${DIST}/assets`)
    .filter((f) => f.endsWith(".css"))
    .flatMap((file) => [...readText(file).matchAll(/url\(["']?([^"')]+)["']?\)/g)].filter((m) => !existsSync(abs(`${DIST}/assets/${m[1]}`))).map((m) => `${file}: missing ${m[1]}`));
}

/** @param {string} html developer page @returns {string[]} roadmap features and ADRs missing from it */
function developerProblems(html) {
  const content = siteContent();
  const features = roadmapFeatures(readText("docs/ROADMAP.md"), readText("docs/STATUS.md"), content.featureFallbacks);
  const missingFeatures = features.filter((f) => !html.includes(`>${escapeHtml(f.name)}<`)).map((f) => `developers.html: feature ${f.id} "${f.name}" is missing`);
  const missingAdrs = adrs().filter((a) => !html.includes(`>${a.id}<`)).map((a) => `developers.html: ${a.id} is missing`);
  return [...missingFeatures, ...missingAdrs];
}

/**
 * @param {string} html home page
 * @param {Set<string>} developerIds element ids of the developer page
 * @returns {string[]} home-page content problems
 */
function homeProblems(html, developerIds) {
  const content = siteContent();
  const ids = idsOf(html);
  const status = readText("docs/STATUS.md");
  const screens = content.gallery.flatMap((g) => ["light", "dark"].map((t) => `screens/${g.id}-${t}.png`)).filter((s) => !html.includes(`src="${s}"`)).map((s) => `index.html: ${s} is missing`);
  const bullets = boldBullets(section(readText("README.md"), "Features")).map((b) => b.title);
  const map = Object.fromEntries(Object.entries(content.featureMap).filter(([k]) => !k.startsWith("$")));
  const coverage = featureCoverage(bullets, map, ids);
  const soon = Object.entries(content.comingSoon)
    .filter(([k]) => !k.startsWith("$"))
    .flatMap(([key, id]) => [
      ...(deliveredOn(status, id) ? [`feature ${id} is delivered: take it out of "Coming soon" (site.json comingSoon.${key})`] : []),
      ...(html.includes(`data-roadmap="${key}"`) ? [] : [`index.html: coming-soon item "${key}" is missing`]),
    ]);
  const moved = content.movedAnchors.flatMap((id) => [
    ...(developerIds.has(id) ? [] : [`moved anchor #${id} does not exist on developers.html`]),
    ...(ids.has(id) ? [`moved anchor #${id} still exists on index.html`] : []),
  ]);
  const kept = ["quick-start", "start", "features", "screens", "gpu", "manuals", "legal", "top", "main"].filter((id) => !ids.has(id)).map((id) => `index.html: anchor #${id} (linked from outside) is missing`);
  return [...screens, ...coverage, ...soon, ...moved, ...kept, ...bannedWords(html)];
}

/** @param {unknown} node locale node @param {string} prefix path @returns {string[]} dotted keys */
const keys = (node, prefix = "") =>
  Object.entries(node).flatMap(([k, v]) => (k.startsWith("$") ? [] : typeof v === "object" ? keys(v, `${prefix}${k}.`) : [`${prefix}${k}`]));

/** @returns {string[]} locales whose keys differ from English */
function localeProblems() {
  const english = new Set(keys(locale("en")));
  return siteContent()
    .locales.filter((l) => l !== "en")
    .flatMap((l) => {
      const own = new Set(keys(locale(l)));
      return [...[...english].filter((k) => !own.has(k)).map((k) => `${l}.json lacks ${k}`), ...[...own].filter((k) => !english.has(k)).map((k) => `${l}.json has unknown key ${k}`)];
    });
}

/** @returns {string[]} cookie use in the site's scripts */
const cookieProblems = () => listFiles("site/src/js").filter((f) => /document\.cookie/.test(readText(f))).map((f) => `${f} uses cookies`);

buildSite();
const built = listFiles(DIST).filter((f) => f.endsWith(".html")).map((f) => f.slice(DIST.length + 1));
const pages = new Map(built.map((rel) => [rel, readFileSync(abs(`${DIST}/${rel}`), "utf8")]));
const pageIds = new Map([...pages].map(([rel, html]) => [rel, idsOf(html)]));
const problems = [
  ...colourProblems(),
  ...[...pages].flatMap(([rel, html]) => htmlProblems(html, pageIds).map((p) => `${rel}: ${p}`)),
  ...cssProblems(),
  ...developerProblems(pages.get("developers.html")),
  ...homeProblems(pages.get("index.html"), pageIds.get("developers.html")),
  ...localeProblems(),
  ...cookieProblems(),
];
if (PAGES.some((p) => !pages.has(p.out))) problems.push("a page of PAGES was not built");
if (problems.length > 0) {
  problems.forEach((p) => console.error(p));
  process.exit(1);
}
console.log(`site/dist is complete: ${listFiles(DIST).length} files, ${pages.size} pages, links and anchors resolve, no literal colours, no external resources.`);
