/**
 * Generates the "Feature status" table of both manuals from docs/ROADMAP.md and docs/STATUS.md,
 * so the manuals never claim a feature that has not been integrated.
 * @module
 */
import { readFileSync } from "node:fs";
import { abs } from "./inputs.mjs";

/** Placeholder in the Markdown sources that is replaced by the generated table. */
export const STATUS_PLACEHOLDER = "<!-- feature-status -->";

/** Feature 001 is not a ROADMAP row (the roadmap starts after the MVP). */
const MVP = { id: "001", name: "MVP: import, separation, mixer and mixes" };

const ROADMAP_ROW = /^\|\s*(\d{3})\s*\|\s*([^|]+)\|/;

/**
 * Short feature name from a roadmap cell: the bold part, else the text before " (".
 * @param {string} cell roadmap "Feature" cell
 * @returns {string} name
 */
export function featureName(cell) {
  const bold = /\*\*(.+?)\*\*/.exec(cell);
  return (bold ? bold[1] : cell.split(" (")[0]).trim();
}

/**
 * Features listed in the roadmap (first table only), with the MVP first.
 * @param {string} roadmap ROADMAP.md text
 * @returns {{ id: string, name: string }[]} features
 */
export function roadmapFeatures(roadmap) {
  const firstTable = roadmap.split(/\n## /)[0];
  const rows = firstTable.split("\n").map((line) => ROADMAP_ROW.exec(line)).filter(Boolean);
  return [MVP, ...rows.map((m) => ({ id: m[1], name: featureName(m[2]) }))];
}

/**
 * Delivery date of a feature in STATUS.md ("## Feature NNN ... (date)"), or null.
 * The MVP counts as delivered by its "## Final verification (..., date)" section.
 * @param {string} status STATUS.md text
 * @param {string} id feature number
 * @returns {string | null} ISO date or null when not delivered
 */
export function deliveredOn(status, id) {
  const heading = id === MVP.id ? /^## Final verification[^\n]*?(\d{4}-\d{2}-\d{2})/m : new RegExp(`^## Feature (?:\\d{3} \\+ )*${id}\\b[^\\n]*?(\\d{4}-\\d{2}-\\d{2})`, "m");
  const match = heading.exec(status);
  return match ? match[1] : null;
}

/**
 * The generated Markdown table.
 * @param {string} roadmap ROADMAP.md text
 * @param {string} status STATUS.md text
 * @returns {string} Markdown table
 */
export function statusTable(roadmap, status) {
  const rows = roadmapFeatures(roadmap).map((f) => {
    const date = deliveredOn(status, f.id);
    const state = date ? `<span class="status status-done">Delivered</span> ${date}` : `<span class="status status-dev">In development</span>`;
    return `| ${f.id} | ${f.name} | ${state} |`;
  });
  return ["| # | Feature | Status |", "|---|---|---|", ...rows].join("\n");
}

/** @returns {string} the table built from the repository files */
export function repoStatusTable() {
  return statusTable(readFileSync(abs("docs/ROADMAP.md"), "utf8"), readFileSync(abs("docs/STATUS.md"), "utf8"));
}
