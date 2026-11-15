import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { requireEnv } from '../support/feature-skips';
import { fakeJobTimeoutMs, runId } from '../support/env';
import { expectAiLabels, generationTimeoutMs, restyledTake, songTake, sourceTrack, takeOf } from '../support/generative-flow';
import {
  present, readEthicsCases, readOwnLyrics, readVoiceManifest, requireGeneratorMode, uniqueName, voiceFixturePath, type VoiceFixtureName,
} from '../support/generative';
import { expectProblem, type GenerationApi, type GenerationDto } from '../support/generation-api';
import { dataFilesContaining } from '../support/generator-stack';
import { expectHelpMarker } from '../support/help';
import { beginnerHiddenUseMyVoiceIds, everyLevelUseMyVoiceIds, expectNotRendered, expectRendered } from '../support/levels';
import { expectPlayable } from '../support/media';
import { levelState } from '../support/preferences';
import type { VoiceApi, VoiceProfileDto, VoiceProfileForm } from '../support/voice-api';
import { expect, test } from '../support/generative-test';

// Feature 010 US3 (own voice) with the consent, exact-copy and deletion rules (010:T052;
// FR-005-FR-009, FR-011, FR-015; SC-004, SC-005, SC-006). Fake generator + fake singer stack
// (quickstart V2). The "own voice" is the synthetic sung fixture own-voice-20s.wav: no real
// person's voice is used anywhere in the suite. The microphone cases are in voice-recording.spec.ts.
const consent = readEthicsCases().consent;
const labels = readEthicsCases().aiLabels;
const manifest = readVoiceManifest();
const refusedSamples = (Object.keys(manifest.fixtures) as VoiceFixtureName[]).filter((name) => manifest.fixtures[name].expectedCheck !== 'ok');

function form(displayName: string, overrides: Partial<VoiceProfileForm> = {}): VoiceProfileForm {
  return { displayName, personName: 'Test Singer', consentConfirmed: true, consentStatementVersion: consent.statementVersion, ...overrides };
}

async function createdProfile(voiceApi: VoiceApi, displayName: string): Promise<VoiceProfileDto> {
  const response = await voiceApi.createProfileFromFile(voiceFixturePath('own-voice-20s.wav'), form(displayName));
  expect(response.status(), await response.text()).toBe(201);
  return (await response.json()) as VoiceProfileDto;
}

async function profileNames(voiceApi: VoiceApi): Promise<string[]> {
  return (await voiceApi.listProfiles()).map((profile) => profile.displayName);
}

/** A completed song take, its "Use my voice" conversion with `profile`, both completed. */
async function convertedTake(voiceApi: VoiceApi, generationApi: GenerationApi, profile: VoiceProfileDto): Promise<{ song: GenerationDto; conversion: GenerationDto }> {
  const song = await songTake(voiceApi, generationApi, { lyricsText: readOwnLyrics('sample-en.txt'), language: 'en', leadVoice: 'any', durationSeconds: 20 });
  const response = await voiceApi.convert(song.id, 1, profile.id);
  expect(response.status(), await response.text()).toBe(202);
  const queued = (await response.json()) as GenerationDto;
  return { song, conversion: await generationApi.waitForCompleted(queued.id, fakeJobTimeoutMs + generationTimeoutMs) };
}

test.beforeEach(async ({ generationApi }) => {
  test.setTimeout(2 * fakeJobTimeoutMs + 3 * generationTimeoutMs);
  await generationApi.requireVoiceState('ready');
});

