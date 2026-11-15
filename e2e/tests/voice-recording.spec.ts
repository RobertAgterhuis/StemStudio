import { fakeMicrophoneLaunchOptions, microphonePermissions } from '../support/fake-microphone';
import { requireChromium } from '../support/feature-skips';
import { fakeJobTimeoutMs, runId } from '../support/env';
import { present, readEthicsCases, requireGeneratorMode, uniqueName, voiceFixtureFile, voiceFixturePath } from '../support/generative';
import { expect, test } from '../support/generative-test';

// Feature 010 US3-1 with the browser microphone (010:T052; FR-005; quickstart V2): Chromium
// records the own-voice fixture through its fake capture device; Firefox records its built-in
// fake tone. Launch options are worker-scoped, so they are set for the whole file.
const consent = readEthicsCases().consent;

test.use({
  launchOptions: [async ({ browserName }, use) => use(fakeMicrophoneLaunchOptions(browserName, voiceFixtureFile('own-voice-20s.wav'))), { scope: 'worker' }],
});

test.beforeEach(async ({ generationApi }) => {
  test.setTimeout(fakeJobTimeoutMs + 120_000);
  requireGeneratorMode('ready', 'oom', 'slow');
  await generationApi.requireVoiceState('ready');
  voiceFixturePath('own-voice-20s.wav');
});

test.describe('recording a voice sample', { tag: '@generative' }, () => {
  test('a recording with consent becomes a profile with source "recorded" (US3-1, FR-005, FR-006)', async ({ browserName, context, voiceApi, voices }, info) => {
    await context.grantPermissions(microphonePermissions(browserName));
    const displayName = uniqueName('Recorded voice', info.project.name, runId());
    await voices.gotoNew();
    await voices.record(15);
    await voices.fillConsent({ displayName, personName: 'Test Singer', consent: true });
    await voices.saveButton.click();
    await expect.poll(async () => (await voiceApi.listProfiles()).map((p) => p.displayName)).toContain(displayName);
    const profile = present((await voiceApi.listProfiles()).find((p) => p.displayName === displayName), 'the recorded profile');
    expect([profile.source, profile.consent.statementVersion]).toEqual(['recorded', consent.statementVersion]);
    expect(profile.durationSeconds).toBeGreaterThanOrEqual(10);
    expect(await voiceApi.deleteProfile(profile.id, true)).toBe(204);
  });

  test('a denied microphone shows the reason and upload stays possible (edge case, FR-005)', async ({ browserName, voices }) => {
    requireChromium(browserName);
    await voices.gotoNew();
    await voices.byId('voice-record-start').click();
    await expect(voices.recordUnavailable).toBeVisible();
    await expect(voices.byId('voice-upload')).toBeEnabled();
  });
});
