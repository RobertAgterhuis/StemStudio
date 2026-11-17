import { describe, expect, it } from 'vitest';
import * as webInterop from '../src/index';

describe('web-interop module (contract 1.1.0)', () => {
  it('declares contract version 1.1.0 (the newest full document, 014)', () => {
    expect(webInterop.WEB_INTEROP_CONTRACT_VERSION).toBe('1.1.0');
  });

  it.each([
    'readPreferences',
    'writePreferences',
    'watchPreferences',
    'applyAppearance',
    'watchSystemAppearance',
    'registerShortcuts',
    'updateShortcuts',
    'defaultKeyMap',
    'captureChord',
    'isReservedChord',
    'startTour',
    'focusedHelpId',
    'captureFocus',
  ])('exports %s as a function', (name) => {
    expect(typeof (webInterop as Record<string, unknown>)[name]).toBe('function');
  });
});
