import { HandoutMenu } from '../pages/handout-menu';
import { ensureCancelledPhase } from '../support/detail';
import { apiContextOptions } from '../support/env';
import { expectHandoutPages, handoutIds, handoutPdf, newHandoutId, pdfWords } from '../support/handout-flow';
import { familyManifest, seedFixtureSong, seedTimeoutMs, type SeededSong } from '../support/handout-seed';
import { handoutFamilies, handoutTimeoutMs, InsightsApi, type HandoutFamily } from '../support/insights-api';
import { useInsightsApi } from '../support/insights-suite';
import { StemStudioApi } from '../support/api';
import { PracticeFlowApi } from '../support/practice-flow';
import { levelState } from '../support/preferences';
import { expect, test } from '../support/test';

// Feature 020 US3 (T080; FR-008, FR-010..FR-015, SC-005, SC-006): one-click lesson handouts for all seven families on the
// per-family fixture song (tests/fixtures/families/manifest.json, seeded once per worker through the API). Each test
// waits on the handout item its own click created (new asset id) and reads the PDF with pypdf: page count, notice and
// "Page n of N" on every page, the full notice last, bookmarks, title and language metadata, the practice note.
// Every instrument family counts equally: one test per family, the same checks for each.
const plan = familyManifest();
const suite = useInsightsApi(seedTimeoutMs + 3 * handoutTimeoutMs);

let seeded: Promise<SeededSong> | undefined;

/** The fixture song, seeded once per worker (beforeAll). */
function fixtureSong(): Promise<SeededSong> {
  if (seeded === undefined) {
    throw new Error('the fixture song was not seeded (beforeAll)');
  }
  return seeded;
}

test.beforeAll(async ({ playwright }) => {
  test.setTimeout(seedTimeoutMs);
  const context = await playwright.request.newContext(apiContextOptions);
  await new InsightsApi(context).requireFeature();
  seeded = seedFixtureSong(new StemStudioApi(context), context);
  await seeded;
});

/** Sorted copy for set-like comparisons (the API keeps its own order). */
function sorted(values: readonly string[]): string[] {
  return [...values].sort();
}

test.describe('one-click lesson handout per family (020 US3, SC-006)', { tag: '@insights' }, () => {
  for (const family of handoutFamilies) {
    test(`${family}: the handout has the family's default ingredients, notice and bookmarks (US3-1, US3-2, US3-4)`, async ({ page, assetsApi }) => {
      const { track } = await fixtureSong();
      const familyPlan = plan.families[family];
      const insights = suite.insights();
      const before = await handoutIds(assetsApi, track.id);
      const menu = new HandoutMenu(page);
      await page.goto(`/tracks/${track.id}`);

      await menu.oneClick(family);
      await expect(page.getByTestId('handout-saved')).toBeVisible({ timeout: 30_000 });
      const id = await newHandoutId({ assets: assetsApi, insights }, before, family, track.id);
      const item = await insights.waitForHandout(id);

      expect(item.kind).toBe('handout');
      expect(item.handout?.kind).toBe('song');
      expect(sorted(item.handout?.included ?? []), 'data-model 6.2 defaults').toEqual(sorted(familyPlan.expectedIncluded));
      expect(item.name).toContain(familyPlan.label);
      const pdf = await handoutPdf(assetsApi, item);
      expectHandoutPages(pdf, item);
      expect(pdfWords(pdf)).toContain(familyPlan.practiceNote);
      // The fake analyser labels by bars ("Verse 1", ...), not the song's composed form: read what the stack holds.
      for (const section of (await new PracticeFlowApi(suite.context()).sections(track.id)).sections) {
        expect(pdfWords(pdf), `form chart row ${section.label}`).toContain(section.label);
      }
      expect(pdf.outline).toContain('Practice notes');
      await page.goto('/library?view=all');
      await expect(page.getByTestId(`asset-handout-${id}`)).toHaveAttribute('data-family', family);
    });
  }
});

