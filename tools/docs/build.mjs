/**
 * Builds docs/public/StemStudio-*-Manual.pdf from the Markdown sources in docs/public/src and
 * writes docs/public/build-manifest.json (SHA-256 of every source input).
 *
 * Usage (from tools/docs): npm ci && npm run browser && npm run build
 * @module
 */
import { readFileSync, readdirSync, writeFileSync } from "node:fs";
import { abs, hashInputs, MANIFEST_PATH, SOURCE_DIR } from "./lib/inputs.mjs";
import { renderChapter } from "./lib/markdown.mjs";
import { chapterHtml, coverHtml, documentHtml, footerTemplate, tocHtml } from "./lib/html.mjs";
import { countPages, prependCover, Printer } from "./lib/pdf.mjs";

/** @typedef {import("./lib/html.mjs").ManualConfig} ManualConfig */

/** @returns {string[]} manual folders below docs/public/src (each has a manual.json) */
const manualDirs = () => readdirSync(abs(SOURCE_DIR)).filter((d) => !d.includes(".")).sort();

/** @param {string} dir manual folder @returns {ManualConfig} its configuration */
const readConfig = (dir) => JSON.parse(readFileSync(abs(`${SOURCE_DIR}/${dir}/manual.json`), "utf8"));

/** @param {string} dir manual folder @returns {string[]} chapter files in file-name order */
const chapterFiles = (dir) => readdirSync(abs(`${SOURCE_DIR}/${dir}`)).filter((f) => f.endsWith(".md")).sort().map((f) => `${SOURCE_DIR}/${dir}/${f}`);

/**
 * Layout pass: the first page of every chapter (cover = page 1, then the contents pages).
 * @param {Printer} printer Chromium printer
 * @param {ManualConfig} config manual configuration
 * @param {import("./lib/markdown.mjs").Chapter[]} chapters rendered chapters
 * @returns {Promise<number[]>} first page per chapter
 */
async function chapterStartPages(printer, config, chapters) {
  const tocPages = await countPages(await printer.print(documentHtml(config.title, tocHtml(chapters, chapters.map(() => 999)))));
  const starts = [];
  let next = 1 + tocPages;
  for (const chapter of chapters) {
    starts.push(next);
    next += await countPages(await printer.print(documentHtml(config.title, chapterHtml(chapter))));
  }
  return starts;
}

/**
 * Builds one manual and writes its PDF.
 * @param {Printer} printer Chromium printer
 * @param {string} dir manual folder
 * @param {{ date: string, hash: string }} build build facts
 * @returns {Promise<{ path: string, pages: number, bytes: number }>} the written output
 */
async function buildManual(printer, dir, build) {
  const config = readConfig(dir);
  const chapters = chapterFiles(dir).map((file, i) => renderChapter(file, i + 1));
  const starts = await chapterStartPages(printer, config, chapters);
  const cover = await printer.print(documentHtml(config.title, coverHtml(config, build)));
  const body = [tocHtml(chapters, starts), ...chapters.map(chapterHtml)].join("\n");
  const pdf = await prependCover(cover, await printer.print(documentHtml(config.title, body), footerTemplate(config.title)));
  const path = `docs/public/${config.output}`;
  writeFileSync(abs(path), pdf);
  return { path, pages: await countPages(pdf), bytes: pdf.length };
}

/** Builds both manuals and the manifest. */
async function main() {
  const { sourceHash, inputs } = hashInputs();
  const build = { date: new Date().toISOString().slice(0, 10), hash: sourceHash };
  const printer = await Printer.start();
  try {
    const outputs = [];
    for (const dir of manualDirs()) outputs.push(await buildManual(printer, dir, build));
    const manifest = { schema: 1, builtAt: build.date, sourceHash, outputs, inputs };
    writeFileSync(abs(MANIFEST_PATH), `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
    outputs.forEach((o) => console.log(`${o.path}: ${o.pages} pages, ${o.bytes} bytes`));
    console.log(`source hash ${sourceHash}`);
  } finally {
    await printer.stop();
  }
}

await main();
