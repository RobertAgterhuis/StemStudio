import { artworkFixturePath } from '../support/artwork-fixtures';
import { fakeJobTimeoutMs, runId } from '../support/env';
import { present, readEthicsCases, requireLibraryArtist, uniqueName, type RefusedText } from '../support/generative';
import { expectProblem, type GenerationApi, type StyleDto, type StyleInputDto } from '../support/generation-api';
import { expect, test } from '../support/generative-test';

// Feature 009 US2 (browse and choose) and US3 (own styles) plus the artist-imitation rules
// (009:T050, T062 editor part; FR-003, FR-004, FR-005; SC-003). Runs on every stack with the 009
// backend, generator or not: the style library stays usable on CPU-only installs (US6-1).
const ethics = readEthicsCases();

function userStyle(name: string, overrides: Partial<StyleInputDto> = {}): StyleInputDto {
  const base: StyleInputDto = {
    name, genreFamily: 'jazz', tempoFeel: 'keep', instrumentation: ['piano'], moods: ['calm'],
    vocalHandling: 'keep', defaultStrength: 'balanced', description: 'Brushed drums and upright bass',
  };
  return { ...base, ...overrides };
}

function withRefusedText(name: string, refused: RefusedText): StyleInputDto {
  return refused.field === 'name' ? userStyle(refused.text) : userStyle(name, { [refused.field]: refused.text });
}

async function createdStyle(generationApi: GenerationApi, input: StyleInputDto): Promise<StyleDto> {
  const response = await generationApi.createStyle(input);
  expect(response.status(), `${input.name}: ${await response.text()}`).toBe(201);
  return (await response.json()) as StyleDto;
}

function styleText(style: StyleDto): string {
  return [style.name, style.subGenre ?? '', style.description ?? '', ...style.instrumentation, ...style.moods].join(' ').toLowerCase();
}

/** Creates a style with a refused text; returns a description of the miss, or `undefined` when refused as expected. */
async function imitationMiss(generationApi: GenerationApi, refused: RefusedText, name: string): Promise<string | undefined> {
  const response = await generationApi.createStyle(withRefusedText(name, refused));
  const body = (await response.json().catch(() => ({}))) as { type?: string; errors?: Record<string, unknown>; id?: string };
  if (body.id !== undefined) {
    await generationApi.deleteStyle(body.id);
  }
  const got = `${String(response.status())} ${body.type ?? ''} ${Object.keys(body.errors ?? {}).join(',')}`;
  return got === `400 imitation ${refused.field}` ? undefined : `"${refused.text}" -> ${got}`;
}

async function newUserStyle(generationApi: GenerationApi, before: readonly string[]): Promise<StyleDto> {
  const user = await generationApi.listStyles({ kind: 'user' });
  return present(user.find((style) => !before.includes(style.id)), 'the new user style');
}

test.describe('style library: presets', { tag: '@styles' }, () => {
  test.beforeEach(async ({ generationApi }) => {
    await generationApi.requireFeature();
  });

  test('lists at least 24 read-only presets in at least 8 genre families (US2-1, US3-4, FR-003, SC-003)', async ({ generationApi, styles }) => {
    const presets = await generationApi.listStyles({ kind: 'preset' });
    expect(presets.length).toBeGreaterThanOrEqual(24);
    expect(new Set(presets.map((p) => p.genreFamily)).size).toBeGreaterThanOrEqual(8);
    await styles.goto();
    const preset = present(presets[0], 'first preset');
    await expect(styles.row(preset.id)).toHaveAttribute('data-kind', 'preset');
    await expect(styles.action('duplicate', preset.id)).toBeVisible();
    await expect(styles.action('edit', preset.id)).toHaveCount(0);
    await expect(styles.action('delete', preset.id)).toHaveCount(0);
  });

  test('presets come first and cannot be changed or deleted through the API (US3-4)', async ({ generationApi }) => {
    const all = await generationApi.listStyles();
    const kinds = all.map((style) => style.kind);
    expect(kinds).toEqual([...kinds].sort((a, b) => (a === b ? 0 : a === 'preset' ? -1 : 1)));
    const preset = present(all[0], 'first style');
    expect((await expectProblem(await generationApi.updateStyle(preset.id, userStyle(preset.name)), 403)).type).toBe('preset-read-only');
    expect(await generationApi.deleteStyle(preset.id)).toBe(403);
  });

  test('no preset uses an imitation phrase (SC-003, constitution IV)', async ({ generationApi }) => {
    const texts = (await generationApi.listStyles({ kind: 'preset' })).map(styleText);
    const hits = texts.flatMap((text) => ethics.presetForbiddenPhrases.filter((phrase) => text.includes(phrase)).map((p) => `${p} in "${text}"`));
    expect(hits).toEqual([]);
  });

  test('filters by mood and searches by name; no match shows the empty state (US2-2)', async ({ generationApi, styles }) => {
    const jazzTrio = await generationApi.preset('Jazz trio');
    const mood = present(jazzTrio.moods[0], 'a mood of "Jazz trio"');
    const matching = (await generationApi.listStyles({ mood })).map((style) => style.id);
    await styles.goto();
    await styles.filter('mood', mood);
    await expect.poll(() => styles.visibleIds()).toEqual(matching);
    await styles.filter('mood', 'All');
    await styles.search('Jazz trio');
    await expect(styles.row(jazzTrio.id)).toBeVisible();
    const total = (await generationApi.listStyles()).length;
    // The list follows the search box after a round trip; poll rather than read it once.
    await expect.poll(async () => (await styles.visibleIds()).length).toBeLessThan(total);
    await styles.search(`no style called ${runId()}`);
    await expect(styles.empty).toBeVisible();
  });
});

