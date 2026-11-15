/**
 * Unit tests of the website generator's pure helpers (node:test, no extra dependencies).
 * Run: (cd tools/docs && npm ci) && node --test tools/site/test/site.test.mjs
 * @module
 */
import assert from "node:assert/strict";
import { test } from "node:test";
import { repoLink, REPO_URL } from "../lib/md.mjs";
import { cleanScreenshot, decodePng, encodePng, paintOverMasks } from "../lib/png.mjs";
import { boldBullets, gpuNumbers, licenceSections, roadmapFeatures, section } from "../lib/sources.mjs";

/** @returns {import("../lib/png.mjs").Image} 3x1 RGB image: grey, magenta, magenta */
const masked = () => ({ width: 3, height: 1, colorType: 2, channels: 3, pixels: Buffer.from([10, 20, 30, 255, 0, 255, 255, 0, 255]) });

test("masks take the colour of the nearest unmasked pixel to the left", () => {
  const image = masked();
  assert.equal(paintOverMasks(image), 2);
  assert.deepEqual([...image.pixels], [10, 20, 30, 10, 20, 30, 10, 20, 30]);
});

test("PNG encode and decode round-trip; cleaning changes only masked files", () => {
  const png = encodePng(masked());
  assert.deepEqual([...decodePng(png).pixels], [...masked().pixels]);
  assert.deepEqual([...decodePng(cleanScreenshot(png)).pixels], [10, 20, 30, 10, 20, 30, 10, 20, 30]);
  const clean = encodePng({ ...masked(), pixels: Buffer.from([1, 2, 3, 4, 5, 6, 7, 8, 9]) });
  assert.equal(cleanScreenshot(clean), clean);
  assert.throws(() => decodePng(Buffer.from("nope")), /not a PNG/);
});

test("relative repository links become GitHub links", () => {
  assert.equal(repoLink("docs/decisions/"), `${REPO_URL}/tree/main/docs/decisions`);
  assert.equal(repoLink("../ROADMAP.md", "docs/public"), `${REPO_URL}/blob/main/docs/ROADMAP.md`);
  assert.equal(repoLink("https://example.org/x"), "https://example.org/x");
  assert.equal(repoLink("#top"), "#top");
});

test("README bullets, sections and the GPU numbers are parsed", () => {
  const readme = "intro\n## Features (MVP)\n\n- **Import** from a file.\n- **Mixer**: faders.\n\n## Next\n";
  assert.deepEqual(boldBullets(section(readme, "Features")).map((b) => [b.title, b.html]), [["Import", "From a file."], ["Mixer", "Faders."]]);
  assert.throws(() => section(readme, "Missing"), /not found/);
  const status = "| Architect spike, reference song 4:22 | standard + detail 3.7 min on GPU vs 31.5 min CPU (8.5x); same |";
  assert.deepEqual(gpuNumbers(status), { song: "4:22", gpu: 3.7, cpu: 31.5, factor: 8.5 });
  assert.throws(() => gpuNumbers("nothing"), /not found/);
});

test("roadmap features carry description and delivery date", () => {
  const roadmap = "| # | Feature |\n|---|---|\n| 002 | Detail (in progress) | x |\n| 003 | **GPU**: fast | y |\n\n## Later\n| 099 | **No** | z |";
  const status = "## Final verification (CTO, 2026-11-15)\n## Feature 003 GPU (2026-11-16)\n";
  const features = roadmapFeatures(roadmap, status, { "001": "the mvp", "002": "fallback text" });
  assert.deepEqual(
    features.map((f) => [f.id, f.name, f.description, f.delivered]),
    [
      ["001", "MVP: import, separation, mixer and mixes", "The mvp", "2026-11-15"],
      ["002", "Detail", "Fallback text", null],
      ["003", "GPU", "Fast", "2026-11-16"],
    ],
  );
  assert.throws(() => roadmapFeatures(roadmap, status, {}), /no description/);
});

test("licence groups count their table rows and skip rejected components", () => {
  const text = "# T\n## Runtime\n| A | B |\n|---|---|\n| x | 1 |\n| y | 2 |\n### Empty\ntext\n## Rejected for licence reasons\n| A |\n|---|\n| z |\n";
  assert.deepEqual(licenceSections(text), [{ title: "Runtime", level: 2, count: 2 }]);
});