test.describe('consent', { tag: '@generative' }, () => {
  test('the statement is shown in English and Dutch with its version (FR-006, US3-5)', async ({ voiceApi, voices }) => {
    const statement = await voiceApi.consentStatement();
    expect([statement.version, statement.text, statement.textNl]).toEqual([consent.statementVersion, consent.textEn, consent.textNl]);
    await voices.gotoNew();
    await expect(voices.consentText).toHaveAttribute('data-version', consent.statementVersion);
    await expect(voices.consentText).toContainText(consent.textEn);
    await expect(voices.consentText).toContainText(consent.textNl);
  });

  test('Save stays disabled until a sample, the ticked statement and both names are given (US3-1, SC-004)', async ({ voices }, info) => {
    const displayName = uniqueName('Consent gate', info.project.name, runId());
    await voices.gotoNew();
    await expect(voices.saveButton).toBeDisabled();
    await voices.upload(voiceFixturePath('own-voice-20s.wav'));
    await voices.fillConsent({ displayName, personName: 'Test Singer', consent: false });
    await expect(voices.saveButton).toBeDisabled();
    await voices.fillConsent({ displayName, personName: '', consent: true });
    await expect(voices.saveButton).toBeDisabled();
    await voices.fillConsent({ displayName, personName: 'Test Singer', consent: true });
    await expect(voices.saveButton).toBeEnabled();
  });

  test('the API refuses a profile without valid consent before anything is stored (FR-006, SC-004)', async ({ voiceApi }, info) => {
    const displayName = uniqueName('No consent', info.project.name, runId());
    const sample = voiceFixturePath('own-voice-20s.wav');
    expect((await expectProblem(await voiceApi.createProfileFromFile(sample, form(displayName, { consentConfirmed: false })), 400)).type).toBe('consent-required');
    expect((await expectProblem(await voiceApi.createProfileFromFile(sample, form(displayName, { consentStatementVersion: consent.outdatedVersion })), 400)).type).toBe('consent-outdated');
    await expectProblem(await voiceApi.createProfileFromFile(sample, form(displayName, { personName: '' })), 400);
    expect(await profileNames(voiceApi)).not.toContain(displayName);
  });

  test('an uploaded sample with consent becomes a profile with its consent record and playable sample (US3-1, US3-5)', async ({ generationApi, voiceApi, voices }, info) => {
    requireGeneratorMode('ready', 'oom', 'slow');
    const displayName = uniqueName('Uploaded voice', info.project.name, runId());
    await voices.gotoNew();
    await voices.upload(voiceFixturePath('own-voice-20s.wav'));
    await voices.fillConsent({ displayName, personName: 'Test Singer', consent: true });
    await voices.saveButton.click();
    await expect.poll(() => profileNames(voiceApi)).toContain(displayName);
    const profile = present((await voiceApi.listProfiles()).find((p) => p.displayName === displayName), 'the new profile');
    expect([profile.consent.statementVersion, profile.consent.personName, profile.source]).toEqual([consent.statementVersion, 'Test Singer', 'uploaded']);
    expect(profile.durationSeconds).toBeCloseTo(20, 0);
    expect(profile.voicedRatio).toBeGreaterThanOrEqual(manifest.limits.minVoicedRatio);
    await expect(voices.part('consent', profile.id)).toContainText(consent.statementVersion);
    await expect(voices.part('consent', profile.id)).toContainText('Test Singer');
    await expectPlayable(voices.part('play', profile.id));
    expect((await generationApi.fetch(profile.sampleUrl)).status()).toBe(200);
    expect(await voiceApi.deleteProfile(profile.id, true)).toBe(204);
  });
});

