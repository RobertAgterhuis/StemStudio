import {
  expectJobDevice,
  expectJobDeviceRecord,
  expectWorkerDevice,
  fakeGpuName,
  fakeGpuText,
  freshImportTimeoutMs,
  importFreshBand,
  outOfMemoryReason,
  requireGpuStack,
  type ExpectedJobDevice,
} from '../support/device';
import { expect, test } from '../support/test';

// Feature 003 device display against the fake GPU stack (STEMSTUDIO_SEPARATOR=fake,
// STEMSTUDIO_FAKE_DEVICE=gpu or gpu-oom; docs/testing/TEST-STRATEGY.md section 5). Excluded from
// `npm test`; run with `npm run test:gpu` once per fake device mode (CTO, T051).

const onFakeGpu: ExpectedJobDevice = { kind: 'gpu', fallback: false, text: fakeGpuText };

// The kind is not pinned: it depends on whether any later step of the job ran on the GPU.
const afterOutOfMemory: ExpectedJobDevice = { fallback: true, reason: outOfMemoryReason };

test.describe('device', { tag: '@gpu' }, () => {
  // T044: US3-1, FR-006. The fake GPU is reported in both fake GPU modes.
  test('worker device on the import page', async ({ api, importPage }) => {
    requireGpuStack(['gpu', 'gpu-oom']);
    expect(await api.getDevice()).toMatchObject({ reachable: true, kind: 'gpu', name: fakeGpuName });
    await importPage.goto();
    await expectWorkerDevice(importPage, 'gpu', fakeGpuText);
  });

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

  // T038: US2-3, FR-004, SC-004. The fake `gpu-oom` device makes the first model step of every
  // job run out of GPU memory; the step is repeated on the CPU and the job completes.
  test('fallback notice', { tag: '@fake-gpu-oom' }, async ({ api, trackPage, detail }, testInfo) => {
    requireGpuStack(['gpu-oom']);
    test.setTimeout(freshImportTimeoutMs);
    const track = await importFreshBand(api, `gpu-oom-${testInfo.project.name}`);

    await test.step('both jobs completed and record the CPU fallback', () => {
      expect(track.status).toBe('completed');
      expectJobDeviceRecord(track.device, afterOutOfMemory);
      expectJobDeviceRecord(track.detail.device, afterOutOfMemory);
    });

    await test.step('the track page shows the fallback reason for both jobs', async () => {
      await trackPage.goto(track.id);
      await expectJobDevice(trackPage.device, trackPage.deviceFallback, afterOutOfMemory);
      await expectJobDevice(detail.device, detail.deviceFallback, afterOutOfMemory);
    });
  });
});
