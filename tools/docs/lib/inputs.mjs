/**
 * Source inputs of the public manuals and their SHA-256 hashes (build-manifest.json).
 * No third-party imports: `check.mjs` runs this without `npm ci`.
 * @module
 */
import { createHash } from "node:crypto";
import { readFileSync, readdirSync, statSync, existsSync } from "node:fs";
import { dirname, join, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";

/** Repository root (two levels above tools/docs/lib). */
export const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");

/** Folder with the Markdown sources of both manuals. */
export const SOURCE_DIR = "docs/public/src";

/** The manifest written next to the PDFs. */
export const MANIFEST_PATH = "docs/public/build-manifest.json";

/** Fixed inputs outside the Markdown folder: app tokens, font, logo, status sources, tool code. */
export const FIXED_INPUTS = [
  "src/StemStudio.Web/wwwroot/css/tokens.css",
  "src/StemStudio.Web/wwwroot/fonts/archivo/Archivo-Variable-latin.woff2",
  "src/StemStudio.Web/wwwroot/favicon.svg",
  "docs/ROADMAP.md",
  "docs/STATUS.md",
  "tools/docs/package.json",
  "tools/docs/package-lock.json",
  "tools/docs/build.mjs",
];

/** Folders whose files are all inputs (tool code and styles). */
const INPUT_DIRS = [SOURCE_DIR, "tools/docs/lib", "tools/docs/styles"];

/** Extensions hashed with normalised line endings (git may check them out with CRLF). */
const TEXT_EXTENSIONS = [".md", ".css", ".mjs", ".json", ".svg", ".html", ".txt"];

const IMAGE_REF = /!\[[^\]]*\]\(([^)\s]+)\)/g;

/** @param {string} rel repository-relative path @returns {string} absolute path */
export const abs = (rel) => join(REPO_ROOT, ...rel.split("/"));

/** @param {string} path absolute path @returns {string} repository-relative POSIX path */
export const repoRel = (path) => relative(REPO_ROOT, path).split(sep).join("/");

/**
 * Lists every file below a repository-relative folder (recursive, sorted).
 * @param {string} rel folder
 * @returns {string[]} repository-relative file paths
 */
export function listFiles(rel) {
  if (!existsSync(abs(rel))) return [];
  return readdirSync(abs(rel), { recursive: true })
    .map((name) => `${rel}/${String(name).split(sep).join("/")}`)
    .filter((path) => statSync(abs(path)).isFile())
    .sort();
}

/**
 * Image paths referenced from a Markdown file, resolved to repository-relative paths.
 * @param {string} mdRel repository-relative Markdown path
 * @returns {string[]} referenced image paths
 */
export function imageRefs(mdRel) {
  const text = readFileSync(abs(mdRel), "utf8");
  const base = dirname(abs(mdRel));
  return [...text.matchAll(IMAGE_REF)].map((m) => repoRel(resolve(base, m[1])));
}

/** @returns {string[]} all inputs of the build, unique and sorted */
export function collectInputs() {
  const files = [...FIXED_INPUTS, ...INPUT_DIRS.flatMap(listFiles)];
  const images = files.filter((f) => f.endsWith(".md") && f.startsWith(SOURCE_DIR)).flatMap(imageRefs);
  return [...new Set([...files, ...images])].sort();
}

/**
 * SHA-256 of one input; text files are hashed with LF line endings.
 * @param {string} rel repository-relative path
 * @returns {string} hex digest
 */
export function hashFile(rel) {
  const bytes = readFileSync(abs(rel));
  const isText = TEXT_EXTENSIONS.some((ext) => rel.toLowerCase().endsWith(ext));
  const data = isText ? Buffer.from(bytes.toString("utf8").replace(/\r\n/g, "\n"), "utf8") : bytes;
  return createHash("sha256").update(data).digest("hex");
}

/**
 * Hashes all inputs; the combined hash covers paths and contents.
 * @returns {{ sourceHash: string, inputs: { path: string, sha256: string }[] }}
 */
export function hashInputs() {
  const inputs = collectInputs().map((path) => ({ path, sha256: hashFile(path) }));
  const combined = createHash("sha256");
  inputs.forEach((i) => combined.update(`${i.path}\0${i.sha256}\n`));
  return { sourceHash: combined.digest("hex"), inputs };
}