test.describe('samples that are refused', { tag: '@generative' }, () => {
  test('too short, too long and unvoiced samples are refused with the reason (US3-2, FR-005, FR-007)', async ({ voiceApi, voices }, info) => {
    requireGeneratorMode('ready', 'oom', 'slow');
    for (const name of refusedSamples) {
      const response = await voiceApi.createProfileFromFile(voiceFixturePath(name), form(uniqueName(name, info.project.name, runId())));
      expect((await expectProblem(response, 422)).type, name).toBe(manifest.fixtures[name].expectedCheck);
    }
    await voices.gotoNew();
    await voices.upload(voiceFixturePath('unvoiced-20s.wav'));
    await voices.fillConsent({ displayName: uniqueName('Noise', info.project.name, runId()), personName: 'Test Singer', consent: true });
    await voices.saveButton.click();
    await expect(voices.error).toContainText(/singing|voiced/iu);
  });

  test('a stem or a take from the library is refused as library-audio (US3-2, FR-007, SC-004)', async ({ api, generationApi, voiceApi, voices }, info) => {
    requireGeneratorMode('ready', 'oom', 'slow');
    const track = await sourceTrack(api);
    const stem = present(track.stems.find((s) => s.name === 'vocals'), 'vocals stem');
    const stemBytes = await (await generationApi.fetch(stem.url)).body();
    const asStem = await voiceApi.createProfile({ name: 'my-voice.flac', bytes: stemBytes }, form(uniqueName('Stem copy', info.project.name, runId())));
    expect((await expectProblem(asStem, 422)).type).toBe('library-audio');
    const take = takeOf(await restyledTake(generationApi, track.id, 'Jazz trio'));
    const asTake = await voiceApi.createProfile({ name: 'take.flac', bytes: await generationApi.takeBytes(take) }, form(uniqueName('Take copy', info.project.name, runId())));
    expect((await expectProblem(asTake, 422)).type).toBe('library-audio');
    const file = path.resolve(__dirname, '..', '.state', 'audio', `stem-copy-${runId()}.flac`);
    mkdirSync(path.dirname(file), { recursive: true });
    writeFileSync(file, stemBytes);
    await voices.gotoNew();
    await voices.upload(file);
    await voices.fillConsent({ displayName: uniqueName('Stem in UI', info.project.name, runId()), personName: 'Test Singer', consent: true });
    await voices.saveButton.click();
    await expect(voices.error).toContainText(/library/iu);
  });

  test('in mode unvoiced-sample every sample is refused as unvoiced (quickstart V2)', async ({ voiceApi }, info) => {
    requireGeneratorMode('unvoiced-sample');
    const response = await voiceApi.createProfileFromFile(voiceFixturePath('own-voice-20s.wav'), form(uniqueName('Unvoiced mode', info.project.name, runId())));
    expect((await expectProblem(response, 422)).type).toBe('unvoiced');
  });
});

test.describe('use my voice', { tag: '@generative' }, () => {
  test.beforeEach(() => {
    requireGeneratorMode('ready', 'oom', 'slow');
  });

  test('"Use my voice" converts a song take into a new take labelled with the profile (US3-3, FR-008, FR-011)', async ({ generationApi, generations, useMyVoice, voiceApi }, info) => {
    const profile = await createdProfile(voiceApi, uniqueName('Converted voice', info.project.name, runId()));
    const song = await songTake(voiceApi, generationApi, { lyricsText: readOwnLyrics('sample-en.txt'), language: 'en', leadVoice: 'any', durationSeconds: 20 });
    const before = (await generationApi.list({ kind: 'voiceConversion' })).map((g) => g.id);
    await generations.goto();
    await useMyVoice.convert(song.id, 1, profile.displayName);
    const queued = await generationApi.waitForNewest({ kind: 'voiceConversion' }, before);
    expect(['queued', 'waitingForStems', 'waitingForGpu', 'running', 'completed']).toContain(queued.status);
    await generations.waitForStatus(queued.id, 'completed', fakeJobTimeoutMs + generationTimeoutMs);
    await generations.expectVocal(queued.id, 'converted');
    await expect(generations.take('vocal-badge', queued.id)).toContainText(profile.displayName);
    await expectPlayable(generations.take('vocal-play', queued.id));
    const done = await generationApi.get(queued.id);
    expect(takeOf(done).durationSeconds).toBeCloseTo(takeOf(song).durationSeconds, 0);
    expect(takeOf(await generationApi.get(song.id)).importedTrackId).not.toBeNull();
    expect([takeOf(done).vocal, takeOf(done).voiceProfileName]).toEqual(['converted', profile.displayName]);
    await expectAiLabels(generationApi, takeOf(done));
  });

  test('instrumental and restyle takes offer no "Use my voice"; the API answers 409 no-vocals (edge case)', async ({ api, generationApi, generations, voiceApi }, info) => {
    const profile = await createdProfile(voiceApi, uniqueName('No vocals', info.project.name, runId()));
    const instrumental = await songTake(voiceApi, generationApi, { lyricsText: readOwnLyrics('sample-en.txt'), language: 'en', leadVoice: 'any', instrumental: true, durationSeconds: 20 });
    expect((await expectProblem(await voiceApi.convert(instrumental.id, 1, profile.id), 409)).type).toBe('no-vocals');
    const restyled = await restyledTake(generationApi, (await sourceTrack(api)).id, 'Synthwave');
    await generations.goto();
    await expect(generations.take('use-my-voice', instrumental.id)).toHaveCount(0);
    await expect(generations.take('use-my-voice', restyled.id)).toHaveCount(0);
    expect(await voiceApi.deleteProfile(profile.id, true)).toBe(204);
  });
});

