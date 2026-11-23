import { expect as baseExpect } from '@playwright/test';
import { QualityPanel } from '../pages/quality-panel';
import { isOn } from '../support/controls';
import { ensureCancelledPhase, ensureDetailCompleted, fakeStems } from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { stemOf, type QualityReportDto, type TipDto } from '../support/insights-api';
import { guitarHardToIsolate, tipsAfterSplit, tipsBeforeSplit, tipTexts, useInsightsApi } from '../support/insights-suite';
import { expect, test } from '../support/test';

// Feature 020 US2 (T055; FR-005, FR-006, SC-003): tips that lead to an action, on the fake stack. Before a detail split
// (a band copy whose detail phase was cancelled) `other` gets "Split further" and `piano` "Mute this stem"; after the
// fake split (guitar: nothing further) guitar gets the honest "hard to isolate". No tip ever mentions the GPU, and every
// tip action exists for its track (SC-003). Waits poll the report the spec started (LESSONS 013/022).
const suite = useInsightsApi(fakeJobTimeoutMs + 2 * detailPhaseTimeoutMs + 120_000);

/** Every tip of a report with its stem. */
function allTips(report: QualityReportDto): { readonly channel: string; readonly tip: TipDto }[] {
  return report.stems.flatMap((stem) => stem.tips.map((tip) => ({ channel: stem.channel, tip })));
}

/** SC-003: no tip suggests the GPU; every text is the catalogue text; at most two per stem (FR-005). */
function expectHonestTips(report: QualityReportDto): void {
  for (const { channel, tip } of allTips(report)) {
    expect(tip.text, channel).not.toMatch(/gpu/iu);
    expect(tip.text, `${channel} ${tip.code}`).toMatch(tipTexts[tip.code]);
  }
  expect(report.stems.every((stem) => stem.tips.length <= 2)).toBe(true);
}

/** The child of the `use-detail-stem` tip of `parent` (its `open-detail` action). */
function openDetailChild(report: QualityReportDto, parent: string): string {
  const action = stemOf(report, parent).tips.find((t) => t.code === 'use-detail-stem')?.action;
  expect(action?.kind).toBe('open-detail');
  return String(action?.child);
}

test.describe('tips before a detail split (020 US2)', { tag: '@insights' }, () => {
  test('every stem gets the catalogue tips, none mentions the GPU (US2-5, FR-006, SC-003)', async ({ api, page }) => {
    const track = await ensureCancelledPhase(api, 'quality-tips');
    const report = await suite.insights().ensureQuality(track.id);

    for (const stem of fakeStems) {
      expect(stemOf(report, stem).tips.map((t) => t.code), stem).toEqual(tipsBeforeSplit[stem]);
    }
    expectHonestTips(report);
    const panel = new QualityPanel(page);
    await page.goto(`/tracks/${track.id}`);
    await panel.waitForReport(report);
    await expect(panel.panel).not.toContainText(/gpu/iu);
    for (const stem of fakeStems) {
      await expect(panel.tips(stem)).toHaveCount(tipsBeforeSplit[stem]?.length ?? 0);
    }
  });

  test('other -> "Split further" starts the detail separation and the report is remade (US2-1)', async ({ api, page }) => {
    const track = await ensureCancelledPhase(api, 'quality-split-further');
    const insights = suite.insights();
    const before = await insights.ensureQuality(track.id);
    const tip = stemOf(before, 'other').tips.find((t) => t.code === 'try-detail-separation');
    expect(tip?.action).toEqual({ kind: 'split-detail', channel: 'other', parent: 'other', child: null });
    const panel = new QualityPanel(page);
    await page.goto(`/tracks/${track.id}`);
    await panel.waitForReport(before);

    await expect(panel.tip('other', 'try-detail-separation')).toContainText('Split further (detail separation)');
    await panel.tipAction('other', 'try-detail-separation').click();
    const done = await api.waitForDetail(track.id, ['completed', 'failed'], detailPhaseTimeoutMs);
    expect(done.detail.status, done.detail.error ?? '').toBe('completed');

    const remade = await insights.waitForQuality(track.id, 60_000, before);
    await baseExpect.poll(async () => (await insights.quality(track.id)).stems.some((s) => s.channel === 'other.rest'), { timeout: 60_000 }).toBe(true);
    expect(remade.measuredAt).not.toBe(before.measuredAt);
    await expect(panel.part('row', 'other.rest')).toBeVisible({ timeout: 30_000 });
  });

  test('piano (probably not in this song) -> "Mute this stem" mutes it in the current mix (US2-4)', async ({ api, page }) => {
    const track = await ensureCancelledPhase(api, 'quality-mute');
    const report = await suite.insights().ensureQuality(track.id);
    const tip = stemOf(report, 'piano').tips.find((t) => t.code === 'not-in-song');
    expect(tip?.action).toEqual({ kind: 'mute', channel: 'piano', parent: null, child: null });
    const panel = new QualityPanel(page);
    await page.goto(`/tracks/${track.id}`);
    await panel.waitForReport(report);

    await panel.tipAction('piano', 'not-in-song').click();

    // Reading (TEST-STRATEGY 23.4): the action opens the mixer of the track with the stem muted in the current mix.
    await expect(page).toHaveURL(new RegExp(`/tracks/${track.id}/mixer`, 'u'));
    await baseExpect.poll(() => isOn(page.getByTestId('mute-piano')), { timeout: 30_000 }).toBe(true);
    await expect(page.getByTestId('quality-badge-piano')).toBeVisible();
  });
});

