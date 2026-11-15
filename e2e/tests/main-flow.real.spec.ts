import { realJobTimeoutMs } from '../support/env';
import {
  importAndOpenMixer,
  practiceSettings,
  reloadAndVerify,
  saveNewMix,
  uniqueMixName,
  type FlowContext,
} from '../support/main-flow';
import { expect, test } from '../support/test';

// Main flow with real Demucs (T108). Only via `npm run test:real` against a stack started with
// STEMSTUDIO_SEPARATOR=demucs and a fresh data volume (otherwise a cached fake result is reused).
// The FLAC fixture keeps its source key distinct from the MP3 used by the fake suite.
const expectedStemCount = { htdemucs_6s: 6, htdemucs: 4 } as const;

test(
  'main flow with real Demucs separation',
  { tag: '@real' },
  async ({ page, api, importPage, trackPage, mixer, mixLibrary }, testInfo) => {
    test.setTimeout(realJobTimeoutMs + 5 * 60_000);
    const ctx: FlowContext = { page, api, importPage, trackPage, mixer, mixLibrary };
    const started = Date.now();
    const { trackId, stems } = await importAndOpenMixer(ctx, 'music_100bpm_20s.flac', realJobTimeoutMs);
    const track = await api.getTrack(trackId);
    testInfo.annotations.push({
      type: 'real-demucs',
      description: JSON.stringify({ model: track.model, seconds: (Date.now() - started) / 1000 }),
    });
    expect(['htdemucs_6s', 'htdemucs']).toContain(track.model);
    expect(stems).toHaveLength(expectedStemCount[track.model as keyof typeof expectedStemCount]);

    await mixer.play();
    await expect.poll(() => mixer.peak('master'), { timeout: 10_000 }).toBeGreaterThan(-60);
    await mixer.apply(practiceSettings);
    const mixName = uniqueMixName('Practice real', testInfo.project.name);
    const mixId = await saveNewMix(ctx, trackId, mixName);
    await reloadAndVerify(ctx, { trackId, mixId, mixName, stems, settings: practiceSettings });
  },
);
