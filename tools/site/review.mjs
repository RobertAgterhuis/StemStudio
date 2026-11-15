/**
 * Local design review of the built site: serves site/dist under /StemStudio/ on a free port,
 * runs axe (WCAG 2.2 AA tags) in light and dark at 1440, 390 and 320 px, and saves home-page
 * screenshots (light/dark at 1440 and 390 px) into docs/design-review/site/.
 * Reuses the e2e suite's Playwright and @axe-core/playwright:
 *   (cd e2e && npm ci) && (cd tools/docs && npm ci) && node tools/site/build.mjs && node tools/site/review.mjs
 * Exit code 1 when axe reports a violation.
 * @module
 */
import { mkdirSync } from "node:fs";
import { createRequire } from "node:module";
import { abs } from "../docs/lib/inputs.mjs";
import { serve } from "./lib/serve.mjs";

const e2eRequire = createRequire(abs("e2e/package.json"));
const { chromium } = e2eRequire("@playwright/test");
const { AxeBuilder } = e2eRequire("@axe-core/playwright");

const TAGS = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa"];
const SHOTS_DIR = "docs/design-review/site";
const VIEWPORTS = [
  { width: 1440, height: 900, shot: true },
  { width: 390, height: 844, shot: true },
  { width: 320, height: 640, shot: false },
];

/**
 * Opens the page in one theme and viewport, scans it and optionally saves a full-page screenshot.
 * @param {import("@playwright/test").Browser} browser browser
 * @param {string} url site URL
 * @param {"light" | "dark"} theme colour scheme
 * @param {{ width: number, height: number, shot: boolean }} viewport viewport
 * @returns {Promise<string[]>} violations as text
 */
async function reviewOne(browser, url, theme, viewport) {
  const context = await browser.newContext({ colorScheme: theme, reducedMotion: "reduce", viewport });
  const page = await context.newPage();
  const errors = [];
  page.on("console", (m) => m.type() === "error" && errors.push(`console: ${m.text()}`));
  page.on("pageerror", (e) => errors.push(`page error: ${e.message}`));
  await page.goto(url, { waitUntil: "networkidle" });
  const scroll = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  if (scroll > 0) errors.push(`horizontal page scroll of ${scroll}px`);
  const result = await new AxeBuilder({ page }).withTags(TAGS).analyze();
  const violations = result.violations.map((v) => `${v.id} (${v.impact}): ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`);
  if (viewport.shot) {
    await page.evaluate(() => document.querySelectorAll("img[loading=lazy]").forEach((img) => img.setAttribute("loading", "eager")));
    await page.waitForFunction(() => [...document.images].filter((i) => i.offsetParent).every((i) => i.complete && i.naturalWidth > 0));
    await page.evaluate(async () => {
      for (let y = 0; y < document.body.scrollHeight; y += window.innerHeight) {
        window.scrollTo(0, y);
        await new Promise((r) => setTimeout(r, 60));
      }
      window.scrollTo(0, 0);
    });
    await page.screenshot({ path: abs(`${SHOTS_DIR}/home-${viewport.width}-${theme}.png`), fullPage: true });
  }
  await context.close();
  return [...errors, ...violations].map((v) => `${theme} ${viewport.width}px: ${v}`);
}

/** Runs the review. */
async function main() {
  mkdirSync(abs(SHOTS_DIR), { recursive: true });
  const server = await serve(abs("site/dist"));
  const browser = await chromium.launch();
  try {
    const problems = [];
    for (const theme of ["light", "dark"]) for (const viewport of VIEWPORTS) problems.push(...(await reviewOne(browser, server.url, theme, viewport)));
    problems.forEach((p) => console.error(p));
    console.log(`axe: ${problems.length} problem(s) on ${VIEWPORTS.length * 2} page states; screenshots in ${SHOTS_DIR}`);
    process.exitCode = problems.length > 0 ? 1 : 0;
  } finally {
    await browser.close();
    await server.close();
  }
}

await main();
