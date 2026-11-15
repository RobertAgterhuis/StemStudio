/**
 * HTML fragments for the generated sections of site/src/index.html. Colours are never written
 * here: elements get stem classes (s-vocals ... s-other) that site.css maps to the app tokens.
 * @module
 */
import { escapeHtml, markdown, repoLink } from "./md.mjs";

/** Stem order of the app (tokens.css); features and strands cycle through it. */
export const STEMS = ["vocals", "drums", "bass", "guitar", "piano", "other"];

/** @param {number} i index @returns {string} stem class for the i-th item */
export const stemClass = (i) => `s-${STEMS[i % STEMS.length]}`;

/** @param {string} iso ISO date @returns {string} e.g. "15 November 2026" */
export const longDate = (iso) => new Date(`${iso}T12:00:00Z`).toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });

/**
 * Feature cards: README feature bullets, then the delivered roadmap features.
 * @param {{ title: string, html: string }[]} bullets README bullets
 * @param {{ id: string, name: string, description: string, delivered: string | null }[]} features roadmap features
 * @returns {string} HTML
 */
export function featureCards(bullets, features) {
  const md = markdown();
  const delivered = features.filter((f) => f.delivered && f.id !== "001");
  const cards = [
    ...bullets.map((b) => ({ title: b.title, html: b.html, tag: "" })),
    ...delivered.map((f) => ({ title: f.name, html: md.renderInline(f.description), tag: `Feature ${f.id}` })),
  ];
  const items = cards.map((c, i) => {
    const tag = c.tag ? `<p class="strip-tag">${c.tag}</p>` : "";
    return `<li class="strip ${stemClass(i)}"><h3 class="strip-name"><span class="dot" aria-hidden="true"></span>${escapeHtml(c.title)}</h3>${tag}<p>${c.html}</p></li>`;
  });
  return `<ul class="strips" role="list">${items.join("")}</ul>`;
}

/**
 * One screenshot pair; only the image of the active theme is displayed (and fetched).
 * @param {{ id: string, title: string, caption: string, images: Record<"light" | "dark", { src: string, width: number, height: number }> }} shot screenshot
 * @returns {string} HTML
 */
function shot(shot) {
  const img = (theme) => {
    const { src, width, height } = shot.images[theme];
    const alt = `${shot.title}, ${theme} theme`;
    return `<a class="shot-link only-${theme}" href="${src}"><img src="${src}" width="${width}" height="${height}" alt="${escapeHtml(alt)}" loading="lazy" decoding="async"></a>`;
  };
  return `<li class="shot shot-${shot.id}"><figure>${img("dark")}${img("light")}<figcaption><strong>${escapeHtml(shot.title)}</strong> ${escapeHtml(shot.caption)}</figcaption></figure></li>`;
}

/** @param {Parameters<typeof shot>[0][]} shots screenshots @returns {string} HTML */
export const gallery = (shots) => `<ul class="shots" role="list">${shots.map(shot).join("")}</ul>`;

/**
 * Roadmap timeline: one grey strand, delivered features branch off in a stem colour.
 * @param {{ id: string, name: string, description: string, delivered: string | null }[]} features roadmap features
 * @returns {string} HTML
 */
export function timeline(features) {
  const md = markdown();
  const items = features.map((f, i) => {
    const state = f.delivered
      ? `<p class="tl-state is-done">Delivered <time datetime="${f.delivered}">${longDate(f.delivered)}</time></p>`
      : `<p class="tl-state is-dev">In development</p>`;
    const cls = f.delivered ? `tl-item is-delivered ${stemClass(i)}` : "tl-item";
    return `<li class="${cls}"><p class="tl-no">${f.id}</p><div class="tl-body"><h3 class="tl-name">${escapeHtml(f.name)}</h3>${state}<p class="tl-desc">${md.renderInline(f.description)}</p></div></li>`;
  });
  const done = features.filter((f) => f.delivered).length;
  return `<p class="tl-summary"><strong>${done} of ${features.length}</strong> features delivered.</p><ol class="timeline" role="list">${items.join("")}</ol>`;
}

