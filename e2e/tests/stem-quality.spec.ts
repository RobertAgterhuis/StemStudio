import { expect as baseExpect } from '@playwright/test';
import { QualityPanel } from '../pages/quality-panel';
import { ensureDetailCompleted, fakeStems, keptStems } from '../support/detail';
import { detailPhaseTimeoutMs, fakeJobTimeoutMs } from '../support/env';
import { qualityDisclaimer, stemOf, type InsightsApi, type QualityReportDto } from '../support/insights-api';
import { fakeRatings, fakeReasons, useInsightsApi } from '../support/insights-suite';
import { levelState } from '../support/preferences';
import { expect, test } from '../support/test';

// Feature 020 US1 (T048; FR-001..FR-004, FR-016, SC-001, quickstart V1-V3): the stem quality report on the fake stack.
// The fake analyzer rates by channel (data-model 2.6), so every rating, reason and level rule is reachable on the plain
// band fixture. Waits poll the report the spec (or the separation) started: by its `measuredAt`, never by a status
// alone (LESSONS 013/022).
const suite = useInsightsApi(fakeJobTimeoutMs + detailPhaseTimeoutMs + 120_000);

/** The detail channels of the fake split: kept stems and the rest of every split parent (`other.strings`, `other.rest`). */
const detailChannels: readonly string[] = fakeStems.flatMap((parent) => {
  const kept = keptStems(parent);
  return kept.length === 0 ? [] : [...kept, 'rest'].map((child) => `${parent}.${child}`);
});

/** The words of each rating (spec US1-1: Good, Fair, Poor, Probably not in this song, Not rated). */
const ratingText: Readonly<Record<string, string>> = { good: 'good', fair: 'fair', poor: 'poor', 'not-present': 'not in this song', unknown: 'not rated' };

/** The rating words of a stem, as shown and as said by its accessible name. */
function ratingWords(stem: string): RegExp {
  return new RegExp(ratingText[String(fakeRatings[stem])] ?? '^$', 'iu');
}

/** Polls until the report holds a row for every detail channel (the automatic report after the detail phase). */
async function reportWithDetail(insights: InsightsApi, trackId: string): Promise<QualityReportDto> {
  const has = async (): Promise<boolean> => {
    const report = await insights.quality(trackId);
    return report.status === 'ready' && detailChannels.every((c) => report.stems.some((s) => s.channel === c));
  };
  await baseExpect.poll(has, { timeout: 60_000, intervals: [500, 1000] }).toBe(true);
  return insights.quality(trackId);
}

test.describe('stem quality report (020 US1)', { tag: '@insights' }, () => {
  test('after a separation every stem is rated with a reason and the disclaimer (US1-1, US1-3)', async ({ api, page }) => {
    const track = await api.ensureCompleted('detail_10s_band.wav', fakeJobTimeoutMs);
    const report = await suite.insights().ensureQuality(track.id);

    expect(report.disclaimer).toBe(qualityDisclaimer);
    expect(report.analyzerVersion).toBe('fake-1');
    for (const stem of fakeStems) {
      expect(stemOf(report, stem).rating, stem).toBe(fakeRatings[stem]);
      expect(stemOf(report, stem).reason, stem).toBe(fakeReasons[stem]);
    }
    const panel = new QualityPanel(page);
    await page.goto(`/tracks/${track.id}`);
    await panel.waitForReport(report);
    await expect(panel.byId('quality-disclaimer')).toHaveText(qualityDisclaimer);
    for (const stem of fakeStems) {
      await expect(panel.part('reason', stem)).toHaveText(String(fakeReasons[stem]));
      // R20-1: the rating carries its word (never colour alone); the accessible name holds the word too.
      await expect(panel.part('rating', stem)).toContainText(ratingWords(stem));
      await expect(panel.part('rating', stem)).toHaveAttribute('aria-label', ratingWords(stem));
    }
  });

  test('after a detail split every kept detail stem and its rest is rated under its parent (US1-2)', async ({ api, page }) => {
    const track = await ensureDetailCompleted(api);
    const report = await reportWithDetail(suite.insights(), track.id);

    for (const channel of detailChannels) {
      const [parent, child] = channel.split('.');
      const stem = stemOf(report, channel);
      expect(stem.parent, channel).toBe(parent);
      expect(stem.rating, channel).toBe(child === 'rest' ? 'fair' : 'good');
    }
    const panel = new QualityPanel(page);
    await page.goto(`/tracks/${track.id}`);
    await panel.waitForReport(report);
    for (const channel of detailChannels) {
      await expect(panel.part('row', channel)).toBeVisible();
    }
  });
});

