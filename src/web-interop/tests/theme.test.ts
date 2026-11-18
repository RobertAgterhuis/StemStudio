import { afterEach, describe, expect, it, vi } from 'vitest';
import { applyAppearance, applyAttributes, resolveAppearance, watchSystemAppearance } from '../src/theme';
import { defaultPreferences } from '../src/preferences';
import type { DotNetRef } from '../src/types';
import { DARK, REDUCED, installMedia } from './media';

const none = (): boolean => false;
const all = (): boolean => true;

afterEach(() => {
  vi.unstubAllGlobals();
  for (const name of ['data-theme', 'data-density', 'data-motion', 'data-level']) document.documentElement.removeAttribute(name);
});

describe('resolveAppearance (web-interop.md 1)', () => {
  it.each([
    ['light', none, 'light'],
    ['dark', none, 'dark'],
    ['system', all, 'dark'],
    ['system', none, 'light'],
    ['bogus', all, 'dark'],
  ])('theme %s with OS dark=%s resolves to %s', (theme, media, expected) => {
    expect(resolveAppearance({ schemaVersion: 1, theme }, media).theme).toBe(expected);
  });

  it.each([
    ['auto', 'expert', 'compact'],
    ['auto', 'advanced', 'comfortable'],
    ['auto', null, 'comfortable'],
    ['comfortable', 'expert', 'comfortable'],
    ['compact', 'beginner', 'compact'],
    ['huge', 'expert', 'compact'],
  ])('density %s at level %s resolves to %s', (density, level, expected) => {
    expect(resolveAppearance({ schemaVersion: 1, density, level }, none).density).toBe(expected);
  });

  it.each([
    ['system', all, 'reduced'],
    ['system', none, 'full'],
    ['reduced', none, 'reduced'],
    [7, none, 'full'],
  ])('motion %s with OS reduce=%s resolves to %s', (motion, media, expected) => {
    expect(resolveAppearance({ schemaVersion: 1, motion }, media).motion).toBe(expected);
  });

  it.each([
    ['beginner', 'beginner'],
    ['advanced', 'advanced'],
    ['expert', 'expert'],
    [null, 'beginner'],
    ['guru', 'beginner'],
  ])('level %s resolves to %s', (level, expected) => {
    expect(resolveAppearance({ schemaVersion: 1, level }, none).level).toBe(expected);
  });

  it('reads schema version 2 records (014)', () => {
    expect(resolveAppearance({ schemaVersion: 2, theme: 'dark' }, () => false).theme).toBe('dark');
  });

  it.each([[null], ['x'], [{ schemaVersion: 3, theme: 'light' }], [[]]])('uses defaults for %j', (raw) => {
    expect(resolveAppearance(raw, none)).toEqual({ theme: 'light', density: 'comfortable', motion: 'full', level: 'beginner' });
  });

  it('asks the media function for the exact queries', () => {
    const asked: string[] = [];
    resolveAppearance({ schemaVersion: 1 }, (q) => {
      asked.push(q);
      return false;
    });
    expect(asked.sort()).toEqual([DARK, REDUCED].sort());
  });
});

describe('applyAttributes / applyAppearance', () => {
  it('writes the four data attributes', () => {
    const root = document.createElement('html');
    applyAttributes(root, { theme: 'dark', density: 'compact', motion: 'reduced', level: 'expert' });
    expect(root.dataset).toMatchObject({ theme: 'dark', density: 'compact', motion: 'reduced', level: 'expert' });
  });

  it('applies a record to the document element using the OS settings', () => {
    installMedia({ [DARK]: true, [REDUCED]: true });
    applyAppearance({ ...defaultPreferences(), level: 'advanced' });
    expect(document.documentElement.dataset).toMatchObject({ theme: 'dark', density: 'comfortable', motion: 'reduced', level: 'advanced' });
  });

  it('mirrors the resolved attributes on the app shell root (ui-test-ids 1.3.0)', () => {
    installMedia({ [DARK]: true });
    const shell = document.createElement('div');
    shell.dataset.testid = 'app-shell';
    document.body.appendChild(shell);
    applyAppearance({ ...defaultPreferences(), motion: 'reduced' });
    expect(shell.dataset).toMatchObject({ theme: 'dark', density: 'comfortable', motion: 'reduced', level: 'beginner' });
    shell.remove();
  });

  it('treats a missing matchMedia as "no match"', () => {
    vi.stubGlobal('matchMedia', undefined);
    applyAppearance(defaultPreferences());
    expect(document.documentElement.dataset.theme).toBe('light');
  });
});

describe('watchSystemAppearance', () => {
  it('calls OnSystemAppearanceChanged when the colour scheme or reduced motion changes', () => {
    const media = installMedia();
    const calls = vi.fn(() => Promise.resolve(undefined));
    const watch = watchSystemAppearance({ invokeMethodAsync: calls });
    media.set(DARK, true);
    media.set(REDUCED, true);
    expect(calls).toHaveBeenCalledTimes(2);
    expect(calls).toHaveBeenCalledWith('OnSystemAppearanceChanged');
    watch.dispose();
    watch.dispose();
    expect(media.listeners()).toBe(0);
  });

  it('is a no-op without matchMedia', () => {
    vi.stubGlobal('matchMedia', undefined);
    const dotnet: DotNetRef = { invokeMethodAsync: vi.fn(() => Promise.resolve(undefined)) };
    expect(() => {
      watchSystemAppearance(dotnet).dispose();
    }).not.toThrow();
  });
});
