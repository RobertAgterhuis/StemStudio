import { expect, type Locator } from '@playwright/test';
import { channelTestId, type QualityReportDto, type TipCode } from '../support/insights-api';
import { TestIdPage } from './test-id-page';

/** Stem quality panel of the track page and the mixer badges (020 US1/US2, ui-test-ids 1.19.0). */
export class QualityPanel extends TestIdPage {
  /** Region "Stem quality" (`data-status`). */
  get panel(): Locator {
    return this.byId('quality-panel');
  }

  /** "Check stem quality" / "Check again". */
  get check(): Locator {
    return this.byId('quality-check');
  }

  /** `quality-<part>-<channel>` of a ChannelId (`other.rest` -> `other-rest`). */
  part(part: 'row' | 'rating' | 'previous' | 'reason' | 'bleed' | 'artefacts' | 'index', channel: string): Locator {
    return this.byId(`quality-${part}-${channelTestId(channel)}`);
  }

  /** One tip of a stem. */
  tip(channel: string, code: TipCode): Locator {
    return this.byId(`quality-tip-${channelTestId(channel)}-${code}`);
  }

  /** The button of a tip (exists only when the tip has an action). */
  tipAction(channel: string, code: TipCode): Locator {
    return this.byId(`quality-tip-action-${channelTestId(channel)}-${code}`);
  }

  /** Every tip element of a stem. */
  tips(channel: string): Locator {
    return this.page.locator(`[data-testid^="quality-tip-${channelTestId(channel)}-"]:not([data-testid^="quality-tip-action-"])`);
  }

  /** Waits until the panel shows `report` (status ready and one row per stem with its rating). */
  async waitForReport(report: QualityReportDto, timeoutMs = 30_000): Promise<void> {
    await expect(this.panel).toHaveAttribute('data-status', 'ready', { timeout: timeoutMs });
    for (const stem of report.stems) {
      await expect(this.part('row', stem.channel)).toHaveAttribute('data-rating', stem.rating, { timeout: timeoutMs });
    }
  }
}
