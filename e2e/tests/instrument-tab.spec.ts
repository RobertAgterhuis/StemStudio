import { readFile } from 'node:fs/promises';
import { ensureDetailCompleted } from '../support/detail';
import { fakeJobTimeoutMs } from '../support/env';
import { clefs, isMidi, staves } from '../support/musicxml';
import { hasNoteOn, musicXmlOf, soundingPitch, staffTunings, tabNotes, tabStaffLines, tuningPitches } from '../support/instrument-scores';
import { required, useInstrumentApi } from '../support/instrument-views';
import { ensureSettledTrack } from '../support/settled-track';
import { expect, test } from '../support/test';

// Feature 016 US2 (T083; FR-018..FR-023, SC-002, SC-006, quickstart V5/V6): tablature for the guitar and bass families
// and the one-click MIDI of any channel (drums here). The fake stack transcribes the 008 fake notes; tablature, TAB
// staff and MIDI use the real notation code, so the spec checks the facts of the files: the TAB clef, one
// `staff-tuning` per string, and every `<string>`/`<fret>` pair sounding the pitch of its note.
const fixture = 'music_100bpm_20s.flac';
const scoreMs = 90_000;

/** Drop D (data-model 4.1): D2 A2 D3 G3 B3 E4. */
const dropD = [38, 45, 50, 55, 59, 64];

/** Five-string bass: B0 E1 A1 D2 G2. */
const fiveStringBass = [23, 28, 33, 38, 43];

/** General MIDI: channel 10, Acoustic Bass Drum. */
const drumChannel = 10;
const kickNote = 36;

useInstrumentApi(fakeJobTimeoutMs + 2 * scoreMs);

/** Expects a TAB staff whose frets sound the pitches of their notes. */
function expectPlayableTab(xml: string, tuning: readonly number[]): void {
  expect(clefs(xml).map((c) => c.sign)).toContain('TAB');
  expect(staffTunings(xml)).toHaveLength(tuning.length);
  expect(tabStaffLines(xml)).toBe(tuning.length);
  expect(tuningPitches(xml)).toEqual(tuning);
  const notes = tabNotes(xml);
  expect(notes.length).toBeGreaterThan(0);
  expect(notes.map((n) => soundingPitch(n, tuning, 0))).toEqual(notes.map((n) => n.midi));
}

test.describe('tablature (016 US2)', { tag: '@instrument' }, () => {
  test('guitar: drop D notation + tab from the sheet music dialog', async ({ api, metadataApi, trackPage, scoreDialog, scoreApi, assetsApi, page }) => {
    // Settled lookup: a later lookup re-files the saved item and the confirmation link opens an empty folder.
    const track = await ensureSettledTrack(api, metadataApi, fixture, fakeJobTimeoutMs);

    await trackPage.goto(track.id);
    await scoreDialog.openForStem('guitar');
    await page.getByTestId('score-profile').selectOption('guitar');
    await page.getByTestId('score-tab-layout').selectOption('notation-and-tab');
    await page.getByTestId('score-tab-tuning').selectOption('guitar-drop-d');
    await scoreDialog.generate();
    const id = await scoreDialog.savedAssetId();
    await assetsApi.waitForReady(id, scoreMs);

    const xml = await scoreApi.musicXml(id);
    expect(staves(xml)).toBe(2);
    expectPlayableTab(xml, dropD);
    await scoreDialog.savedOpen.click();
    await expect(page.getByTestId(`asset-score-tab-${id}`)).toContainText('Drop D');
  });

  test('bass: 5-string tab only has five lines and no notation staff', async ({ api, scoreApi, assetsApi }) => {
    const track = await api.ensureCompleted(fixture, fakeJobTimeoutMs);

    const xml = await musicXmlOf(scoreApi, assetsApi, {
      source: { trackId: track.id, channel: 'bass' },
      profile: 'bass',
      tablature: { tuning: 'bass-5-standard', layout: 'tab-only' },
    }, scoreMs);

    expect(staves(xml)).toBe(1);
    expect(clefs(xml).map((c) => c.sign)).toEqual(['TAB']);
    expectPlayableTab(xml, fiveStringBass);
  });
});

test.describe('MIDI of a channel (016 US2)', { tag: '@instrument' }, () => {
  test('drums: the MIDI action on the kick downloads channel 10 note 36', async ({ api, trackPage, page }) => {
    const track = await ensureDetailCompleted(api);
    required(track.detail.parents.find((p) => p.parent === 'drums')?.stems.find((s) => s.name === 'kick'), 'drums.kick stem');

    await trackPage.goto(track.id);
    const download = page.waitForEvent('download', { timeout: scoreMs });
    await page.getByTestId('midi-drums-kick').click();
    const file = await readFile(required(await (await download).path(), 'downloaded MIDI'));

    expect(isMidi(file)).toBe(true);
    expect(hasNoteOn(file, drumChannel, kickNote)).toBe(true);
    await expect(page.getByTestId('midi-drums-kick')).not.toHaveAttribute('aria-busy', 'true');
    await expect(page.getByTestId('midi-error-drums-kick')).toHaveCount(0);
  });
});