test.describe('handout options (020 US3)', { tag: '@insights' }, () => {
  test('melodic in B-flat: the transposed part, transposed chords and the facts note (US3-5)', async ({ assetsApi }) => {
    const { track, scores } = await fixtureSong();
    const insights = suite.insights();
    const melodic = plan.families.melodic;
    const options = await insights.handoutOptions({ trackId: track.id }, 'melodic', 'b-flat');
    expect(options.transpositions).toEqual(['c', 'b-flat', 'e-flat', 'f']);
    const selected = options.notation.filter((n) => n.defaultSelected);
    expect(selected.map((n) => n.assetId)).toContain(scores.melodic[0]);
    expect(selected.every((n) => n.kind === 'transposed' && n.transposition === 'b-flat')).toBe(true);

    const created = await insights.createHandout({ source: { trackId: track.id }, family: 'melodic', transposition: 'b-flat', paper: 'letter' });
    const item = await insights.waitForHandout(created.id);

    expect(item.handout).toMatchObject({ family: 'melodic', transposition: 'b-flat', paper: 'letter' });
    const pdf = await handoutPdf(assetsApi, item);
    expectHandoutPages(pdf, item);
    expect(pdfWords(pdf)).toContain(String(melodic.transpositionNote));
  });

  test('a practice note is shown in that family only (FR-015)', async ({ assetsApi }) => {
    const { track } = await fixtureSong();
    const insights = suite.insights();
    const notes = await insights.practiceNotes(track.id);
    expect(notes.map((n) => n.family).sort()).toEqual([...handoutFamilies].sort());

    const created = await insights.createHandout({ source: { trackId: track.id }, family: 'drums' });
    const pdf = await handoutPdf(assetsApi, await insights.waitForHandout(created.id));

    expect(pdfWords(pdf)).toContain(plan.families.drums.practiceNote);
    expect(pdfWords(pdf)).not.toContain(plan.families.bass.practiceNote);
  });

  test('"Make again" posts the recipe and makes a new item (FR-014)', async ({ page, assetsApi }) => {
    const { track } = await fixtureSong();
    const insights = suite.insights();
    const first = await insights.waitForHandout((await insights.createHandout({ source: { trackId: track.id }, family: 'keys' })).id);
    const before = await handoutIds(assetsApi, track.id);
    await page.goto('/library?view=all');

    await page.getByTestId(`asset-handout-again-${first.id}`).click();
    const again = await insights.waitForHandout(await newHandoutId({ assets: assetsApi, insights }, before, 'keys', track.id));

    expect(again.id).not.toBe(first.id);
    expect(again.provenance.handoutRecipe).toEqual(first.provenance.handoutRecipe);
  });
});

test.describe('missing ingredients (020 US3-3, FR-010)', { tag: '@insights' }, () => {
  test.use({ storageState: levelState('beginner') });

  test('a bass handout without notation is still made and lists what is not included', async ({ api, page, assetsApi }) => {
    const track = await ensureCancelledPhase(api, 'handout-missing');
    const insights = suite.insights();
    const options = await insights.handoutOptions({ trackId: track.id }, 'bass');
    expect(options.ingredients.find((i) => i.code === 'notation')).toMatchObject({ status: 'missing' });
    const before = await handoutIds(assetsApi, track.id);
    const menu = new HandoutMenu(page);
    await page.goto(`/tracks/${track.id}`);

    await menu.oneClick('bass' satisfies HandoutFamily);
    await expect(page.getByTestId('handout-missing')).toContainText(/Not included/iu, { timeout: 30_000 });
    const item = await insights.waitForHandout(await newHandoutId({ assets: assetsApi, insights }, before, 'bass', track.id));

    expect(item.handout?.omitted).toContainEqual({ code: 'notation', reason: 'not-available' });
    expect(item.handout?.included).not.toContain('notation');
    expectHandoutPages(await handoutPdf(assetsApi, item), item);
    await page.goto('/library?view=all');
    await expect(page.getByTestId(`asset-handout-omitted-${item.id}`)).toContainText(/Not included/iu);
  });
});
