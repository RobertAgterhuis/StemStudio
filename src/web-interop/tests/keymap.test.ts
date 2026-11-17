import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { captureChord } from '../src/capture';
import { CATALOGUE, defaultKeyMap, isReservedChord } from '../src/catalogue';
import { chordsClash, effectiveShift } from '../src/chords';
import { compileKeyMap, focusContext } from '../src/keymap';
import { registerShortcuts, updateShortcuts } from '../src/shortcuts';
import type { Disposable, KeyBinding, KeyMap, ShortcutScope } from '../src/types';

let registrations: Disposable[] = [];

function register(keyMap: KeyMap, scope: ShortcutScope = 'mixer'): { calls: ReturnType<typeof vi.fn>; handle: Disposable } {
  const calls = vi.fn(() => Promise.resolve(undefined));
  const handle = registerShortcuts({ invokeMethodAsync: calls }, scope, keyMap);
  registrations.push(handle);
  return { calls, handle };
}

function press(key: string, init: KeyboardEventInit = {}, target: EventTarget = document.body): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...init });
  target.dispatchEvent(event);
  return event;
}

function focusOn(html: string): Element {
  document.body.innerHTML = html;
  const target = document.body.querySelector('[data-target]');
  if (target === null) {
    throw new Error('no [data-target]');
  }

  return target;
}

function commands(calls: ReturnType<typeof vi.fn>): unknown[] {
  return calls.mock.calls.map((call: unknown[]) => call[1]);
}

function map(bindings: KeyBinding[], options: Partial<KeyMap> = {}): KeyMap {
  return { bindings, singleKey: true, pedalMode: false, ...options };
}

const PLAY: KeyBinding = { command: 'mixer.play-pause', chords: [{ key: ' ' }], layer: 'keys', owner: '001' };
const PEDAL: KeyBinding = { command: 'mixer.play-pause', chords: [{ key: 'pagedown' }], layer: 'pedal', owner: '001' };
const NEXT: KeyBinding = { command: 'mixer.section.next', chords: [{ key: 'n', shift: false }], layer: 'keys', owner: '014' };
const SAVE: KeyBinding = { command: 'mixer.save', chords: [{ key: 's', mod: true, shift: false }], layer: 'keys', owner: '004' };
const PALETTE: KeyBinding = { command: 'palette.open', chords: [{ key: 'k', mod: true, shift: false }], layer: 'keys', owner: '004', anywhere: true };

beforeEach(() => {
  vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (Windows NT 10.0; Win64; x64)');
});

afterEach(() => {
  registrations.forEach((r) => {
    r.dispose();
  });
  registrations = [];
  document.body.innerHTML = '';
  vi.useRealTimers();
});

