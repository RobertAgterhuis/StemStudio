/**
 * Prints HTML to PDF with Playwright's Chromium (print media, A4, light theme).
 * @module
 */
import { mkdtempSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { pathToFileURL } from "node:url";
import { PDFDocument } from "pdf-lib";
import { chromium } from "playwright-core";

/** @param {Uint8Array} pdf PDF bytes @returns {Promise<number>} number of pages */
export const countPages = async (pdf) => (await PDFDocument.load(pdf)).getPageCount();

/**
 * Puts the cover (printed without a footer) in front of the body, so the cover carries no page
 * number and the body keeps its links, outline and "page n of m" footer.
 * @param {Uint8Array} cover one-page cover PDF
 * @param {Uint8Array} body body PDF (contents and chapters)
 * @returns {Promise<Buffer>} the merged PDF
 */
export async function prependCover(cover, body) {
  const doc = await PDFDocument.load(body);
  const [page] = await doc.copyPages(await PDFDocument.load(cover), [0]);
  doc.insertPage(0, page);
  return Buffer.from(await doc.save());
}

/** A Chromium session that prints HTML documents written to a temporary folder. */
export class Printer {
  /** @returns {Promise<Printer>} a started printer */
  static async start() {
    const printer = new Printer();
    printer.browser = await chromium.launch();
    printer.page = await printer.browser.newPage();
    printer.dir = mkdtempSync(join(tmpdir(), "stemstudio-docs-"));
    return printer;
  }

  /**
   * Prints one HTML document.
   * @param {string} html full HTML document
   * @param {string} [footer] footer template; omitted = no header or footer
   * @returns {Promise<Buffer>} PDF bytes
   */
  async print(html, footer) {
    const file = join(this.dir, "page.html");
    writeFileSync(file, html, "utf8");
    await this.page.goto(pathToFileURL(file).href, { waitUntil: "load" });
    await this.page.evaluate(() => document.fonts.ready);
    const chrome = footer ? { displayHeaderFooter: true, headerTemplate: "<span></span>", footerTemplate: footer } : {};
    return this.page.pdf({ format: "A4", printBackground: true, preferCSSPageSize: true, outline: true, tagged: true, ...chrome });
  }

  /** Closes the browser and removes the temporary folder. */
  async stop() {
    await this.browser.close();
    rmSync(this.dir, { recursive: true, force: true });
  }
}