test.describe('levels of the quality panel (020 US1-4, R20-2)', { tag: '@insights' }, () => {
  test.describe('Beginner', () => {
    test.use({ storageState: levelState('beginner') });

    test('rating, icon text and tip only', async ({ api, page }) => {
      const track = await api.ensureCompleted('detail_10s_band.wav', fakeJobTimeoutMs);
      const report = await suite.insights().ensureQuality(track.id);
      const panel = new QualityPanel(page);
      await page.goto(`/tracks/${track.id}`);
      await panel.waitForReport(report);

      await expect(panel.part('rating', 'vocals')).toBeVisible();
      await expect(panel.tips('vocals').first()).toBeVisible();
      for (const part of ['bleed', 'artefacts', 'index'] as const) {
        await expect(panel.part(part, 'vocals'), part).toHaveCount(0);
      }
      await expect(panel.byId('quality-analyzer-version')).toHaveCount(0);
    });
  });

  test.describe('Advanced', () => {
    test.use({ storageState: levelState('advanced') });

    test('adds the bleed and artefact levels and the main bleed source', async ({ api, page }) => {
      const track = await api.ensureCompleted('detail_10s_band.wav', fakeJobTimeoutMs);
      const report = await suite.insights().ensureQuality(track.id);
      const panel = new QualityPanel(page);
      await page.goto(`/tracks/${track.id}`);
      await panel.waitForReport(report);

      await expect(panel.part('bleed', 'vocals')).toContainText(/medium/iu);
      await expect(panel.part('bleed', 'vocals')).toContainText(/drums/iu);
      await expect(panel.part('artefacts', 'guitar')).toContainText(/high/iu);
      await expect(panel.part('index', 'vocals')).toHaveCount(0);
      await expect(panel.byId('quality-analyzer-version')).toHaveCount(0);
    });
  });

  test.describe('Expert', () => {
    test.use({ storageState: levelState('expert') });

    test('adds the indices, the reliability labels and the analyzer version', async ({ api, page }) => {
      const track = await api.ensureCompleted('detail_10s_band.wav', fakeJobTimeoutMs);
      const report = await suite.insights().ensureQuality(track.id);
      const panel = new QualityPanel(page);
      await page.goto(`/tracks/${track.id}`);
      await panel.waitForReport(report);

      const vocals = stemOf(report, 'vocals');
      await expect(panel.part('index', 'vocals')).toHaveAttribute('data-bleed-reliability', vocals.bleed.reliability);
      await expect(panel.part('index', 'vocals')).toHaveAttribute('data-artefact-reliability', vocals.artefacts.reliability);
      // FR-004: until G1 runs every label is "rough indication" (indicative).
      expect(report.stems.every((s) => s.bleed.reliability === 'indicative' && s.artefacts.reliability === 'indicative')).toBe(true);
      await expect(panel.byId('quality-analyzer-version')).toContainText('fake-1');
    });
  });
});

test.describe('check again (020 FR-002)', { tag: '@insights' }, () => {
  test('"Check again" on an existing track makes a new report and the panel shows it', async ({ api, page }) => {
    const track = await api.ensureCompleted('detail_10s_band.wav', fakeJobTimeoutMs);
    const insights = suite.insights();
    const before = await insights.ensureQuality(track.id);
    const panel = new QualityPanel(page);
    await page.goto(`/tracks/${track.id}`);
    await panel.waitForReport(before);

    await expect(panel.check).toBeEnabled();
    await panel.check.click();
    const after = await insights.waitForQuality(track.id, 60_000, before);

    expect(after.measuredAt).not.toBe(before.measuredAt);
    await panel.waitForReport(after);
    await expect(panel.check).toBeEnabled();
    await expect(panel.byId('quality-outdated')).toHaveCount(0);
  });
});
