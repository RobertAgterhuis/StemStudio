import { RecordingPanel } from '../pages/recording-panel';
import { recordingLaunchOptions } from '../support/recording';
import { expect, test } from '../support/ux-test';
import { cellState, dynamicMasks, matrixThemes, requireVisualContainer, shotName } from '../support/visual';

// Feature 015 T085 visual baselines: the recording panel at desktop width and at 320 px, both themes, Advanced. Container
// only; the CTO generates and approves the baselines in the integration wave (TEST-STRATEGY 9 / 21).
requireVisualContainer();

test.use({ launchOptions: [async ({}, use) => use(recordingLaunchOptions('input-vocals.wav')), { scope: 'worker' }], permissions: ['microphone'] });

const widths = [{ name: 'desktop', width: 1280, height: 900 }, { name: '320', width: 320, height: 800 }] as const;

for (const theme of matrixThemes) {
  for (const size of widths) {
    test.describe(`recording panel / ${size.name} / ${theme}`, () => {
      test.use({ storageState: cellState('advanced', theme), viewport: { width: size.width, height: size.height } });

      test('matches its baseline', async ({ page, uxData }) => {
        const recording = new RecordingPanel(page);
        await page.goto(`/tracks/${uxData.plainTrack.id}/mixer`);
        await expect(page.getByTestId('transport-play')).toBeEnabled({ timeout: 60_000 });
        await recording.openWithInput();
        await expect(recording.panel).toHaveScreenshot(shotName(['recording-panel', size.name, theme]), {
          mask: [...dynamicMasks(page), recording.byId('meter-input')],
        });
      });
    });
  }
}
