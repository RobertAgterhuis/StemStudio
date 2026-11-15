import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fakeJobTimeoutMs, runId } from '../support/env';
import { expectAiLabels, generationTimeoutMs, songTake, takeOf } from '../support/generative-flow';
import {
  ownLyricsPath, placeholderLyrics, present, readEthicsCases, readOwnLyrics, readOwnLyricsExpected, requireGeneratorMode, uniqueName,
} from '../support/generative';
import { expectProblem } from '../support/generation-api';
import { beginnerHiddenSongIds, everyLevelSongIds, expectNotRendered, expectRendered } from '../support/levels';
import { expectPlayable } from '../support/media';
import { levelState } from '../support/preferences';
import { expect, test } from '../support/generative-test';

// Feature 010 US1 (lyrics to song), US2 (lyrics list), US4 (practice) and US5 (instrumental)
// (010:T033, T038, T056, T057; FR-001-FR-004, FR-010, FR-011, FR-013, FR-016; SC-005). Fake
// generator stack (quickstart V2). Lyrics are self-written fixtures (tests/fixtures/lyrics/).
const english = readOwnLyrics('sample-en.txt');
const expected = readOwnLyricsExpected();
const ethics = readEthicsCases();

test.beforeEach(async ({ generationApi }) => {
  test.setTimeout(fakeJobTimeoutMs + 3 * generationTimeoutMs);
  requireGeneratorMode('ready', 'oom', 'slow');
  await generationApi.requireState('ready');
});

test.describe('song from own lyrics', { tag: '@generative' }, () => {
  test('paste, add sections, "Pop rock", generate: a playable take with an AI voice (US1-1, US1-2, FR-001, FR-002, FR-016)', async ({ generationApi, generations, song }, info) => {
    await song.goto();
    await song.fill({ lyrics: english });
    await expect(song.lyricsCount).toContainText(new RegExp(`^\\s*(${String(english.length)}|${String(english.trimEnd().length)}) / 4096`, 'u'));
    await song.proposeSections();
    await song.fill({ title: uniqueName('Paper boats', info.project.name, runId()), language: 'English', style: 'Pop rock', leadVoice: 'female', durationSeconds: 30 });
    await expect(song.notice).toContainText(/personal use/iu);
    await expect(song.densityWarning).toHaveCount(0);
    const before = (await generationApi.list({ kind: 'song' })).map((g) => g.id);
    await song.generate();
    const queued = await generationApi.waitForNewest({ kind: 'song' }, before);
    await generations.waitForStatus(queued.id, 'completed', generationTimeoutMs);
    await generations.expectVocal(queued.id, 'synthetic');
    await expect(generations.take('ai-badge', queued.id)).toContainText('AI');
    await expectPlayable(generations.take('play', queued.id));
    const take = takeOf(await generationApi.get(queued.id));
    expect(take.durationSeconds).toBeCloseTo(30, 0);
    await expectAiLabels(generationApi, take);
  });

  test('the section helper proposes verse and chorus blocks (US1-2, FR-002)', async ({ voiceApi }) => {
    const proposal = await voiceApi.proposeSections(english);
    expect(proposal.sections.map((s) => s.marker)).toEqual(expected['sample-en.txt'].proposedMarkers);
    expect(proposal.text.match(/\[chorus[^\]]*\]/giu)).toHaveLength(2);
    expect(proposal.wordsPerSecondAt120s).toBeCloseTo(expected['sample-en.txt'].wordsPerSecondAt120s, 1);
  });

  test('the take details show lyrics, style, voice options, seed and model (US1-4)', async ({ generationApi, generations, voiceApi }) => {
    const done = await songTake(voiceApi, generationApi, { lyricsText: english, language: 'en', styleId: (await generationApi.preset('Pop rock')).id, leadVoice: 'male', durationSeconds: 20 });
    const take = takeOf(done);
    await generations.goto();
    const details = await generations.openDetails(done.id);
    for (const text of ['Paper boats along the gutter', 'Pop rock', String(take.seed), take.model.id]) await expect(details).toContainText(text);
    await expect(details).toContainText(/male/iu);
  });

});

