/**
 * Builds the website and validates the result (exit 1 on any problem):
 * - every local link, image, script, style sheet and CSS url() resolves to a file in site/dist,
 *   every in-page anchor has a target;
 * - nothing is loaded from another server; links leave the site only for GitHub, the documented
 *   open-data attribution sites and localhost (the app's own address in the quick start);
 * - no literal colours in the site's CSS/JS and no style or colour attributes in the HTML
 *   (colours come from the app tokens), no cookies;
 * - every feature of docs/ROADMAP.md, every ADR and every gallery screen appears on the page.
 * Needs tools/docs' markdown-it: `cd tools/docs && npm ci`, then `node tools/site/check.mjs`.
 * @module
 */
import { existsSync, readFileSync } from "node:fs";
import { abs, listFiles } from "../docs/lib/inputs.mjs";
import { DIST, buildSite } from "./build.mjs";
import { escapeHtml } from "./lib/md.mjs";
import { adrs, readText, roadmapFeatures } from "./lib/sources.mjs";

/** Hosts a link may point to (attribution and source links; never loaded resources). */
const ALLOWED_HOSTS = new Set([
  "github.com",
  "robertagterhuis.github.io",
  "localhost",
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

/**
 * Checks one URL found in the page.
 * @param {string} url href or src value
 * @param {string} attr "href" or "src"
 * @param {Set<string>} ids element ids of the page
 * @returns {string | null} problem or null
 */
function urlProblem(url, attr, ids) {
  if (url.startsWith("#")) return ids.has(url.slice(1)) ? null : `anchor ${url} has no target`;
  if (/^https?:/.test(url)) {
    if (attr === "src") return `external resource ${url} (load nothing from other servers)`;
    const host = new URL(url).hostname;
    return ALLOWED_HOSTS.has(host) ? null : `external link to ${host} is not on the allow-list`;
  }
  if (/^[a-z]+:/i.test(url)) return `unsupported URL scheme in ${url}`;
  return existsSync(abs(`${DIST}/${url.split(/[?#]/)[0]}`)) ? null : `missing file ${url}`;
}

/** @param {string} html page @returns {string[]} problems with links, resources and attributes */
function htmlProblems(html) {
  const ids = new Set([...html.matchAll(/\sid="([^"]+)"/g)].map((m) => m[1]));
  const urls = [...html.matchAll(/\s(href|src)="([^"]*)"/g)].map((m) => urlProblem(m[2].replace(/&amp;/g, "&"), m[1], ids));
  const external = [...html.matchAll(/<(link|script)\b[^>]*\s(?:href|src)="(https?:[^"]+)"/g)].filter((m) => !/rel="canonical"/.test(m[0])).map((m) => `<${m[1]}> loads ${m[2]}`);
  const styles = /\sstyle="/.test(html) ? ["inline style attribute (blocked by the page's CSP)"] : [];
  const colours = [...html.matchAll(/\s(?:fill|stroke|color|stop-color)="(?!none|currentColor)[^"]*"/g)].map((m) => `colour attribute ${m[0].trim()}`);
  const placeholders = [...html.matchAll(/\{\{\w+\}\}/g)].map((m) => `unreplaced placeholder ${m[0]}`);
  return [...urls.filter(Boolean), ...external, ...styles, ...colours, ...placeholders];
}

/** @returns {string[]} url() references in the CSS that do not resolve */
function cssProblems() {
  return listFiles(`${DIST}/assets`)
    .filter((f) => f.endsWith(".css"))
    .flatMap((file) => [...readText(file).matchAll(/url\(["']?([^"')]+)["']?\)/g)].filter((m) => !existsSync(abs(`${DIST}/assets/${m[1]}`))).map((m) => `${file}: missing ${m[1]}`));
}

/** @param {string} html page @returns {string[]} roadmap features, ADRs and screens missing from the page */
function contentProblems(html) {
  const content = JSON.parse(readText("site/src/content/site.json"));
  const features = roadmapFeatures(readText("docs/ROADMAP.md"), readText("docs/STATUS.md"), content.featureFallbacks);
  const missingFeatures = features.filter((f) => !html.includes(`>${escapeHtml(f.name)}<`)).map((f) => `feature ${f.id} "${f.name}" is not on the page`);
  const missingAdrs = adrs().filter((a) => !html.includes(`>${a.id}<`)).map((a) => `${a.id} is not on the page`);
  const screens = content.gallery.flatMap((g) => ["light", "dark"].map((t) => `screens/${g.id}-${t}.png`)).filter((s) => !html.includes(`src="${s}"`)).map((s) => `${s} is not on the page`);
  return [...missingFeatures, ...missingAdrs, ...screens];
}

/** @returns {string[]} cookie use in the site's scripts */
const cookieProblems = () => listFiles("site/src/js").filter((f) => /document\.cookie/.test(readText(f))).map((f) => `${f} uses cookies`);

buildSite();
const html = readFileSync(abs(`${DIST}/index.html`), "utf8");
const problems = [...colourProblems(), ...htmlProblems(html), ...cssProblems(), ...contentProblems(html), ...cookieProblems()];
if (problems.length > 0) {
  problems.forEach((p) => console.error(p));
  process.exit(1);
}
console.log(`site/dist is complete: ${listFiles(DIST).length} files, links and assets resolve, no literal colours, no external resources.`);
