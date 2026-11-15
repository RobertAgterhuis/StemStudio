import { describe, expect, it } from 'vitest';
import {
  WEB_INTEROP_CONTRACT_VERSION,
  captureFocus,
  focusedHelpId,
  startTour,
  type DotNetRef,
  type TourRuntime,
} from '../src/index';

const dotnet: DotNetRef = { invokeMethodAsync: () => Promise.resolve(undefined) };

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
    ['startTour', () => startTour(dotnet, tour)],
    ['focusedHelpId', () => focusedHelpId()],
    ['captureFocus', () => captureFocus()],
  ])('%s is a stub that throws until implemented', (name, call) => {
    expect(call).toThrow(`not implemented: ${name}`);
  });
});
