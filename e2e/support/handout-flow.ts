/**
 * Steps shared by the 020 handout specs: find the handout item a UI click created (by its own new asset id, never by
 * "the newest one", LESSONS 013), download its PDF and check what SC-005 and FR-012/FR-013 promise for every page.
 *
 * @packageDocumentation
 */
import { expect } from '@playwright/test';
import type { AssetsApi } from './assets-api';
import { flat, readHandoutPdf, type HandoutPdf } from './handout-pdf';
import type { HandoutAssetDto, HandoutFamily, InsightsApi } from './insights-api';

/** data-model 6.6 `notice.footer`. */
export const footerNotice = 'For personal practice and teaching only';

/** The start of data-model 6.6 `notice.full` (the last page). */
export const fullNoticeStart = 'This handout was made with StemStudio for your own practice and lessons.';

/** Ids of the handout items of a track (or of every item with `trackId` null), for the "before" set. */
export async function handoutIds(assets: AssetsApi, trackId?: string): Promise<Set<string>> {
  const list = await assets.list(trackId === undefined ? {} : { trackId });
  return new Set(list.items.filter((item) => (item as unknown as HandoutAssetDto).kind === 'handout').map((item) => item.id));
}

/** The two clients the handout look-ups need. */
export interface HandoutClients {
  readonly assets: AssetsApi;
  readonly insights: InsightsApi;
}

/** Polls until a handout item of `family` appears that is not in `before`; returns its id. */
export async function newHandoutId({ assets, insights }: HandoutClients, before: ReadonlySet<string>, family: HandoutFamily, trackId?: string): Promise<string> {
  let found: string | undefined;
  await expect
    .poll(
      async () => {
        const now = await handoutIds(assets, trackId);
        for (const id of now) {
          if (!before.has(id) && (await insights.handout(id)).handout?.family === family) {
            found = id;
          }
        }
        return found !== undefined;
      },
      { timeout: 30_000, intervals: [250, 500, 1000] },
    )
    .toBe(true);
  return String(found);
}

/** The PDF of a ready handout item (role `pdf`). */
export async function handoutPdf(assets: AssetsApi, item: HandoutAssetDto): Promise<HandoutPdf> {
  const file = item.files.find((f) => f.role === 'pdf');
  expect(file, 'a handout has one pdf file').toBeDefined();
  expect(file?.contentType).toBe('application/pdf');
  return readHandoutPdf(await assets.bytes(String(file?.url)));
}

/** SC-005 / FR-012 / FR-013: page count, notice and page numbers on every page, the full notice last, metadata. */
export function expectHandoutPages(pdf: HandoutPdf, item: HandoutAssetDto): void {
  expect(pdf.pages).toBe(item.handout?.pages);
  pdf.texts.forEach((text, index) => {
    expect(flat(text), `page ${String(index + 1)}`).toContain(footerNotice);
    expect(flat(text), `page ${String(index + 1)}`).toMatch(new RegExp(`Page ${String(index + 1)} of ${String(pdf.pages)}`, 'iu'));
  });
  expect(flat(pdf.texts.at(-1) ?? '')).toContain(fullNoticeStart);
  expect(pdf.title, 'document title metadata').toBeTruthy();
  expect(pdf.lang, 'document language metadata').toBeTruthy();
  expect(pdf.outline.length, 'bookmarks').toBeGreaterThan(0);
}

/** All page texts as one flat string. */
export function pdfWords(pdf: HandoutPdf): string {
  return flat(pdf.texts.join(' '));
}
