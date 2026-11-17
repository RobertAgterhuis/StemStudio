import { readFile } from 'node:fs/promises';
import type { APIRequestContext, Page } from '@playwright/test';
import { SetlistBar, SetlistsPage } from '../pages/setlists-page';
import type { StemStudioApi, TrackDto } from '../support/api';
import { axeViolations } from '../support/axe';
import { fillAndCommit } from '../support/controls';
import { apiBaseUrl, fakeJobTimeoutMs, runId } from '../support/env';
import { fixturePath } from '../support/fixtures';
import { blurActiveElement } from '../support/help';
import { mixSettings } from '../support/practice';
import { PracticeFlowApi } from '../support/practice-flow';
import { containsText, pdfText } from '../support/practice-flow-pdf';
import { uniqueWavCopy } from '../support/unique-audio';
import { expect, test } from '../support/test';

// Feature 014 US5 (T094; FR-015..FR-018, SC-008): setlists on the fake stack. A setlist of three songs (one with a mix and
// a note) is built in the UI, reordered with the keyboard, played through with the bar button, Ctrl+Right and the pedal
// preset "next-previous-song", printed (pypdf reads the PDF text) and loses an entry when its track is deleted.
let context: APIRequestContext;
let flow: PracticeFlowApi;

test.beforeEach(async ({ playwright }) => {
  test.setTimeout(3 * fakeJobTimeoutMs + 120_000);
  context = await playwright.request.newContext({ baseURL: apiBaseUrl });
  flow = new PracticeFlowApi(context);
});

test.afterEach(async () => {
  await context.dispose();
});

/** Three completed songs; the first one gets a mix named for this run. */
interface Songs {
  readonly tracks: readonly [TrackDto, TrackDto, TrackDto];
  readonly mixName: string;
}

async function threeSongs(api: StemStudioApi): Promise<Songs> {
  const tracks = [
    await api.ensureCompleted('click_120bpm_4-4_12s.wav', fakeJobTimeoutMs),
    await api.ensureCompleted('music_100bpm_20s.mp3', fakeJobTimeoutMs),
    await api.ensureCompleted('music_100bpm_20s.flac', fakeJobTimeoutMs),
  ] as const;
  const mixName = `Band night ${runId()}`;
  await api.createMix(tracks[0].id, mixSettings({ name: mixName }));
  return { tracks, mixName };
}

async function buildSetlist(setlists: SetlistsPage, songs: Songs): Promise<string> {
  await setlists.goto();
  const id = await setlists.create(`Friday rehearsal ${runId()}`);
  await setlists.add(songs.tracks[0].id, songs.mixName);
  await setlists.add(songs.tracks[1].id);
  await setlists.add(songs.tracks[2].id);
  await expect(setlists.entries).toHaveCount(3);
  return id;
}

async function pressOutsideFields(page: Page, key: string): Promise<void> {
  await blurActiveElement(page);
  await page.keyboard.press(key);
}