describe('data-driven matching per scope (web-interop 1.1.0 5.2)', () => {
  it.each([
    ['mixer', ' ', {}, 'mixer.play-pause'],
    ['mixer', 'N', { shiftKey: true }, 'mixer.section.previous'],
    ['mixer', 'n', {}, 'mixer.section.next'],
    ['mixer', 'L', { shiftKey: true }, 'mixer.loop.section'],
    ['mixer', 'Backspace', {}, 'mixer.section.restart'],
    ['mixer', '0', {}, 'mixer.practice.reset'],
    ['mixer', 'K', { shiftKey: true }, 'mixer.countin.toggle'],
    ['mixer', 'k', {}, 'mixer.metronome.toggle'],
    ['mixer', 'f', {}, 'mixer.score.toggle'],
    ['mixer', 'PageDown', {}, 'mixer.play-pause'],
    ['mixer', 'PageUp', {}, 'mixer.section.restart'],
    ['mixer', 'S', { ctrlKey: true, shiftKey: true }, 'mixer.save-as'],
    ['setlist', 'ArrowRight', { ctrlKey: true }, 'setlist.next'],
    ['setlist', 'ArrowLeft', { ctrlKey: true }, 'setlist.previous'],
    ['library', '/', {}, 'library.search.focus'],
    ['global', '?', { shiftKey: true }, 'help.open'],
    ['global', 'F1', {}, 'help.open'],
  ] as const)('%s: %s %o -> %s', (scope, key, init, command) => {
    const { calls } = register(defaultKeyMap(scope), scope);
    const event = press(key, init);
    expect(commands(calls)).toEqual([command]);
    expect(event.defaultPrevented).toBe(true);
  });

  it('only fires the bindings of the given map', () => {
    const { calls } = register(map([NEXT]));
    press(' ');
    press('l');
    expect(commands(calls)).toEqual([]);
  });

  it('never dispatches the reserved 015 bindings (R and C)', () => {
    const { calls } = register(defaultKeyMap('mixer'));
    const event = press('r');
    press('c');
    expect(commands(calls)).toEqual([]);
    expect(event.defaultPrevented).toBe(false);
  });

  it('ignores Alt and the OS key, and the Mod key on macOS is Cmd', () => {
    const { calls } = register(map([NEXT, SAVE]));
    press('n', { altKey: true });
    press('n', { metaKey: true });
    press('s', { ctrlKey: true });
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0)');
    press('s', { ctrlKey: true });
    press('s', { metaKey: true });
    expect(commands(calls)).toEqual(['mixer.save', 'mixer.save']);
  });

  it('ignores repeats and keys another registration already handled', () => {
    const { calls } = register(map([NEXT]));
    press('n', { repeat: true });
    const handled = new KeyboardEvent('keydown', { key: 'n', bubbles: true, cancelable: true });
    handled.preventDefault();
    document.body.dispatchEvent(handled);
    expect(commands(calls)).toEqual([]);
  });
});

describe('guard contexts (rule 1)', () => {
  it.each([
    ['<input type="text" data-target>', 'text'],
    ['<textarea data-target></textarea>', 'text'],
    ['<select data-target></select>', 'text'],
    ['<div contenteditable="true" data-target></div>', 'text'],
    ['<div role="dialog"><button data-target>x</button></div>', 'text'],
    ['<div data-testid="tour-popover"><span data-target></span></div>', 'text'],
    ['<input type="range" data-target>', 'control'],
    ['<div role="slider" data-target></div>', 'control'],
    ['<ul role="listbox"><li data-target>x</li></ul>', 'control'],
    ['<div role="grid"><span data-target></span></div>', 'control'],
    ['<div role="menu" data-target></div>', 'control'],
    ['<div role="menubar" data-target></div>', 'control'],
    ['<div role="tablist" data-target></div>', 'control'],
    ['<button data-target>x</button>', 'none'],
  ])('%s is a %s context for letters', (html, context) => {
    expect(focusContext(focusOn(html), 'n')).toBe(context);
  });

  it.each([
    '<button data-target>x</button>',
    '<a href="#" data-target>x</a>',
    '<details><summary data-target>x</summary></details>',
    '<div role="button" data-target></div>',
    '<div role="checkbox" data-target></div>',
    '<div role="switch" data-target></div>',
  ])('Space and Enter keep their native action on %s', (html) => {
    const target = focusOn(html);
    expect(focusContext(target, ' ')).toBe('control');
    expect(focusContext(target, 'enter')).toBe('control');
  });

  it('treats a non-element target as no context', () => {
    expect(focusContext(null, 'n')).toBe('none');
    expect(focusContext(window, 'n')).toBe('none');
  });

  it('keeps Space on a focused button and fires it elsewhere', () => {
    const { calls } = register(map([PLAY]));
    const button = focusOn('<button data-target>Play</button>');
    expect(press(' ', {}, button).defaultPrevented).toBe(false);
    press(' ');
    expect(commands(calls)).toEqual(['mixer.play-pause']);
  });

  it('fires only anywhere Mod chords in a text field', () => {
    const { calls } = register(map([NEXT, SAVE, PALETTE]));
    const input = focusOn('<input type="text" data-target>');
    press('n', {}, input);
    press('s', { ctrlKey: true }, input);
    press('k', { ctrlKey: true }, input);
    expect(commands(calls)).toEqual(['palette.open']);
  });

  it('fires pedal chords on non-text controls only in pedal mode, never in text fields', () => {
    const off = register(map([PEDAL]));
    const slider = focusOn('<input type="range" data-target><input type="text">');
    const text = document.body.querySelector('input[type=text]') ?? slider;
    press('PageDown', {}, slider);
    expect(commands(off.calls)).toEqual([]);
    off.handle.dispose();

    const on = register(map([PEDAL, NEXT], { pedalMode: true }));
    press('PageDown', {}, slider);
    press('n', {}, slider);
    press('PageDown', {}, text);
    expect(commands(on.calls)).toEqual(['mixer.play-pause']);
  });
});