test.describe('use my voice at Beginner', { tag: '@generative' }, () => {
  test.use({ storageState: levelState('beginner') });

  test('profile select and buttons are shown; pitch shift and auto shift are not rendered (ui-test-ids 1.9.0, R7)', async ({ generationApi, generations, page, useMyVoice, voiceApi }, info) => {
    requireGeneratorMode('ready', 'oom', 'slow');
    const profile = await createdProfile(voiceApi, uniqueName('Beginner voice', info.project.name, runId()));
    const song = await songTake(voiceApi, generationApi, { lyricsText: readOwnLyrics('sample-en.txt'), language: 'en', leadVoice: 'any', durationSeconds: 20 });
    await generations.goto();
    await useMyVoice.open(song.id, 1);
    await expectRendered(page, everyLevelUseMyVoiceIds);
    await expectNotRendered(page, beginnerHiddenUseMyVoiceIds);
    await expectHelpMarker(page.locator('body'), 'voice-conversion');
    await useMyVoice.byId('take-voice-convert-cancel').click();
    await expect(useMyVoice.profileSelect).toBeHidden();
    expect(await voiceApi.deleteProfile(profile.id, true)).toBe(204);
  });
});

test.describe('deleting a voice profile', { tag: '@generative' }, () => {
  test.beforeEach(() => {
    requireGeneratorMode('ready', 'oom', 'slow');
  });

  test('keeping the takes: sample gone, converted takes stay labelled "(profile deleted)" (US3-4, FR-009)', async ({ generationApi, voiceApi, voices }, info) => {
    const profile = await createdProfile(voiceApi, uniqueName('Deleted, takes kept', info.project.name, runId()));
    const { conversion } = await convertedTake(voiceApi, generationApi, profile);
    await voices.gotoNew();
    await voices.delete(profile.id, false);
    expect((await generationApi.fetch(profile.sampleUrl)).status()).toBe(404);
    expect((await voiceApi.listProfiles()).map((p) => p.id)).not.toContain(profile.id);
    const kept = present((await generationApi.get(conversion.id)).voiceConversion, 'conversion details');
    expect(kept.voiceProfileId).toBeNull();
    expect(kept.voiceProfileName.endsWith(labels.convertedProfileDeletedSuffix)).toBe(true);
    expect((await generationApi.fetch(takeOf(conversion).audioUrl)).status()).toBe(200);
  });

  test('with its takes: the converted takes and their library items are removed too (US3-4, FR-009)', async ({ generationApi, voiceApi, voices }, info) => {
    const profile = await createdProfile(voiceApi, uniqueName('Deleted with takes', info.project.name, runId()));
    const { song, conversion } = await convertedTake(voiceApi, generationApi, profile);
    const take = takeOf(conversion);
    await voices.gotoNew();
    await voices.delete(profile.id, true);
    expect((await generationApi.fetch(take.audioUrl)).status()).toBe(404);
    expect((await generationApi.libraryItem(present(take.libraryAssetId, 'library item'))).status).toBe(404);
    expect((await generationApi.fetch(takeOf(song).audioUrl)).status()).toBe(200);
  });

  test('no file derived from the sample remains on disk (SC-006, quickstart V7)', { tag: '@restart' }, async ({ generationApi, voiceApi }, info) => {
    requireEnv('E2E_GENERATIVE_COMPOSE_FILES');
    const profile = await createdProfile(voiceApi, uniqueName('Disk walk', info.project.name, runId()));
    const { conversion } = await convertedTake(voiceApi, generationApi, profile);
    expect(await voiceApi.deleteProfile(profile.id, true)).toBe(204);
    expect(dataFilesContaining('api', [profile.id, conversion.id])).toEqual([]);
    expect(dataFilesContaining('singer', ['.flac', '.wav', '.webm', '.mp3'])).toEqual([]);
  });
});
