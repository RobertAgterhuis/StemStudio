/**
 * Markdown rendering for the website with the markdown-it that tools/docs already pins (no new
 * dependency): `cd tools/docs && npm ci` installs it. Relative repository links become GitHub
 * links, fenced code blocks get a copy button.
 * @module
 */
import { createRequire } from "node:module";
import { pathToFileURL } from "node:url";
import { abs } from "../../docs/lib/inputs.mjs";

/** Public repository; relative README/docs links are rewritten to it. */
export const REPO_URL = "https://github.com/RobertAgterhuis/StemStudio";

/** Published site (canonical URL). */
export const SITE_URL = "https://robertagterhuis.github.io/StemStudio/";

const docsRequire = createRequire(abs("tools/docs/package.json"));
let MarkdownIt;
try {
  MarkdownIt = (await import(pathToFileURL(docsRequire.resolve("markdown-it")).href)).default;
} catch {
  console.error("markdown-it is missing: run `npm ci` in tools/docs first.");
  process.exit(2);
}

/** @param {string} text raw text @returns {string} HTML-escaped text */
export const escapeHtml = (text) => text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");

/**
 * Absolute URL for a link found in a repository Markdown file.
 * @param {string} href link target as written
 * @param {string} baseDir repository-relative folder of the Markdown file ("" for the root)
 * @returns {string} absolute URL
 */
export function repoLink(href, baseDir = "") {
  if (/^[a-z]+:/i.test(href) || href.startsWith("#")) return href;
  const parts = [...baseDir.split("/"), ...href.split("/")].filter((p) => p && p !== ".");
  const path = parts.reduce((acc, p) => (p === ".." ? acc.slice(0, -1) : [...acc, p]), []).join("/");
  return `${REPO_URL}/${href.endsWith("/") ? "tree" : "blob"}/main/${path}`;
}

/**
 * Renders a fenced block as a code panel with a copy button (enabled by site.js).
 * @param {string} code the code
 * @returns {string} HTML
 */
export function codeBlock(code) {
  const text = code.replace(/\n$/, "");
  return `<div class="code"><pre tabindex="0"><code>${escapeHtml(text)}</code></pre><button type="button" class="copy" data-copy hidden>Copy</button></div>`;
}

/**
 * A markdown-it instance for one source file.
 * @param {string} baseDir repository-relative folder of the source ("" for the root)
 * @returns {import("markdown-it").default} renderer
 */
export function markdown(baseDir = "") {
  const md = new MarkdownIt({ html: false, typographer: true, linkify: false });
  const defaultLink = md.renderer.rules.link_open ?? ((tokens, idx, options, _env, self) => self.renderToken(tokens, idx, options));
  md.renderer.rules.link_open = (tokens, idx, options, env, self) => {
    const token = tokens[idx];
    token.attrSet("href", repoLink(token.attrGet("href") ?? "", baseDir));
    return defaultLink(tokens, idx, options, env, self);
  };
  md.renderer.rules.fence = (tokens, idx) => codeBlock(tokens[idx].content);
  return md;
}