test.describe('song: limits, guards and formats', { tag: '@generative' }, () => {
  test('empty, too long and unsupported lyrics are refused with the limit or reason (US1-3, FR-001)', async ({ song, voiceApi }) => {
    await expectProblem(await voiceApi.createSong({ lyricsText: '', language: 'en', leadVoice: 'any' }), 400);
    const tooLong = await expectProblem(await voiceApi.createSong({ lyricsText: placeholderLyrics(expected.maxCharacters + 1), language: 'en', leadVoice: 'any' }), 400);
    expect(JSON.stringify(tooLong)).toContain('4096');
    const pdf = path.resolve(__dirname, '..', '.state', 'not-lyrics.pdf');
    mkdirSync(path.dirname(pdf), { recursive: true });
    writeFileSync(pdf, '%PDF-1.4\n');
    expect((await voiceApi.uploadLyrics(pdf, 'Not lyrics', 'en')).status()).toBe(415);
    await song.goto();
    await song.fill({ lyrics: placeholderLyrics(expected.maxCharacters + 1) });
    await expect(song.lyricsCount).toContainText('4097 / 4096');
    await song.generate();
    await expect(song.error).toContainText('4096');
  });

  test('a dense text warns before generating but may continue (edge case, FR-001)', async ({ song, voiceApi }) => {
    await song.goto();
    await song.fill({ lyrics: english, durationSeconds: 10 });
    await expect(song.densityWarning).toBeVisible();
    await expect(song.generateButton).toBeEnabled();
    const response = await voiceApi.createSong({ lyricsText: english, language: 'en', leadVoice: 'any', durationSeconds: 10 });
    expect(response.status()).toBe(202);
    expect(((await response.json()) as { warnings: string[] }).warnings.length).toBeGreaterThan(0);
  });

  test('imitation in a free caption or the delivery is refused (FR-010)', async ({ voiceApi }) => {
    for (const refused of ethics.songTextsRefused) {
      const problem = await expectProblem(await voiceApi.createSong({ lyricsText: english, language: 'en', leadVoice: 'any', [refused.field]: refused.text }), 400);
      expect([problem.type, Object.keys(problem.errors ?? {})], refused.text).toEqual(['imitation', [refused.field]]);
    }
  });

  test('Dutch lyrics are generated with language nl (quickstart V3)', async ({ generationApi, voiceApi }) => {
    const done = await songTake(voiceApi, generationApi, { lyricsText: readOwnLyrics('sample-nl.txt'), language: 'nl', leadVoice: 'any', durationSeconds: 20 });
    expect(present(done.song, 'song details').lyrics.language).toBe(expected['sample-nl.txt'].language);
    expect(takeOf(done).vocal).toBe('synthetic');
  });

  test('an .lrc upload drops the timestamps (FR-001)', async ({ song, voiceApi }, info) => {
    const response = await voiceApi.uploadLyrics(ownLyricsPath('sample.lrc'), uniqueName('LRC upload', info.project.name, runId()), 'en');
    expect(response.status()).toBe(201);
    const sheet = (await response.json()) as { id: string; text: string };
    expect(sheet.text).toBe(expected['sample.lrc'].normalisedText);
    await song.goto();
    await song.upload(ownLyricsPath('sample.lrc'));
    await expect(song.lyricsText).toHaveValue(expected['sample.lrc'].normalisedText);
    expect(await voiceApi.deleteLyrics(sheet.id)).toBe(204);
  });
});

test.describe('song page at Beginner', { tag: '@generative' }, () => {
  test.use({ storageState: levelState('beginner') });

  test('lyrics, style, voice, length and generate are shown; the expert options are not rendered (ui-test-ids 1.9.0, R7)', async ({ page, song }) => {
    await song.goto();
    await expectRendered(page, everyLevelSongIds);
    await expectNotRendered(page, beginnerHiddenSongIds);
    await expect(song.generateButton).toBeEnabled();
  });
});

