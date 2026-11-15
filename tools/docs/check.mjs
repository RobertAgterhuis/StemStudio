/**
 * Fails (exit 1) when the public manuals are out of date: the SHA-256 of the current source
 * inputs differs from docs/public/build-manifest.json, an output PDF is missing, or a docs
 * style sheet contains a raw colour (colours must come from the app tokens).
 * Needs only Node (no npm install). Usage: node tools/docs/check.mjs
 * @module
 */
import { existsSync, readFileSync } from "node:fs";
import { abs, hashInputs, listFiles, MANIFEST_PATH } from "./lib/inputs.mjs";

const RAW_COLOUR = /#[0-9a-fA-F]{3,8}\b|\b(?:rgba?|hsla?|hwb|lab|lch|oklab|oklch)\(/;
const REGENERATE = "Regenerate: cd tools/docs && npm ci && npm run browser && npm run build, then commit docs/public.";

/** @returns {string[]} problems with the manifest and the outputs */
function manifestProblems() {
  if (!existsSync(abs(MANIFEST_PATH))) return [`${MANIFEST_PATH} is missing.`];
  const manifest = JSON.parse(readFileSync(abs(MANIFEST_PATH), "utf8"));
  const current = hashInputs();
  const missing = manifest.outputs.filter((o) => !existsSync(abs(o.path))).map((o) => `${o.path} is missing.`);
  if (current.sourceHash === manifest.sourceHash) return missing;
  const recorded = new Map(manifest.inputs.map((i) => [i.path, i.sha256]));
  const changed = current.inputs.filter((i) => recorded.get(i.path) !== i.sha256).map((i) => `  changed or new: ${i.path}`);
  const now = new Set(current.inputs.map((i) => i.path));
  const removed = manifest.inputs.filter((i) => !now.has(i.path)).map((i) => `  removed: ${i.path}`);
  return [...missing, "Manual sources changed since the PDFs were built:", ...changed, ...removed];
}

/** @returns {string[]} raw colours found in the docs style sheets */
function colourProblems() {
  return listFiles("tools/docs/styles").flatMap((file) =>
    readFileSync(abs(file), "utf8")
      .replace(/\/\*[\s\S]*?\*\//g, (comment) => comment.replace(/[^\n]/g, ""))
      .split("\n")
      .flatMap((line, i) => (RAW_COLOUR.test(line) ? [`${file}:${i + 1}: raw colour; use a --ss-* token`] : [])),
  );
}

const problems = [...manifestProblems(), ...colourProblems()];
if (problems.length > 0) {
  problems.forEach((p) => console.error(p));
  console.error(REGENERATE);
  process.exit(1);
}
console.log("docs/public manuals are up to date with their sources.");