/**
 * GPU comparison: the README text plus two bars drawn to scale from the STATUS numbers.
 * @param {{ gpu: number, cpu: number, factor: number, song: string }} numbers STATUS spike numbers
 * @param {string} readmeGpu README "NVIDIA GPU (optional)" subsection (Markdown)
 * @returns {string} HTML
 */
export function gpuSection(numbers, readmeGpu) {
  const width = (minutes) => Math.max(8, Math.round((minutes / numbers.cpu) * 1000));
  const bar = (cls, label, minutes) =>
    `<div class="bar-row"><dt>${label}</dt><dd><svg class="bar-track" viewBox="0 0 1000 10" preserveAspectRatio="none" aria-hidden="true" focusable="false"><rect class="bar ${cls}" width="${width(minutes)}" height="10"/></svg><span class="bar-value">${minutes} min</span></dd></div>`;
  const chart = `<dl class="bars">${bar("bar-cpu", "CPU", numbers.cpu)}${bar("bar-gpu", "NVIDIA GPU", numbers.gpu)}</dl>`;
  const figure = `<figure class="gpu-figure"><p class="gpu-factor"><span class="gpu-x">${numbers.factor}×</span> faster separation</p>${chart}<figcaption>Reference song of ${numbers.song}, standard and detail separation together, measured on the reference machine with an RTX 5070 Ti. The stems are identical.</figcaption></figure>`;
  return `<div class="gpu">${figure}<div class="prose">${markdown().render(readmeGpu)}</div></div>`;
}

/** @param {number} bytes size @returns {string} e.g. "1.4 MB" */
const megabytes = (bytes) => `${(bytes / 1_000_000).toFixed(1)} MB`;

/**
 * Download tiles for the public manuals.
 * @param {{ title: string, subtitle: string, audience: string, file: string, pages: number, bytes: number }[]} list manuals
 * @returns {string} HTML
 */
export function manualTiles(list) {
  const items = list.map(
    (m, i) =>
      `<li class="manual ${stemClass(i * 3)}"><h3>${escapeHtml(m.title)}</h3><p>${escapeHtml(m.subtitle)}</p><p class="manual-for">For ${escapeHtml(m.audience.charAt(0).toLowerCase() + m.audience.slice(1))}.</p><a class="button" href="manuals/${m.file}" download>Download the ${escapeHtml(m.title.toLowerCase())} <span class="meta">(PDF, ${m.pages} pages, ${megabytes(m.bytes)})</span></a></li>`,
  );
  return `<ul class="manuals" role="list">${items.join("")}</ul>`;
}

/**
 * Licence summary: the model-weights notice first, then the component groups.
 * @param {string} notice README "Licences" section (Markdown)
 * @param {{ title: string, level: number, count: number }[]} groups licence file sections
 * @returns {string} HTML
 */
export function licences(notice, groups) {
  const rows = groups.map((g) => `<li class="lic-${g.level}"><span>${escapeHtml(g.title)}</span><span class="count">${g.count}</span></li>`);
  const file = repoLink("docs/THIRD-PARTY-LICENSES.md");
  return `<div class="lic"><div class="notice notice-warn">${markdown().render(notice)}</div><div class="lic-list"><h3>What StemStudio is built on</h3><p>Every component with its version and licence is listed in <a href="${file}">THIRD-PARTY-LICENSES.md</a>. Number of components per group:</p><ul role="list">${rows.join("")}</ul><p>StemStudio's own code is under the <a href="${repoLink("LICENSE")}">MIT licence</a>.</p></div></div>`;
}

/**
 * ADR list with links to the records on GitHub.
 * @param {{ id: string, title: string, status: string, path: string }[]} list ADRs
 * @returns {string} HTML
 */
export function adrList(list) {
  const md = markdown();
  const items = list.map(
    (a) => `<li><a href="${repoLink(a.path)}"><span class="adr-id">${a.id}</span> ${md.renderInline(a.title)}</a><span class="adr-state is-${a.status.toLowerCase()}">${escapeHtml(a.status)}</span></li>`,
  );
  return `<ol class="adrs" role="list">${items.join("")}</ol>`;
}
