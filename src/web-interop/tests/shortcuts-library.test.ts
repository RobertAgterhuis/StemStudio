import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { registerShortcuts } from '../src/shortcuts';
import type { Disposable } from '../src/types';

let registrations: Disposable[] = [];

function register(scope: 'global' | 'library'): ReturnType<typeof vi.fn> {
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

describe('library key map (feature 022, web-interop 1.2.0)', () => {
  it.each([
    ['/', {}, 'library.search.focus'],
    ['/', { shiftKey: true }, 'library.search.focus'],
    ['/', { ctrlKey: true }, null],
    ['/', { altKey: true }, null],
    ['/', { metaKey: true }, null],
    ['k', { ctrlKey: true }, null],
    ['?', {}, null],
  ])('%s %o -> %s', (key, init, command) => {
    const calls = register('library');
    const event = press(key, init);
    expect(commands(calls)).toEqual(command === null ? [] : [command]);
    expect(event.defaultPrevented).toBe(command !== null);
  });

  it.each(['<input type="search">', '<textarea></textarea>', '<div contenteditable="true"></div>', '<div role="dialog"><button>x</button></div>'])(
    'is ignored while typing in %s',
    (html) => {
      document.body.innerHTML = html;
      const target = document.body.querySelector('button') ?? (document.body.firstElementChild as HTMLElement);
      const calls = register('library');
      const event = press('/', {}, target);
      expect(calls).not.toHaveBeenCalled();
      expect(event.defaultPrevented).toBe(false);
    },
  );

  it('does not conflict with the global Mod+K and Mod+/', () => {
    const global = register('global');
    const library = register('library');
    press('k', { ctrlKey: true });
    press('/', { ctrlKey: true });
    press('/');
    expect(commands(global)).toEqual(['palette.open', 'shortcuts.open']);
    expect(commands(library)).toEqual(['library.search.focus']);
  });

  it('/ is not a global key', () => {
    const calls = register('global');
    press('/');
    expect(calls).not.toHaveBeenCalled();
  });
});
