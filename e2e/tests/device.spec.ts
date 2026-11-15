import {
  expectJobDevice,
  expectJobDeviceRecord,
  freshImportTimeoutMs,
  importFreshBand,
  requireCpuStack,
  type ExpectedJobDevice,
} from '../support/device';
import { expect, test } from '../support/test';

// Feature 003 on the default CPU stack (STEMSTUDIO_SEPARATOR=fake, STEMSTUDIO_FAKE_DEVICE unset or
// cpu): the device is shown as "CPU" without a fallback (US2-1, US3-1, FR-006). Part of `npm test`.

const onCpu: ExpectedJobDevice = { kind: 'cpu', fallback: false };

test.describe('device', { tag: '@device' }, () => {
  // T033 default-suite assertion: a fresh import on the CPU stack shows "CPU".
  test('the track page shows the CPU for both jobs', async ({ api, trackPage, detail }, testInfo) => {
    requireCpuStack();
    test.setTimeout(freshImportTimeoutMs);
    const track = await importFreshBand(api, `cpu-${testInfo.project.name}`);
    expectJobDeviceRecord(track.device, onCpu);
    expectJobDeviceRecord(track.detail.device, onCpu);
    expect(track.device?.name ?? null, 'no GPU name on the CPU').toBeNull();

    await trackPage.goto(track.id);
    await expectJobDevice(trackPage.device, trackPage.deviceFallback, onCpu);
    await expectJobDevice(detail.device, detail.deviceFallback, onCpu);
  });
});
