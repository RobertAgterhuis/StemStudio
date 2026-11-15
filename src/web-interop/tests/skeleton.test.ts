import { describe, expect, it } from 'vitest';
import { applyFirstPaint } from '../src/first-paint';
import {
  WEB_INTEROP_CONTRACT_VERSION,
  applyAppearance,
  captureFocus,
  focusedHelpId,
  readPreferences,
  registerShortcuts,
  startTour,
  watchPreferences,
  watchSystemAppearance,
  writePreferences,
  type DotNetRef,
  type StoredPreferences,
  type TourRuntime,
} from '../src/index';

const dotnet: DotNetRef = { invokeMethodAsync: () => Promise.resolve(undefined) };

const preferences: StoredPreferences = {
  schemaVersion: 1,
  level: null,
  theme: 'system',
  density: 'auto',
  motion: 'system',
  help: { welcomeDismissed: false, tours: {}, hintsDismissed: [] },
};

const tour: TourRuntime = {
  id: 'import-basics',
  version: 1,
  reducedMotion: false,
  steps: [{ target: 'import-source', title: 'Source', text: 'Paste a link.' }],
  labels: { next: 'Next', previous: 'Previous', done: 'Done', close: 'Close', progress: '{{current}} of {{total}}' },
};

describe('web-interop skeleton (Phase 0)', () => {
  it('declares contract version 1.0.0', () => {
    expect(WEB_INTEROP_CONTRACT_VERSION).toBe('1.0.0');
  });

  it.each([
    ['readPreferences', () => readPreferences()],
    ['writePreferences', () => writePreferences(preferences)],
    ['watchPreferences', () => watchPreferences(dotnet)],
    ['applyAppearance', () => { applyAppearance(preferences); }],
    ['watchSystemAppearance', () => watchSystemAppearance(dotnet)],
    ['registerShortcuts', () => registerShortcuts(dotnet, 'global')],
    ['startTour', () => startTour(dotnet, tour)],
    ['focusedHelpId', () => focusedHelpId()],
    ['captureFocus', () => captureFocus()],
    ['applyFirstPaint', () => { applyFirstPaint(document.documentElement); }],
  ])('%s is a stub that throws until implemented', (name, call) => {
    expect(call).toThrow(`not implemented: ${name}`);
  });
});
