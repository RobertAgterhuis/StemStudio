/**
 * HTML templates of a manual: cover, table of contents, chapters and the PDF footer.
 * Colours come only from the app tokens (src/StemStudio.Web/wwwroot/css/tokens.css); the footer
 * template, which cannot use the page's CSS, receives token values resolved from that file.
 * @module
 */
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { abs } from "./inputs.mjs";

/** @typedef {import("./markdown.mjs").Chapter} Chapter */
/** @typedef {{ id: string, title: string, subtitle: string, audience: string, output: string }} ManualConfig */

const STEMS = ["vocals", "drums", "bass", "guitar", "piano", "other"];
const TOKENS = "src/StemStudio.Web/wwwroot/css/tokens.css";
const FONT = "src/StemStudio.Web/wwwroot/fonts/archivo/Archivo-Variable-latin.woff2";

/** @param {string} rel repository-relative path @returns {string} file URL */
const fileUrl = (rel) => pathToFileURL(abs(rel)).href;

/** @param {string} text raw text @returns {string} HTML-escaped text */
export const escapeHtml = (text) => text.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c]);

/**
 * Value of a custom property in the light theme block of the app tokens (falls back to :root).
 * @param {string} name property name, e.g. "--ss-color-muted"
 * @returns {string} the declared value
 */
export function lightToken(name) {
  const css = readFileSync(abs(TOKENS), "utf8");
  const light = css.slice(css.indexOf('html[data-theme="light"]'));
  const pattern = new RegExp(`${name}:\\s*([^;]+);`);
  return (pattern.exec(light) ?? pattern.exec(css) ?? ["", "inherit"])[1].trim();
}

/** @returns {string} the "one strand becomes six" signature drawn with the stem tokens */
export function strandSvg() {
  const paths = STEMS.map((stem, i) => {
    const y = 8 + i * 16.8;
    return `<path d="M260 50 C 430 50 450 ${y} 620 ${y} H 1000" style="stroke: var(--ss-stem-${stem})"/>`;
  }).join("");
  return `<svg class="strand" viewBox="0 0 1000 100" preserveAspectRatio="none" aria-hidden="true"><path class="strand-root" d="M0 50 H 260"/>${paths}</svg>`;
}

/**
 * The cover page.
 * @param {ManualConfig} config manual configuration
 * @param {{ date: string, hash: string }} build build facts
 * @returns {string} HTML section
 */
export function coverHtml(config, build) {
  const logo = readFileSync(abs("src/StemStudio.Web/wwwroot/favicon.svg"), "utf8");
  return `<section class="cover">
  <div class="cover-brand"><span class="cover-logo">${logo}</span><span class="cover-name">StemStudio</span></div>
  <div class="cover-title"><h1 class="cover-h1">${escapeHtml(config.title)}</h1><p class="cover-sub">${escapeHtml(config.subtitle)}</p></div>
  ${strandSvg()}
  <dl class="cover-facts"><dt>Audience</dt><dd>${escapeHtml(config.audience)}</dd><dt>Built</dt><dd>${build.date}</dd><dt>Sources</dt><dd><code>${build.hash.slice(0, 12)}</code></dd></dl>
  <p class="cover-note">This manual is a living document. It is regenerated whenever a feature changes what you see or how StemStudio is set up.</p>
</section>`;
}

/**
 * The table of contents; chapter page numbers come from the layout pass.
 * @param {Chapter[]} chapters rendered chapters
 * @param {number[]} pages first page of every chapter (same order)
 * @returns {string} HTML section
 */
export function tocHtml(chapters, pages) {
  const items = chapters.map((chapter, i) => {
    const [head, ...sections] = chapter.headings;
    const subs = sections.map((s) => `<li class="toc-sub"><a href="#${s.id}"><span class="toc-no">${s.number}</span>${escapeHtml(s.text)}</a></li>`).join("");
    return `<li class="toc-chapter"><a href="#${head.id}"><span class="toc-no">${head.number}</span>${escapeHtml(head.text)}<span class="toc-page">${pages[i] ?? ""}</span></a><ul>${subs}</ul></li>`;
  });
  return `<section class="toc"><h2 class="toc-title">Contents</h2><ol class="toc-list">${items.join("")}</ol></section>`;
}

/** @param {Chapter} chapter rendered chapter @returns {string} HTML section */
export const chapterHtml = (chapter) => `<section class="chapter">${strandSvg()}${chapter.html}</section>`;

/**
 * A full HTML document in the light theme ("day session") for print.
 * @param {string} title document title
 * @param {string} body HTML body content
 * @returns {string} HTML document
 */
export function documentHtml(title, body) {
  const css = ["styles/doc-tokens.css", "styles/print.css"].map((f) => `<link rel="stylesheet" href="${fileUrl(`tools/docs/${f}`)}">`);
  return `<!doctype html><html lang="en" data-theme="light"><head><meta charset="utf-8"><title>${escapeHtml(title)}</title>
<link rel="stylesheet" href="${fileUrl(TOKENS)}">
<style>@font-face { font-family: "Archivo"; src: url("${fileUrl(FONT)}") format("woff2"); font-weight: 100 900; font-stretch: 62% 125%; font-style: normal; }</style>
${css.join("\n")}</head><body>${body}</body></html>`;
}

/**
 * Footer of every page except the cover: manual title and "page n of m".
 * @param {string} title manual title
 * @returns {string} Chromium footer template
 */
export function footerTemplate(title) {
  const style = `font-family: Archivo, "Segoe UI", sans-serif; font-size: 8pt; color: ${lightToken("--ss-color-muted")}; width: 100%; margin: 0 18mm; display: flex; justify-content: space-between; font-variant-numeric: tabular-nums;`;
  return `<div style='${style}'><span>StemStudio · ${escapeHtml(title)}</span><span>Page <span class="pageNumber"></span> of <span class="totalPages"></span></span></div>`;
}