test.describe('setlist editing and play mode (014 US5)', { tag: '@practice-flow' }, () => {
  test('three songs with a mix and a note, reordered with the keyboard (scenarios 1-2)', async ({ api, page }) => {
    const setlists = new SetlistsPage(page);
    const songs = await threeSongs(api);
    const id = await buildSetlist(setlists, songs);
    const [first, second] = await setlists.entryIds();
    await fillAndCommit(setlists.byId(`setlist-entry-note-${first ?? ''}`), 'Start soft.');

    await setlists.byId(`setlist-entry-down-${first ?? ''}`).focus();
    await page.keyboard.press('Enter');

    await expect.poll(async () => (await setlists.entryIds()).slice(0, 2)).toEqual([second, first]);
    await expect(setlists.byId(`setlist-entry-down-${first ?? ''}`)).toBeFocused();
    const stored = await flow.setlist(id);
    expect(stored.entries.map((entry) => entry.trackId)).toEqual([songs.tracks[1].id, songs.tracks[0].id, songs.tracks[2].id]);
    expect(stored.entries[1]).toMatchObject({ mixName: songs.mixName, note: 'Start soft.' });
    await expect(setlists.byId('setlist-total')).not.toBeEmpty();
  });

  test('play mode opens each mixer in turn: button, Ctrl+Right, Ctrl+Left and the pedal preset (scenario 3, FR-017)', async ({ api, page }) => {
    const setlists = new SetlistsPage(page);
    const bar = new SetlistBar(page);
    const songs = await threeSongs(api);
    const id = await buildSetlist(setlists, songs);
    await flow.setSetlistNote(id, 'Tune down first.');

    await setlists.goto();
    await setlists.byId(`setlist-play-${id}`).click();
    await bar.expectOn(songs.tracks[0].id);
    await expect(bar.bar).toHaveAttribute('data-count', '3');
    await bar.next.click();
    await bar.expectOn(songs.tracks[1].id);
    await pressOutsideFields(page, 'Control+ArrowRight');
    await bar.expectOn(songs.tracks[2].id);
    await pressOutsideFields(page, 'Control+ArrowLeft');
    await bar.expectOn(songs.tracks[1].id);

    const playing = page.url();
    await page.goto('/settings/shortcuts');
    await page.getByTestId('pedal-mode').click();
    await page.getByTestId('pedal-preset').selectOption('next-previous-song');
    await page.goto(playing);
    await bar.expectOn(songs.tracks[1].id);
    await pressOutsideFields(page, 'PageDown');
    await bar.expectOn(songs.tracks[2].id);
    await expect(bar.note).toBeAttached();
    await bar.exit();
  });

});

test.describe('setlist PDF, removed songs and accessibility (014 US5)', { tag: '@practice-flow' }, () => {
  test('the PDF lists the songs in order with notes, sections and lyrics (scenario 4, FR-018)', async ({ api, page }) => {
    const setlists = new SetlistsPage(page);
    const songs = await threeSongs(api);
    const id = await buildSetlist(setlists, songs);
    await flow.setSetlistNote(id, 'Bring the capo.');
    const [first] = await setlists.entryIds();
    await fillAndCommit(setlists.byId(`setlist-entry-note-${first ?? ''}`), 'Start soft.');
    const sections = await flow.waitForSections(songs.tracks[0].id, fakeJobTimeoutMs);

    const download = await setlists.downloadPdf();

    expect(download.suggestedFilename()).toMatch(/^Friday rehearsal .*\.pdf$/u);
    const text = pdfText(await readFile(await download.path())).join('\n');
    const titles = songs.tracks.map((track) => track.title);
    const order = titles.map((title) => text.indexOf(title));
    expect(order.every((index) => index >= 0), `every title is printed: ${titles.join(', ')}`).toBe(true);
    expect([...order].sort((a, b) => a - b)).toEqual(order);
    expect(containsText(text, 'Bring the capo.')).toBe(true);
    expect(containsText(text, 'Start soft.')).toBe(true);
    expect(containsText(text, sections.sections[0]?.label ?? 'missing')).toBe(true);
    expect(containsText(text, `Mix: ${songs.mixName}`)).toBe(true);
  });

  test('deleting a track removes its entry and the editor says so once (scenario 5)', async ({ api, page }) => {
    const setlists = new SetlistsPage(page);
    const songs = await threeSongs(api);
    const disposable = await api.ensureFileCompleted(uniqueWavCopy(fixturePath('click_120bpm_4-4_12s.wav'), `setlists-${runId()}`), fakeJobTimeoutMs);
    const id = await buildSetlist(setlists, songs);
    await setlists.add(disposable.id);
    await expect(setlists.entries).toHaveCount(4);

    expect(await api.deleteTrack(disposable.id)).toBe(204);

    await setlists.open(id);
    await expect(setlists.entries).toHaveCount(3);
    await expect(setlists.byId('setlist-removed-note')).toBeVisible();
    await page.reload();
    await expect(setlists.byId('setlist-removed-note')).toHaveCount(0);
  });

  test('the setlists page and the editor have no axe violations (SC-008)', async ({ api, page }) => {
    const setlists = new SetlistsPage(page);
    const id = await buildSetlist(setlists, await threeSongs(api));

    expect(await axeViolations(page)).toEqual([]);
    await setlists.goto();
    await expect(setlists.byId(`setlist-row-${id}`)).toBeVisible();
    expect(await axeViolations(page)).toEqual([]);
  });
});
