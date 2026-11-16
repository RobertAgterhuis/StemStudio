/**
 * Page templating for the website: `{{name}}` takes a generated value (build.mjs), `{{t.path}}`
 * takes a string from the page's locale file (site/src/i18n/<lang>.json). Locale strings are
 * site-owned, trusted HTML and may themselves use `{{name}}` values (one level), so numbers read
 * from the repository (GPU minutes, page counts) stay out of the translations.
 * A Dutch version is added as site/src/i18n/nl.json plus an entry in site.json "locales".
 * @module
 */

/**
 * Looks up a dotted path in a locale dictionary.
 * @param {Record<string, unknown>} dict locale dictionary
 * @param {string} path e.g. "hero.title"
 * @returns {string} the string
 */
export function lookup(dict, path) {
  const value = path.split(".").reduce((node, key) => (node && typeof node === "object" ? node[key] : undefined), dict);
  if (typeof value !== "string") throw new Error(`missing locale string t.${path}`);
  return value;
}

/**
 * Replaces the placeholders of a template.
 * @param {string} template page source
 * @param {Record<string, string>} values generated values
 * @param {Record<string, unknown>} dict locale dictionary ({} for pages without translations)
 * @param {string} [name] template name for error messages
 * @returns {string} the filled page
 */
export function fill(template, values, dict, name = "template") {
  const value = (key) => {
    if (!(key in values)) throw new Error(`unknown placeholder {{${key}}} in ${name}`);
    return values[key];
  };
  const inner = (text) => text.replace(/\{\{([\w.-]+)\}\}/g, (_m, key) => value(key));
  return template.replace(/\{\{(t\.)?([\w.-]+)\}\}/g, (_m, t, key) => (t ? inner(lookup(dict, key)) : value(key)));
}

/**
 * The fenced code blocks of a Markdown text.
 * @param {string} markdown Markdown text
 * @returns {string[]} the code of each block (without the fences)
 */
export const fences = (markdown) => [...markdown.matchAll(/^```[^\n]*\n([\s\S]*?)^```/gm)].map((m) => m[1].replace(/\n$/, ""));

/**
 * Checks that every README feature bullet is placed on the home page.
 * @param {string[]} titles README "Features" bullet titles
 * @param {Record<string, string>} map bullet title -> element id on the home page
 * @param {Set<string>} ids element ids of the home page
 * @returns {string[]} problems
 */
export function featureCoverage(titles, map, ids) {
  const missing = titles.filter((t) => !(t in map)).map((t) => `README feature "${t}" is not mapped to a home-page section (site.json featureMap)`);
  const stale = Object.keys(map).filter((t) => !titles.includes(t)).map((t) => `site.json featureMap names "${t}", which is no longer a README feature`);
  const targets = Object.entries(map).filter(([, id]) => !ids.has(id)).map(([t, id]) => `README feature "${t}" points to #${id}, which is not on the home page`);
  return [...missing, ...stale, ...targets];
}

/** Words a showcase for a free, open-source tool never uses (owner brief: no sales or hype language). */
export const BANNED_WORDS = ["buy", "pricing", "price", "plans", "free trial", "subscribe", "revolutionary", "game-changing", "game changing", "unleash"];

/**
 * Finds banned words in the visible text of a page.
 * @param {string} html page
 * @returns {string[]} problems
 */
export function bannedWords(html) {
  const text = html
    .replace(/<(script|style)[\s\S]*?<\/\1>/g, " ")
    .replace(/<[^>]+>/g, " ")
    .toLowerCase();
  return BANNED_WORDS.filter((w) => new RegExp(`\\b${w}\\b`).test(text)).map((w) => `the home page uses "${w}" (no sales or hype language)`);
}