test.describe('tips after the detail split (020 US2)', { tag: '@insights' }, () => {
  test('guitar (nothing further to split) -> hard to isolate, without a button (US2-2)', async ({ api, page }) => {
    const track = await ensureDetailCompleted(api);
    const insights = suite.insights();
    await baseExpect.poll(async () => (await insights.quality(track.id)).stems.some((s) => s.channel === 'other.rest'), { timeout: 60_000 }).toBe(true);
    const report = await insights.waitForQuality(track.id);

    for (const stem of fakeStems) {
      expect(stemOf(report, stem).tips.map((t) => t.code), stem).toEqual(tipsAfterSplit[stem]);
    }
    expect(stemOf(report, 'guitar').tips[0]).toEqual({ code: 'hard-to-isolate', text: guitarHardToIsolate, action: null });
    expectHonestTips(report);
    const panel = new QualityPanel(page);
    await page.goto(`/tracks/${track.id}`);
    await panel.waitForReport(report);
    await expect(panel.tip('guitar', 'hard-to-isolate')).toHaveText(guitarHardToIsolate);
    await expect(panel.tipAction('guitar', 'hard-to-isolate')).toHaveCount(0);
  });

  test('other -> "Use the <child> detail stem instead" opens that detail stem in the mixer (US2-3)', async ({ api, page }) => {
    const track = await ensureDetailCompleted(api);
    const insights = suite.insights();
    await baseExpect.poll(async () => (await insights.quality(track.id)).stems.some((s) => s.channel === 'other.rest'), { timeout: 60_000 }).toBe(true);
    const report = await insights.waitForQuality(track.id);
    const child = openDetailChild(report, 'other');
    expect(['strings', 'brass', 'woodwinds']).toContain(child);
    // SC-003: the child the tip names is a kept detail stem of this track.
    expect(report.stems.some((s) => s.channel === `other.${child}`)).toBe(true);
    const panel = new QualityPanel(page);
    await page.goto(`/tracks/${track.id}`);
    await panel.waitForReport(report);

    await panel.tipAction('other', 'use-detail-stem').click();

    await expect(page).toHaveURL(new RegExp(`/tracks/${track.id}/mixer`, 'u'));
    await expect(page.getByTestId(`strip-other-${child}`)).toBeVisible({ timeout: 30_000 });
  });
});
