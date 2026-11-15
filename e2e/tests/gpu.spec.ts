import {
  expectJobDevice,
  expectJobDeviceRecord,
  fakeGpuName,
  fakeGpuText,
  freshImportTimeoutMs,
  importFreshBand,
  requireGpuStack,
  type ExpectedJobDevice,
} from '../support/device';
import { expect, test } from '../support/test';

// Feature 003 device display against the fake GPU stack (STEMSTUDIO_SEPARATOR=fake,
// STEMSTUDIO_FAKE_DEVICE=gpu or gpu-oom; docs/testing/TEST-STRATEGY.md section 5). Excluded from
// `npm test`; run with `npm run test:gpu` once per fake device mode (CTO, T051).

const onFakeGpu: ExpectedJobDevice = { kind: 'gpu', fallback: false, text: fakeGpuText };

test.describe('device', { tag: '@gpu' }, () => {
  // T033: US1-1, FR-006.
  test('device on the track page', { tag: '@fake-gpu' }, async ({ api, trackPage, detail }, testInfo) => {
    requireGpuStack(['gpu']);
    test.setTimeout(freshImportTimeoutMs);
    const track = await importFreshBand(api, `gpu-${testInfo.project.name}`);

    await test.step('the API records the fake GPU for both jobs', () => {
      expectJobDeviceRecord(track.device, onFakeGpu);
      expectJobDeviceRecord(track.detail.device, onFakeGpu);
      expect(track.device?.name).toBe(fakeGpuName);
    });

    await test.step('the track page shows "GPU (Fake GPU)" without a fallback notice', async () => {
      await trackPage.goto(track.id);
      await expectJobDevice(trackPage.device, trackPage.deviceFallback, onFakeGpu);
      await expectJobDevice(detail.device, detail.deviceFallback, onFakeGpu);
    });
  });
});