describe('single-key switch (FR-022, WCAG 2.2 SC 2.1.4)', () => {
  it('drops every chord without Mod, the pedal layer included', () => {
    const { calls } = register(map([NEXT, SAVE, PEDAL], { singleKey: false }));
    press('n');
    press('PageDown');
    press('s', { ctrlKey: true });
    expect(commands(calls)).toEqual(['mixer.save']);
  });

  it('turns the "G then X" sequences off as well', () => {
    const { calls } = register(map([], { singleKey: false }), 'global');
    press('g');
    press('i');
    expect(commands(calls)).toEqual([]);
  });

  it('keeps the sequences with single keys on', () => {
    const { calls } = register(defaultKeyMap('global'), 'global');
    press('g');
    press('t');
    expect(commands(calls)).toEqual(['go.tracks']);
  });
});

describe('duplicates and updates (rule 3)', () => {
  it('drops later duplicate chords and logs once', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    const twin: KeyBinding = { ...NEXT, command: 'mixer.loop.toggle' };
    const compiled = compileKeyMap(map([NEXT, twin, { ...twin, command: 'mixer.stop' }]));
    expect(compiled.map((c) => c.binding.command)).toEqual(['mixer.section.next']);
    expect(warn).toHaveBeenCalledTimes(1);
    expect(String(warn.mock.calls[0]?.[0])).toContain('mixer.loop.toggle, mixer.stop');
  });

  it('does not log without duplicates', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    compileKeyMap(defaultKeyMap('mixer'));
    expect(warn).not.toHaveBeenCalled();
  });

  it('updateShortcuts swaps the map without re-registering', () => {
    const { calls, handle } = register(map([NEXT]));
    press('n');
    updateShortcuts(handle, map([{ ...NEXT, chords: [{ key: 'j' }] }]));
    press('n');
    press('j');
    expect(commands(calls)).toEqual(['mixer.section.next', 'mixer.section.next']);
  });

  it('updateShortcuts ignores an unknown handle and a disposed one stops firing', () => {
    const { calls, handle } = register(map([NEXT]));
    expect(() => {
      updateShortcuts({ dispose: () => undefined }, map([]));
    }).not.toThrow();
    handle.dispose();
    handle.dispose();
    press('n');
    expect(commands(calls)).toEqual([]);
  });

  it('registerShortcuts without a key map keeps the 1.0.0 behaviour', () => {
    const calls = vi.fn(() => Promise.resolve(undefined));
    registrations.push(registerShortcuts({ invokeMethodAsync: calls }, 'mixer'));
    press(' ');
    press('r');
    press('l');
    expect(commands(calls)).toEqual(['mixer.practice.reset', 'mixer.loop.toggle']);
  });

  it('a 1.0.0 registration has no setlist table', () => {
    const calls = vi.fn(() => Promise.resolve(undefined));
    registrations.push(registerShortcuts({ invokeMethodAsync: calls }, 'setlist'));
    press('ArrowRight', { ctrlKey: true });
    expect(commands(calls)).toEqual([]);
  });
});