test.describe('lyrics list', { tag: '@generative' }, () => {
  test('save, generate, edit, generate again: each take keeps its own text, also after deleting the sheet (US2-1, US2-2, FR-003)', async ({ generationApi, lyricsList, page, voiceApi }, info) => {
    const title = uniqueName('Kept lyrics', info.project.name, runId());
    const sheet = await voiceApi.createLyricsOk(title, 'en', english);
    await lyricsList.goto();
    await lyricsList.action('song', sheet.id).click();
    await expect(page).toHaveURL(new RegExp(`lyricsId=${sheet.id}`, 'u'));
    const first = await songTake(voiceApi, generationApi, { lyricsId: sheet.id, language: 'en', leadVoice: 'any', durationSeconds: 20 });
    const edited = `${english.trimEnd()}\n\nOne more line for the second take`;
    await lyricsList.goto();
    await lyricsList.action('edit', sheet.id).click();
    await lyricsList.saveEditor(title, edited);
    await expect.poll(async () => ((await (await voiceApi.getLyrics(sheet.id)).json()) as { text: string }).text).toContain('One more line');
    const second = await songTake(voiceApi, generationApi, { lyricsId: sheet.id, language: 'en', leadVoice: 'any', durationSeconds: 20 });
    await lyricsList.goto();
    await lyricsList.delete(sheet.id);
    const texts = await Promise.all([first.id, second.id].map(async (id) => present((await generationApi.get(id)).song, 'song details').lyrics.text));
    expect(texts[0]).not.toContain('One more line');
    expect(texts[1]).toContain('One more line');
  });
});

test.describe('song to practice and instrumental', { tag: '@generative' }, () => {
  test('an imported song take is separated, mixable, AI-labelled and links to its lyrics (US4-1, FR-013)', async ({ api, generationApi, mixer, page, trackPage, voiceApi }, info) => {
    const sheet = await voiceApi.createLyricsOk(uniqueName('Practice lyrics', info.project.name, runId()), 'en', english);
    const done = await songTake(voiceApi, generationApi, { lyricsId: sheet.id, language: 'en', leadVoice: 'female', durationSeconds: 20 });
    const imported = await generationApi.importTake(done.id, 1);
    expect(imported.status).toBe(202);
    expect((await api.waitForTerminal(imported.track.id, fakeJobTimeoutMs)).status).toBe('completed');
    await mixer.goto(imported.track.id);
    await expect(page.getByTestId('strip-vocals')).toBeVisible();
    await trackPage.gotoLoaded(imported.track.id);
    await expect(trackPage.originBadge).toContainText(/AI-generated/iu);
    await expect(trackPage.vocalBadge).toContainText(/AI voice/iu);
    await expect(trackPage.lyricsLink).toHaveAttribute('href', new RegExp(sheet.id, 'u'));
    const origin = present((await generationApi.track(imported.track.id)).origin, 'track origin');
    expect([origin.kind, origin.vocal, origin.lyricsSheetId]).toEqual(['generated', 'synthetic', sheet.id]);
  });

  test('instrumental: no voice label, no "Use my voice", no AI_VOICE tag (US5-1, FR-004)', async ({ generationApi, generations, song }) => {
    await song.goto();
    await song.fill({ lyrics: english, style: 'Pop rock', instrumental: true, durationSeconds: 20 });
    const before = (await generationApi.list({ kind: 'song' })).map((g) => g.id);
    await song.generate();
    const queued = await generationApi.waitForNewest({ kind: 'song' }, before);
    await generations.waitForStatus(queued.id, 'completed', generationTimeoutMs);
    await generations.expectVocal(queued.id, 'none');
    await expect(generations.take('use-my-voice', queued.id)).toHaveCount(0);
    const done = await generationApi.get(queued.id);
    expect(present(done.song, 'song details').instrumental).toBe(true);
    await expectAiLabels(generationApi, takeOf(done));
  });
});