test.describe('style library: own styles', { tag: '@styles' }, () => {
  test.beforeEach(async ({ generationApi }) => {
    await generationApi.requireFeature();
  });

  test('duplicating a preset gives an editable "(copy)" user style; delete asks first (US3-1, FR-004)', async ({ generationApi, styles }) => {
    const bossa = await generationApi.preset('Bossa nova');
    const before = (await generationApi.listStyles({ kind: 'user' })).map((style) => style.id);
    await styles.goto();
    await styles.action('duplicate', bossa.id).click();
    await expect.poll(async () => (await newUserStyle(generationApi, before).catch(() => undefined))?.name ?? '').toMatch(/^Bossa nova \(copy( \d+)?\)$/u);
    const copy = await newUserStyle(generationApi, before);
    expect([copy.genreFamily, copy.moods, copy.instrumentation]).toEqual([bossa.genreFamily, bossa.moods, bossa.instrumentation]);
    await styles.goto();
    await expect(styles.row(copy.id)).toHaveAttribute('data-kind', 'user');
    await expect(styles.action('edit', copy.id)).toBeVisible();
    await styles.delete(copy.id);
    expect(await generationApi.deleteStyle(copy.id)).toBe(404);
  });

  test('field limits and duplicate names are refused with the field (US3-2, FR-005)', async ({ generationApi }, info) => {
    const name = uniqueName('Limits', info.project.name, runId());
    const fieldsOf = async (input: StyleInputDto): Promise<string[]> => Object.keys((await expectProblem(await generationApi.createStyle(input), 400)).errors ?? {});
    expect(await fieldsOf(userStyle(''))).toContain('name');
    expect(await fieldsOf(userStyle(name, { description: 'x'.repeat(301) }))).toContain('description');
    expect(await fieldsOf(userStyle(name, { moods: Array.from({ length: 9 }, (_, i) => `mood${String(i)}`) }))).toContain('moods');
    const style = await createdStyle(generationApi, userStyle(name));
    expect((await expectProblem(await generationApi.createStyle(userStyle(name.toUpperCase())), 409)).type).toBe('style-name-taken');
    expect(await generationApi.deleteStyle(style.id)).toBe(204);
  });
});

test.describe('style library: no artist imitation', { tag: '@styles' }, () => {
  test.beforeEach(async ({ generationApi }) => {
    await generationApi.requireFeature();
  });

  test('the editor refuses an imitation phrase with the field and the reason (US3-2, FR-005)', async ({ generationApi, styleEditor }, info) => {
    // The name must pass the guard itself: "imitat*" is one of its phrases.
    const name = uniqueName('Editor guard check', info.project.name, runId());
    await styleEditor.gotoNew();
    await styleEditor.fill({ name, genre: 'Jazz', description: present(ethics.imitationRefused[0], 'first case').text });
    await styleEditor.save();
    expect(await styleEditor.expectFieldError('description')).toMatch(/artist|sounds like|style of/iu);
    expect(await generationApi.listStyles({ q: name })).toHaveLength(0);
  });

  test('the API refuses every imitation case with problem type "imitation" (FR-005)', async ({ generationApi }, info) => {
    const misses = { spec: [] as string[], 'qa-proposal': [] as string[] };
    for (const [index, refused] of ethics.imitationRefused.entries()) {
      const miss = await imitationMiss(generationApi, refused, uniqueName(`Guard ${String(index)}`, info.project.name, runId()));
      misses[refused.source].push(...(miss === undefined ? [] : [miss]));
    }
    expect(misses.spec).toEqual([]);
    // qa-proposal cases are soft: a miss is reported in the run (and the hand-off), not hidden.
    expect.soft(misses['qa-proposal']).toEqual([]);
  });

  test('genre words are not mistaken for artist names (no false positives, FR-005)', async ({ generationApi }, info) => {
    for (const genre of ethics.genreNamesAccepted) {
      const style = await createdStyle(generationApi, userStyle(uniqueName(genre, info.project.name, runId()), { description: `${genre} with a warm sound` }));
      expect(await generationApi.deleteStyle(style.id)).toBe(204);
    }
  });

  test('an artist of the own library is refused (FR-005)', async ({ api, generationApi, metadataApi }, info) => {
    const track = await api.ensureFileCompleted(artworkFixturePath('cover_embedded_10s.mp3'), fakeJobTimeoutMs);
    const metadata = (await metadataApi.waitForMetadata(track.id, fakeJobTimeoutMs)).metadata;
    const artist = requireLibraryArtist([(await generationApi.track(track.id)).artist, metadata.artistName]);
    // "like" alone is no imitation phrase: only the artist name can make the guard refuse it.
    const description = ethics.libraryArtist.refusedDescription.replace(ethics.libraryArtist.artist, artist);
    const input = userStyle(uniqueName('Library artist', info.project.name, runId()), { description });
    expect((await expectProblem(await generationApi.createStyle(input), 400)).type).toBe('imitation');
  });
});