describe('catalogue (section 5.4)', () => {
  it('defaultKeyMap has every row of the scope and the default pedal preset', () => {
    const mixer = defaultKeyMap('mixer');
    expect(mixer.singleKey).toBe(true);
    expect(mixer.pedalMode).toBe(false);
    expect(mixer.bindings.filter((b) => b.layer === 'keys')).toHaveLength(CATALOGUE.commands.filter((c) => c.scope === 'mixer').length);
    expect(mixer.bindings.filter((b) => b.layer === 'pedal').map((b) => [b.command, b.chords[0]?.key])).toEqual([
      ['mixer.section.restart', 'pageup'],
      ['mixer.play-pause', 'pagedown'],
    ]);
    expect(defaultKeyMap('global').bindings.find((b) => b.command === 'palette.open')?.anywhere).toBe(true);
    expect(defaultKeyMap('setlist').bindings.every((b) => b.layer === 'keys')).toBe(true);
  });

  it.each([
    [{ key: 'tab' }, 'Moves the keyboard focus'],
    [{ key: 'tab', shift: true }, 'Moves the keyboard focus'],
    [{ key: 'f5' }, 'Reloads the page'],
    [{ key: 'r', mod: true }, 'Browser shortcut'],
    [{ key: 't', mod: true, shift: true }, 'Browser shortcut'],
    [{ key: 'capslock' }, 'Screen reader key'],
    [{ key: 'f8', shift: true }, 'Used elsewhere: Previous suggestion'],
    [{ key: 'j' }, null],
    [{ key: 'r' }, null],
  ])('isReservedChord(%j) = %s', (chord, reason) => {
    expect(isReservedChord(chord)).toBe(reason);
  });

  it('applies the Shift rule: forbidden for letters when omitted, unchecked for other keys', () => {
    expect(effectiveShift({ key: 'l' })).toBe(false);
    expect(effectiveShift({ key: 'home' })).toBeUndefined();
    expect(chordsClash({ key: 'l' }, { key: 'l', shift: true })).toBe(false);
    expect(chordsClash({ key: '/' }, { key: '/', shift: true })).toBe(true);
    expect(chordsClash({ key: '/' }, { key: '/', mod: true })).toBe(false);
  });
});

describe('captureChord (Learn)', () => {
  function capture(timeoutMs = 5000): ReturnType<typeof vi.fn> {
    const calls = vi.fn(() => Promise.resolve(undefined));
    registrations.push(captureChord({ invokeMethodAsync: calls }, { timeoutMs }));
    return calls;
  }

  it('reports the next chord once and prevents it', () => {
    const calls = capture();
    const shortcut = register(map([NEXT]));
    press('Shift', { shiftKey: true });
    const event = press('N', { shiftKey: true, ctrlKey: true });
    press('n');
    expect(calls.mock.calls).toEqual([['OnChordCaptured', { key: 'n', mod: true, shift: true }]]);
    expect(event.defaultPrevented).toBe(true);
    expect(commands(shortcut.calls)).toEqual(['mixer.section.next']);
  });

  it('ignores repeats and Alt chords, and reports pedal keys', () => {
    const calls = capture();
    press('b', { repeat: true });
    press('b', { altKey: true });
    press('PageDown');
    expect(calls.mock.calls).toEqual([['OnChordCaptured', { key: 'pagedown', mod: false, shift: false }]]);
  });

  it('Escape cancels with null', () => {
    const calls = capture();
    press('Escape');
    press('j');
    expect(calls.mock.calls).toEqual([['OnChordCaptured', null]]);
  });

  it('reports null after the timeout, clamped to 1..30 s', () => {
    vi.useFakeTimers();
    const short = capture(10);
    vi.advanceTimersByTime(999);
    expect(short).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(short.mock.calls).toEqual([['OnChordCaptured', null]]);

    const long = capture(60000);
    vi.advanceTimersByTime(29999);
    expect(long).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(long).toHaveBeenCalledTimes(1);
  });

  it('dispose stops capturing without a report', () => {
    vi.useFakeTimers();
    const calls = vi.fn(() => Promise.resolve(undefined));
    captureChord({ invokeMethodAsync: calls }, { timeoutMs: 1000 }).dispose();
    press('j');
    vi.advanceTimersByTime(2000);
    expect(calls).not.toHaveBeenCalled();
  });
});
