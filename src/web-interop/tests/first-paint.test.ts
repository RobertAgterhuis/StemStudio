import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { applyFirstPaint } from '../src/theme';
import { PREFERENCES_KEY } from '../src/preferences';
import { DARK, REDUCED, installMedia } from './media';

function root(): HTMLElement {
  return document.createElement('html');
}

function store(record: Record<string, unknown>): void {
  localStorage.setItem(PREFERENCES_KEY, JSON.stringify({ schemaVersion: 1, ...record }));
}

beforeEach(() => {
  localStorage.clear();
});

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('first-paint entry', () => {
  it('applies the stored appearance to the document element when loaded', async () => {
    installMedia();
    store({ theme: 'dark', level: 'expert' });
    await import('../src/first-paint');
    expect(document.documentElement.dataset).toMatchObject({ theme: 'dark', density: 'compact', level: 'expert' });
  });
});

describe('applyFirstPaint (web-interop.md 1)', () => {
  const themes = ['system', 'light', 'dark'] as const;
  const densities = ['auto', 'comfortable', 'compact'] as const;
  const motions = ['system', 'reduced'] as const;
  const levels = [null, 'beginner', 'advanced', 'expert'] as const;
  const cases = themes.flatMap((theme) =>
    densities.flatMap((density) => motions.flatMap((motion) => levels.map((level) => ({ theme, density, motion, level })))),
  );

  it.each(cases)('applies %o for every combination (OS dark + reduce)', (record) => {
    installMedia({ [DARK]: true, [REDUCED]: true });
    store(record);
    const el = root();
    applyFirstPaint(el);
    const expectedDensity = record.density === 'auto' ? (record.level === 'expert' ? 'compact' : 'comfortable') : record.density;
    expect(el.dataset.theme).toBe(record.theme === 'system' ? 'dark' : record.theme);
    expect(el.dataset.density).toBe(expectedDensity);
    expect(el.dataset.motion).toBe('reduced');
    expect(el.dataset.level).toBe(record.level ?? 'beginner');
  });

  it('follows a light OS and full motion for system values', () => {
    installMedia({ [DARK]: false, [REDUCED]: false });
    store({ theme: 'system', motion: 'system' });
    const el = root();
    applyFirstPaint(el);
    expect(el.dataset.theme).toBe('light');
    expect(el.dataset.motion).toBe('full');
  });

  it('uses defaults for corrupt JSON', () => {
    installMedia({ [DARK]: true });
    localStorage.setItem(PREFERENCES_KEY, '{oops');
    const el = root();
    applyFirstPaint(el);
    expect(el.dataset).toMatchObject({ theme: 'dark', density: 'comfortable', motion: 'full', level: 'beginner' });
  });

  it('uses defaults when storage throws and never throws itself', () => {
    installMedia();
    vi.spyOn(window, 'localStorage', 'get').mockImplementation(() => {
      throw new Error('blocked');
    });
    const el = root();
    expect(() => {
      applyFirstPaint(el);
    }).not.toThrow();
    expect(el.dataset).toMatchObject({ theme: 'light', density: 'comfortable', motion: 'full', level: 'beginner' });
  });

  it('uses defaults when nothing is stored', () => {
    installMedia();
    const el = root();
    applyFirstPaint(el);
    expect(el.dataset.level).toBe('beginner');
  });

  it('ignores a record larger than 16 KB', () => {
    installMedia();
    localStorage.setItem(PREFERENCES_KEY, JSON.stringify({ schemaVersion: 1, theme: 'dark', pad: 'x'.repeat(17000) }));
    const el = root();
    applyFirstPaint(el);
    expect(el.dataset.theme).toBe('light');
  });
});
