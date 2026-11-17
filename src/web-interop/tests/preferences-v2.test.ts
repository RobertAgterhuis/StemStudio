import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it, vi } from 'vitest';
import { CATALOGUE } from '../src/catalogue';
import { MAX_OVERRIDES, validateOverrides } from '../src/overrides';
import { MAX_PREFERENCES_BYTES, defaultShortcuts, parsePreferences, validatePreferences, validateShortcuts, writePreferences } from '../src/preferences';
import type { StoredPreferences } from '../src/types';

interface OverrideCase {
  name: string;
  input: unknown;
  overrides: Record<string, unknown>;
  dropped: string[];
}

// Vitest runs in src/web-interop (jsdom has no file: import.meta.url).
const casesFile = resolve(process.cwd(), '../../tests/StemStudio.Web.Tests/Practice/keymap-override-cases.json');
const shared = (JSON.parse(readFileSync(casesFile, 'utf-8')) as { cases: OverrideCase[] }).cases;

const v1 = {
  schemaVersion: 1,
  level: 'advanced',
  theme: 'dark',
  density: 'auto',
  motion: 'system',
  help: { welcomeDismissed: true, tours: {}, hintsDismissed: [] },
};

describe('schema version 2 (web-interop 1.1.0 5.3)', () => {
  it('reads a version 1 record with default shortcuts and upgrades it', () => {
    const record = validatePreferences(v1);
    expect(record.schemaVersion).toBe(2);
    expect(record.level).toBe('advanced');
    expect(record.shortcuts).toEqual(defaultShortcuts());
  });

  it('ignores a shortcuts field in a version 1 record', () => {
    expect(validatePreferences({ ...v1, shortcuts: { singleKey: false } }).shortcuts.singleKey).toBe(true);
  });

  it('keeps valid version 2 shortcut settings', () => {
    const shortcuts = { singleKey: false, pedalMode: true, pedalPreset: 'next-previous-song', overrides: { 'mixer.stop': [{ key: 'j' }] } };
    expect(validatePreferences({ ...v1, schemaVersion: 2, shortcuts }).shortcuts).toEqual(shortcuts);
  });

  it.each([[null], ['x'], [{ singleKey: 'no', pedalMode: 'yes', pedalPreset: 'jump', overrides: 'none' }]])(
    'takes defaults for invalid fields %j',
    (shortcuts) => {
      expect(validateShortcuts(shortcuts)).toEqual(defaultShortcuts());
    },
  );

  it('has the documented defaults', () => {
    expect(defaultShortcuts()).toEqual({ singleKey: true, pedalMode: false, pedalPreset: 'play-section-start', overrides: {} });
  });

  it('reports dropped overrides once per page', async () => {
    vi.resetModules();
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    const fresh = await import('../src/preferences');
    fresh.validateShortcuts({ overrides: { 'mixer.fly': [] } });
    fresh.validateShortcuts({ overrides: { 'mixer.fly': [] } });
    fresh.validateShortcuts({ overrides: {} });
    expect(warn).toHaveBeenCalledTimes(1);
    expect(String(warn.mock.calls[0]?.[0])).toContain('mixer.fly');
  });

  it('keeps the 16 KB record limit', () => {
    const hints = Array.from({ length: 200 }, (_, i) => `hint-${'x'.repeat(80)}-${String(i)}`);
    const big: StoredPreferences = { ...validatePreferences(v1), help: { welcomeDismissed: false, tours: {}, hintsDismissed: hints } };
    expect(writePreferences(big)).toBe(false);
    expect(parsePreferences(`{"schemaVersion":2,"pad":"${'x'.repeat(MAX_PREFERENCES_BYTES)}"}`)).toEqual(validatePreferences(null));
  });
});

describe('override rules shared with KeyMapValidator (keymap-override-cases.json)', () => {
  it.each(shared.map((c) => [c.name, c] as const))('%s', (_, testCase) => {
    const result = validateOverrides(testCase.input);
    expect(result.overrides).toEqual(testCase.overrides);
    expect(result.dropped).toEqual(testCase.dropped);
  });

  it('keeps at most 60 commands, in record order', () => {
    const input: Record<string, unknown> = {};
    CATALOGUE.commands.filter((c) => !c.reserved).forEach((c) => (input[c.command] = []));
    expect(Object.keys(input).length).toBeLessThan(MAX_OVERRIDES);
    expect(Object.keys(validateOverrides(input).overrides)).toEqual(Object.keys(input));
  });
});
