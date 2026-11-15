/**
 * Reads everything the website shows from the repository, so the site is regenerated from the
 * same sources as the manuals: README, ROADMAP, STATUS, ADRs, licences, manual PDFs, screenshots.
 * A source that no longer has the expected shape fails the build instead of silently dropping
 * a section.
 * @module
 */
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { abs, listFiles } from "../../docs/lib/inputs.mjs";
import { deliveredOn, featureName } from "../../docs/lib/status.mjs";
import { markdown } from "./md.mjs";

/** @param {string} rel repository-relative path @returns {string} file text with LF line endings */
export const readText = (rel) => readFileSync(abs(rel), "utf8").replace(/\r\n/g, "\n");

/**
 * The body of the first "## " section whose title starts with the given text.
 * @param {string} text Markdown text
 * @param {string} title title prefix
 * @returns {string} section body (without its heading)
 */
export function section(text, title) {
  const found = text.split(/^## /m).find((s) => s.startsWith(title));
  if (!found) throw new Error(`section "## ${title}" not found`);
  return found.slice(found.indexOf("\n") + 1);
}

/**
 * Splits a section at its "### " subsections.
 * @param {string} body section body
 * @returns {{ intro: string, subsections: Map<string, string> }} text before the first ### and each ### body
 */
export function subsections(body) {
  const [intro, ...rest] = body.split(/^### /m);
  return { intro, subsections: new Map(rest.map((s) => [s.slice(0, s.indexOf("\n")).trim(), s.slice(s.indexOf("\n") + 1)])) };
}

/** @param {string} text text @returns {string} the text with its first letter in upper case */
const capitalise = (text) => text.charAt(0).toUpperCase() + text.slice(1);

/**
 * The bullets of a Markdown list whose items start with a bold term: "- **Term** rest".
 * @param {string} body Markdown with a bullet list
 * @returns {{ title: string, html: string }[]} one entry per bullet
 */
export function boldBullets(body) {
  const md = markdown();
  const tokens = md.parse(body, {});
  return tokens
    .filter((t, i) => t.type === "inline" && tokens[i - 2]?.type === "list_item_open")
    .map((t) => /^\*\*(.+?)\*\*:?\s*([\s\S]*)$/.exec(t.content))
    .filter(Boolean)
    .map((m) => ({ title: m[1], html: md.renderInline(capitalise(m[2].replace(/\s+/g, " ").trim())) }));
}

/**
 * Roadmap features (MVP first) with description and delivery state.
 * @param {string} roadmap ROADMAP.md text
 * @param {string} status STATUS.md text
 * @param {Record<string, string>} fallbacks descriptions for rows without one
 * @returns {{ id: string, name: string, description: string, delivered: string | null }[]} features
 */
export function roadmapFeatures(roadmap, status, fallbacks) {
  const rows = roadmap.split(/\n## /)[0].split("\n").map((line) => /^\|\s*(\d{3})\s*\|\s*([^|]+)\|/.exec(line)).filter(Boolean);
  const all = [{ id: "001", cell: "**MVP: import, separation, mixer and mixes**" }, ...rows.map((m) => ({ id: m[1], cell: m[2] }))];
  return all.map(({ id, cell }) => {
    const after = /\*\*.+?\*\*:?\s*(.*)$/.exec(cell.trim());
    const description = (after?.[1] ?? "").trim() || fallbacks[id];
    if (!description) throw new Error(`feature ${id} has no description (add one to site/src/content/site.json)`);
    return { id, name: featureName(cell), description: capitalise(description), delivered: deliveredOn(status, id) };
  });
}

/**
 * GPU speed-up of the architect's reference-song spike recorded in STATUS.md.
 * @param {string} status STATUS.md text
 * @returns {{ gpu: number, cpu: number, factor: number, song: string }} minutes and factor
 */
export function gpuNumbers(status) {
  const m = /reference song ([\d:]+)[^|]*\|[^|]*?([\d.]+) min on GPU vs ([\d.]+) min CPU \(([\d.]+)x\)/.exec(status);
  if (!m) throw new Error("GPU spike numbers not found in docs/STATUS.md");
  return { song: m[1], gpu: Number(m[2]), cpu: Number(m[3]), factor: Number(m[4]) };
}

/** @returns {{ id: string, title: string, status: string, path: string }[]} the ADRs in number order */
export function adrs() {
  return listFiles("docs/decisions")
    .filter((path) => /\/ADR-\d+[^/]*\.md$/.test(path))
    .map((path) => {
      const text = readText(path);
      const heading = /^# (ADR-\d+):\s*(.+)$/m.exec(text);
      const state = /^- \*\*Status\*\*:\s*(\w+)/m.exec(text);
      return { id: heading?.[1] ?? path, title: heading?.[2]?.trim() ?? path, status: state?.[1] ?? "Unknown", path };
    });
}

/**
 * Headings of THIRD-PARTY-LICENSES.md with the number of table rows (components) below each.
 * @param {string} text the licence file
 * @returns {{ title: string, level: number, count: number }[]} sections that list components
 */
export function licenceSections(text) {
  const result = [];
  for (const line of text.split("\n")) {
    const heading = /^(#{2,3}) (.+)$/.exec(line);
    if (heading) result.push({ title: heading[2].replace(/`/g, ""), level: heading[1].length, count: 0 });
    else if (result.length > 0 && /^\|(?!\s*-)/.test(line)) result.at(-1).count += 1;
  }
  return result.map((s) => ({ ...s, count: Math.max(0, s.count - 1) })).filter((s) => s.count > 0 && !s.title.startsWith("Rejected"));
}

/** @returns {{ title: string, subtitle: string, audience: string, file: string, pages: number, bytes: number }[]} the public manuals */
export function manuals() {
  const manifest = JSON.parse(readText("docs/public/build-manifest.json"));
  return readdirSync(abs("docs/public/src"))
    .filter((d) => existsSync(abs(`docs/public/src/${d}/manual.json`)))
    .sort((a, b) => b.localeCompare(a))
    .map((d) => {
      const config = JSON.parse(readText(`docs/public/src/${d}/manual.json`));
      const output = manifest.outputs.find((o) => o.path === `docs/public/${config.output}`);
      if (!output) throw new Error(`docs/public/${config.output} is not in the build manifest`);
      return { title: config.title, subtitle: config.subtitle, audience: config.audience, file: config.output, pages: output.pages, bytes: output.bytes };
    });
}

/** @param {string[]} args git arguments @returns {string} trimmed output, "" when git is unavailable */
function git(args) {
  try {
    return execFileSync("git", args, { cwd: abs("."), encoding: "utf8" }).trim();
  } catch {
    return "";
  }
}

/** @returns {{ sha: string, date: string }} the commit the site is built from */
export function buildInfo() {
  const sha = process.env.GITHUB_SHA || git(["rev-parse", "HEAD"]) || "unknown";
  return { sha, date: git(["log", "-1", "--format=%cs"]) || new Date().toISOString().slice(0, 10) };
}
