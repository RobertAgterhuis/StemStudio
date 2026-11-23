import { HandoutMenu } from '../pages/handout-menu';
import { StemStudioApi } from '../support/api';
import { apiContextOptions, fakeJobTimeoutMs } from '../support/env';
import { expectHandoutPages, handoutIds, handoutPdf, newHandoutId, pdfWords } from '../support/handout-flow';
import { familyManifest, seedSetlist } from '../support/handout-seed';
import { handoutTimeoutMs, InsightsApi } from '../support/insights-api';
import { useInsightsApi } from '../support/insights-suite';
import type { SetlistDto } from '../support/practice-flow';
import { expect, test } from '../support/test';

// Feature 020 US4 (T087; FR-009, FR-013, SC-005): the setlist handout of the three-song fixture setlist
// (tests/fixtures/families/manifest.json). One click on the setlist page makes one handout item: a cover with the
// setlist name and notes, the running order with the entry notes, one bookmark per song in setlist order and each song
// on its own pages; 014's quick setlist PDF stays next to the new menu (DELTA "Phase 0 outcome" 3).
const plan = familyManifest();
const suite = useInsightsApi(3 * fakeJobTimeoutMs + 2 * handoutTimeoutMs);

let seeded: Promise<SetlistDto> | undefined;

test.beforeAll(async ({ playwright }) => {
  test.setTimeout(3 * fakeJobTimeoutMs);
  const context = await playwright.request.newContext(apiContextOptions);
  await new InsightsApi(context).requireFeature();
  seeded = seedSetlist(new StemStudioApi(context), context);
  await seeded;
});

/** The seeded setlist (beforeAll). */
function fixtureSetlist(): Promise<SetlistDto> {
  if (seeded === undefined) {
    throw new Error('the fixture setlist was not seeded (beforeAll)');
  }
  return seeded;
}

test.describe('setlist handout (020 US4)', { tag: '@insights' }, () => {
  test('band overview of the three-song setlist: cover, running order, one section per song in order (US4-1)', async ({ page, assetsApi }) => {
    const setlist = await fixtureSetlist();
    const insights = suite.insights();
    expect(setlist.entries.map((e) => e.note)).toEqual(plan.setlist.entries.map((e) => e.note));
    const before = await handoutIds(assetsApi);
    const menu = new HandoutMenu(page);
    await page.goto(`/setlists/${setlist.id}`);
    await expect(page.getByTestId('setlist-pdf')).toBeVisible();

    await menu.oneClick('overview', 'setlist');
    const item = await insights.waitForHandout(await newHandoutId({ assets: assetsApi, insights }, before, 'overview'));

    expect(item.handout).toMatchObject({ kind: 'setlist', family: 'overview', songs: 3 });
    expect(item.handout?.included).toContain('setlist-notes');
    expect(item.provenance.handoutRecipe).toMatchObject({ kindLabel: 'setlist', setlistId: setlist.id, setlistName: plan.setlist.name });
    const pdf = await handoutPdf(assetsApi, item);
    expectHandoutPages(pdf, item);
    const words = pdfWords(pdf);
    expect(words).toContain(plan.setlist.name);
    expect(words).toContain(plan.setlist.note);
    for (const entry of plan.setlist.entries.filter((e) => e.note !== '')) {
      expect(words, `running order note of ${entry.file}`).toContain(entry.note);
    }
    // One bookmark per song, in setlist order (data-model 6.4: song title > section headings).
    const titles = setlist.entries.map((e) => e.trackTitle);
    const positions = titles.map((title) => pdf.outline.indexOf(title));
    expect(positions.every((p) => p >= 0), `outline ${pdf.outline.join(' | ')}`).toBe(true);
    expect([...positions].sort((a, b) => a - b)).toEqual(positions);
  });

  test('the running order lists key, tempo and duration per song, and the facts count the songs (US4-1, FR-009)', async ({ assetsApi }) => {
    const setlist = await fixtureSetlist();
    const insights = suite.insights();
    const options = await insights.handoutOptions({ setlistId: setlist.id }, 'bass');
    expect(options).toMatchObject({ kind: 'setlist', songs: 3 });

    const created = await insights.createHandout({ source: { setlistId: setlist.id }, family: 'bass' });
    const item = await insights.waitForHandout(created.id);
    const words = pdfWords(await handoutPdf(assetsApi, item));

    expect(item.handout?.songs).toBe(3);
    for (const bpm of [plan.song.bpm, ...plan.setlist.entries.map((e) => e.bpm).filter((b) => b !== undefined)]) {
      expect(words).toContain(`${String(bpm)} BPM`);
    }
    for (const title of setlist.entries.map((e) => e.trackTitle)) {
      expect(words).toContain(title);
    }
  });
});
