/**
 * Markdown to HTML for one manual chapter: numbered headings with ids, images as figures with
 * absolute file URLs, and the generated feature-status table.
 * @module
 */
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { pathToFileURL } from "node:url";
import MarkdownIt from "markdown-it";
import { abs } from "./inputs.mjs";
import { STATUS_PLACEHOLDER, repoStatusTable } from "./status.mjs";

/**
 * @typedef {{ level: number, number: string, text: string, id: string }} Heading
 * @typedef {{ html: string, title: string, headings: Heading[] }} Chapter
 */

/** @param {string} text heading text @returns {string} URL-safe slug */
export const slug = (text) => text.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-|-$/g, "");

/**
 * Numbers h1/h2 headings ("3", "3.2"), gives every heading an id and records h1/h2 for the TOC.
 * @param {import("markdown-it").Token[]} tokens tokens of the chapter
 * @param {number} chapterNo chapter number
 * @returns {Heading[]} the h1/h2 headings
 */
function numberHeadings(tokens, chapterNo) {
  const headings = [];
  let section = 0;
  tokens.forEach((token, i) => {
    if (token.type !== "heading_open") return;
    const level = Number(token.tag.slice(1));
    const text = tokens[i + 1].content.replace(/<[^>]+>[^<]*<\/[^>]+>|<[^>]+>/g, "").trim();
    if (level === 2) section += 1;
    const number = level === 1 ? `${chapterNo}` : level === 2 ? `${chapterNo}.${section}` : "";
    const id = `c${chapterNo}-${slug(text)}`;
    token.attrSet("id", id);
    if (number) token.attrSet("data-number", number);
    if (level <= 2) headings.push({ level, number, text, id });
  });
  return headings;
}

/**
 * Renders images as figures; the alt text becomes the caption, the path an absolute file URL.
 * @param {MarkdownIt} md renderer
 * @param {string} baseDir folder of the Markdown file
 */
function useFigures(md, baseDir) {
  md.renderer.rules.image = (tokens, idx) => {
    const token = tokens[idx];
    const src = pathToFileURL(resolve(baseDir, token.attrGet("src") ?? "")).href;
    const alt = md.utils.escapeHtml(token.content);
    return `<figure><img src="${src}" alt="${alt}"><figcaption>${alt}</figcaption></figure>`;
  };
}

/**
 * Renders one chapter file.
 * @param {string} rel repository-relative Markdown path
 * @param {number} chapterNo chapter number (1-based)
 * @returns {Chapter} rendered chapter
 */
export function renderChapter(rel, chapterNo) {
  const md = new MarkdownIt({ html: true, typographer: true });
  useFigures(md, dirname(abs(rel)));
  const source = readFileSync(abs(rel), "utf8").replace(STATUS_PLACEHOLDER, repoStatusTable());
  const tokens = md.parse(source, {});
  const headings = numberHeadings(tokens, chapterNo);
  const html = md.renderer.render(tokens, md.options, {}).replace(/<p>(<figure>.*?<\/figure>)<\/p>/gs, "$1");
  return { html, title: headings[0]?.text ?? rel, headings };
}
