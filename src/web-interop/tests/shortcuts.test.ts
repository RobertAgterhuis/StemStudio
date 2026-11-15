import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { registerShortcuts } from '../src/shortcuts';
import type { Disposable, ShortcutScope } from '../src/types';

let registrations: Disposable[] = [];

function register(scope: ShortcutScope = 'global'): ReturnType<typeof vi.fn> {
  const calls = vi.fn(() => Promise.resolve(undefined));
  registrations.push(registerShortcuts({ invokeMethodAsync: calls }, scope));
  return calls;
}

function press(key: string, init: KeyboardEventInit = {}, target: EventTarget = document.body): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...init });
  target.dispatchEvent(event);
  return event;
}

function field(html: string): Element {
  document.body.innerHTML = html;
  const element = document.body.firstElementChild as HTMLElement;
  return element.querySelector('button') ?? element;
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
  vi.useRealTimers();
});

describe('global key map (design-spec.md 6)', () => {
  it.each([
    ['?', {}, 'help.open'],
    ['?', { shiftKey: true }, 'help.open'],
    ['F1', {}, 'help.open'],
    ['k', { ctrlKey: true }, 'palette.open'],
    ['K', { ctrlKey: true, shiftKey: true }, null],
    ['/', { ctrlKey: true }, 'shortcuts.open'],
  ])('%s %o -> %s', (key, init, command) => {
    const calls = register();
    const event = press(key, init);
    expect(commands(calls)).toEqual(command === null ? [] : [command]);
    expect(event.defaultPrevented).toBe(command !== null);
  });

  it.each([
    ['i', 'go.import'],
    ['t', 'go.tracks'],
    ['m', 'go.mixes'],
  ])('G then %s goes to %s', (key, command) => {
    const calls = register();
    press('g');
    press(key);
    expect(commands(calls)).toEqual([command]);
  });

  it('G sequences time out after 1 s', () => {
    vi.useFakeTimers();
    const calls = register();
    press('g');
    vi.advanceTimersByTime(1001);
    press('i');
    expect(calls).not.toHaveBeenCalled();
  });

  it('G followed by another key cancels the sequence', () => {
    const calls = register();
    press('g');
    press('x');
    press('i');
    expect(calls).not.toHaveBeenCalled();
  });

  it('the mixer letters are not global', () => {
    const calls = register();
    press('l');
    press('Home');
    expect(calls).not.toHaveBeenCalled();
  });
});

describe('mixer key map (design-spec.md 6)', () => {
  it.each([
    ['Home', {}, 'mixer.stop'],
    ['ArrowLeft', {}, 'mixer.seek:-5'],
    ['ArrowRight', {}, 'mixer.seek:5'],
    ['ArrowLeft', { shiftKey: true }, 'mixer.seek:-1'],
    ['ArrowRight', { shiftKey: true }, 'mixer.seek:1'],
    ['[', {}, 'mixer.loop.a'],
    [']', {}, 'mixer.loop.b'],
    ['l', {}, 'mixer.loop.toggle'],
    ['L', { shiftKey: true }, null],
    ['k', {}, 'mixer.metronome.toggle'],
    ['1', {}, 'mixer.channel.select:1'],
    ['9', {}, 'mixer.channel.select:9'],
    ['0', {}, null],
    ['m', {}, 'mixer.channel.mute'],
    ['s', {}, 'mixer.channel.solo'],
    ['s', { ctrlKey: true }, 'mixer.save'],
    ['S', { ctrlKey: true, shiftKey: true }, 'mixer.save-as'],
    ['l', { ctrlKey: true }, null],
    ['l', { altKey: true }, null],
    ['l', { metaKey: true }, null],
    ['F1', {}, null],
  ])('%s %o -> %s', (key, init, command) => {
    const calls = register('mixer');
    const event = press(key, init);
    expect(commands(calls)).toEqual(command === null ? [] : [command]);
    expect(event.defaultPrevented).toBe(command !== null);
  });

  it('a global G sequence wins over the mixer letters', () => {
    const global = register('global');
    const mixer = register('mixer');
    press('g');
    press('m');
    expect(commands(global)).toEqual(['go.mixes']);
    expect(mixer).not.toHaveBeenCalled();
  });

  it('never handles Space', () => {
    const calls = register('mixer');
    const global = register('global');
    const event = press(' ');
    expect(calls).not.toHaveBeenCalled();
    expect(global).not.toHaveBeenCalled();
    expect(event.defaultPrevented).toBe(false);
  });
});

describe('typing guard', () => {
  it.each([
    ['<input type="text" />'],
    ['<input type="number" />'],
    ['<input type="range" />'],
    ['<textarea></textarea>'],
    ['<select><option>a</option></select>'],
    ['<div contenteditable="true">x</div>'],
    ['<div role="dialog"><button>x</button></div>'],
    ['<div data-testid="tour-popover"><button>x</button></div>'],
  ])('ignores shortcuts while focus is in %s', (html) => {
    const mixer = register('mixer');
    const global = register('global');
    const target = field(html);
    press('l', {}, target);
    press('?', {}, target);
    press('s', { ctrlKey: true }, target);
    expect(mixer).not.toHaveBeenCalled();
    expect(global).not.toHaveBeenCalled();
  });

  it('Mod+K still opens the palette from a text field', () => {
    const calls = register();
    const target = field('<input type="text" />');
    press('k', { ctrlKey: true }, target);
    expect(commands(calls)).toEqual(['palette.open']);
  });

  it('ignores key repeats', () => {
    const calls = register('mixer');
    press('l', { repeat: true });
    expect(calls).not.toHaveBeenCalled();
  });
});

describe('platform and lifetime', () => {
  it('Mod is Meta on macOS user agents', () => {
    vi.spyOn(navigator, 'userAgent', 'get').mockReturnValue('Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0)');
    const calls = register();
    press('k', { ctrlKey: true });
    press('k', { metaKey: true });
    expect(commands(calls)).toEqual(['palette.open']);
  });

  it('dispose removes the listener (twice is safe)', () => {
    const calls = register('mixer');
    const registration = registrations.pop();
    registration?.dispose();
    registration?.dispose();
    press('l');
    expect(calls).not.toHaveBeenCalled();
  });
});
