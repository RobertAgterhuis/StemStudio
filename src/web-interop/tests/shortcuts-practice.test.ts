import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { registerShortcuts } from '../src/shortcuts';
import type { Disposable, ShortcutScope } from '../src/types';

/** Practice shortcuts of feature 012 (ui-test-ids 1.11.0 "Shortcut commands", FR-017). */

let registrations: Disposable[] = [];

function register(scope: ShortcutScope): ReturnType<typeof vi.fn> {
  const calls = vi.fn(() => Promise.resolve(undefined));
  registrations.push(registerShortcuts({ invokeMethodAsync: calls }, scope));
  return calls;
}

function press(key: string, init: KeyboardEventInit = {}, target: EventTarget = document.body): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...init });
  target.dispatchEvent(event);
  return event;
}

function commands(calls: ReturnType<typeof vi.fn>): unknown[] {
  return calls.mock.calls.map((call: unknown[]) => call[1]);
}

beforeEach(() => {
  vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (Windows NT 10.0; Win64; x64)');
});

afterEach(() => {
  registrations.forEach((r) => {
    r.dispose();
  });
  registrations = [];
  document.body.innerHTML = '';
});

describe('practice keys in the mixer', () => {
  it.each([
    ['-', 'mixer.speed:-5'],
    ['=', 'mixer.speed:5'],
    ['+', 'mixer.speed:5'],
    [',', 'mixer.transpose:-1'],
    ['.', 'mixer.transpose:1'],
    ['r', 'mixer.practice.reset'],
    ['c', 'mixer.countin.toggle'],
    ['t', 'mixer.trainer.toggle'],
  ])('%s -> %s', (key, command) => {
    const calls = register('mixer');
    const event = press(key);
    expect(commands(calls)).toEqual([command]);
    expect(event.defaultPrevented).toBe(true);
  });

  it('allows Shift (= with Shift is + on most layouts)', () => {
    const calls = register('mixer');
    press('+', { shiftKey: true });
    press('R', { shiftKey: true });
    expect(commands(calls)).toEqual(['mixer.speed:5', 'mixer.practice.reset']);
  });

  it.each([
    [{ ctrlKey: true }],
    [{ altKey: true }],
    [{ metaKey: true }],
  ])('ignores the keys with %o', (init) => {
    const calls = register('mixer');
    for (const key of ['-', '=', ',', '.', 'r', 'c', 't']) {
      press(key, init);
    }
    expect(commands(calls)).toEqual([]);
  });

  it.each([
    ['<input type="text">'],
    ['<input type="range">'],
    ['<textarea></textarea>'],
    ['<div role="dialog"><button>x</button></div>'],
  ])('ignores the keys in a typing context or dialog: %s', (html) => {
    document.body.innerHTML = html;
    const target = document.body.querySelector('button') ?? (document.body.firstElementChild as Element);
    const calls = register('mixer');
    for (const key of ['-', '=', ',', '.', 'r', 'c', 't']) {
      press(key, {}, target);
    }
    expect(commands(calls)).toEqual([]);
  });

  it('are not bound outside the mixer', () => {
    const calls = register('global');
    for (const key of ['-', '=', ',', '.', 'r', 'c']) {
      press(key);
    }
    expect(commands(calls)).toEqual([]);
  });

  it('G then T still goes to the tracks and does not toggle the trainer', () => {
    const global = register('global');
    const mixer = register('mixer');
    press('g');
    press('t');
    expect(commands(global)).toEqual(['go.tracks']);
    expect(commands(mixer)).toEqual([]);
  });
});
