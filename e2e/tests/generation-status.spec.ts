import { runId } from '../support/env';
import { sourceTrack } from '../support/generative-flow';
import { readEthicsCases, readFakeGenerator, readOwnLyrics, requireGeneratorMode, uniqueName, voiceFixturePath } from '../support/generative';
import { expectProblem } from '../support/generation-api';
import { expect, test } from '../support/generative-test';

// Feature 009 US6 and 010 V1: clear behaviour without a usable generator (009:T057; FR-010;
// SC-002, 010 SC-007). The default stack has no generator (`notInstalled`); the fake generator
// in mode `no-gpu` reports `unavailable` with the probe reason. Controls are disabled, not hidden,
// and describe the reason (ui-test-ids 1.8.0).
const notInstalled = /not installed/iu;
const consent = readEthicsCases().consent;

test.describe('without a generator', { tag: '@generation-status' }, () => {
  test.beforeEach(async ({ generationApi }) => {
    await generationApi.requireState('notInstalled');
  });

  test('the track page says "not installed" and Restyle is disabled with the reason (US6-1, SC-002)', async ({ api, generatedTrack, restyle, status }) => {
    const track = await sourceTrack(api);
    await generatedTrack.goto(track.id);
    await status.expectState('notInstalled');
    await expect(status.reason).toContainText(notInstalled);
    await expect(status.helpLink).toBeVisible();
    await status.expectDisabledWithReason(restyle.openButton, notInstalled);
  });

  test('the API refuses a restyle with 409 not-installed; the style library stays usable (US6-1, FR-010)', async ({ api, generationApi, status, styles }) => {
    const track = await sourceTrack(api);
    const jazz = await generationApi.preset('Jazz trio');
    expect((await expectProblem(await generationApi.restyle(track.id, { styleId: jazz.id, mode: 'range' }), 409)).type).toBe('not-installed');
    const own = await generationApi.duplicateStyle(jazz.id);
    expect(own.kind).toBe('user');
    await styles.goto();
    await status.expectState('notInstalled');
    await expect(styles.action('edit', own.id)).toBeEnabled();
    expect(await generationApi.deleteStyle(own.id)).toBe(204);
  });

  test('songs and voice profiles are disabled with the reason; lyrics can still be saved (010 V1)', async ({ lyricsList, song, status, voiceApi }, info) => {
    const sheet = await voiceApi.createLyricsOk(uniqueName('Offline lyrics', info.project.name, runId()), 'en', readOwnLyrics('sample-en.txt'));
    await lyricsList.goto();
    await expect(lyricsList.row(sheet.id)).toBeVisible();
    await song.goto(sheet.id);
    await status.expectState('notInstalled');
    await status.expectDisabledWithReason(song.generateButton, notInstalled);
    expect((await expectProblem(await voiceApi.createSong({ lyricsId: sheet.id, language: 'en', leadVoice: 'any' }), 409)).type).toBe('not-installed');
    expect(await voiceApi.deleteLyrics(sheet.id)).toBe(204);
  });

  test('a voice profile cannot be created without the singer (010 V1, FR-007)', async ({ status, voiceApi, voices }, info) => {
    const displayName = uniqueName('Offline voice', info.project.name, runId());
    const form = { displayName, personName: 'Test Singer', consentConfirmed: true, consentStatementVersion: consent.statementVersion };
    const problem = await expectProblem(await voiceApi.createProfileFromFile(voiceFixturePath('own-voice-20s.wav'), form), 409);
    expect(problem.type).toBe('voice-not-installed');
    expect((await voiceApi.listProfiles()).map((p) => p.displayName)).not.toContain(displayName);
    await voices.gotoNew();
    await status.expectVoiceState('notInstalled');
    await voices.fillConsent({ displayName, personName: 'Test Singer', consent: true });
    await status.expectDisabledWithReason(voices.saveButton, notInstalled);
  });
});

test.describe('generator without a usable GPU', { tag: '@generative' }, () => {
  test.beforeEach(async ({ generationApi }) => {
    requireGeneratorMode('no-gpu');
    await generationApi.requireState('unavailable');
  });

  test('Restyle is disabled with the probe reason; the API answers 409 gpu-required (US6-2, FR-010)', async ({ api, generatedTrack, generationApi, restyle, status }) => {
    const reason = (await generationApi.status()).reason ?? '';
    expect(reason).toMatch(new RegExp(readFakeGenerator().modes['no-gpu'].reasonPattern, 'u'));
    const track = await sourceTrack(api);
    await generatedTrack.goto(track.id);
    await status.expectState('unavailable');
    await expect(status.reason).toContainText(reason);
    await status.expectDisabledWithReason(restyle.openButton, reason);
    const jazz = await generationApi.preset('Jazz trio');
    const problem = await expectProblem(await generationApi.restyle(track.id, { styleId: jazz.id, mode: 'range' }), 409);
    expect([problem.type, problem.detail]).toEqual(['gpu-required', expect.stringContaining(reason) as unknown]);
  });

  test('song generation is disabled with the reason and refused by the API (010 FR-012)', async ({ generationApi, song, status, voiceApi }) => {
    const reason = (await generationApi.status()).reason ?? '';
    await song.goto();
    await status.expectState('unavailable');
    await status.expectDisabledWithReason(song.generateButton, reason);
    const problem = await expectProblem(await voiceApi.createSong({ lyricsText: readOwnLyrics('sample-en.txt'), language: 'en', leadVoice: 'any' }), 409);
    expect(problem.type).toBe('gpu-required');
  });
});
