import { afterEach, describe, expect, it, vi } from 'vitest';
import { registerShortcuts } from '../src/shortcuts';
import type { Disposable } from '../src/types';

let registration: Disposable | undefined;

function register(scope: 'global' | 'mixer' = 'global'): ReturnType<typeof vi.fn> {
  const calls = vi.fn(() => Promise.resolve(undefined));
  registration = registerShortcuts({ invokeMethodAsync: calls }, scope);
  return calls;
}

function press(key: string, init: KeyboardEventInit = {}, target: EventTarget = document.body): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...init });
  target.dispatchEvent(event);
  return event;
}

function field(html: string): HTMLElement {
  document.body.innerHTML = html;
  const element = document.body.firstElementChild as HTMLElement;
  element.focus();
  return element;
}

afterEach(() => {
  registration?.dispose();
  registration = undefined;
  document.body.innerHTML = '';
});

describe('help keys (T048)', () => {
  it.each([['?'], ['F1']])('%s opens help', (key) => {
    const calls = register();
    const event = press(key);
    expect(calls).toHaveBeenCalledWith('OnCommand', 'help.open');
    expect(event.defaultPrevented).toBe(true);
  });

  it.each([
    ['<input type="text" />'],
    ['<input type="number" />'],
    ['<textarea></textarea>'],
    ['<select><option>a</option></select>'],
    ['<div contenteditable="true" tabindex="0">x</div>'],
    ['<div role="dialog"><button>x</button></div>'],
    ['<div data-testid="tour-popover"><button>x</button></div>'],
  ])('? is ignored while focus is in %s', (html) => {
    const calls = register();
    const target = field(html).querySelector('button') ?? document.activeElement ?? document.body;
    press('?', {}, target);
    expect(calls).not.toHaveBeenCalled();
  });

  it('ignores key repeats', () => {
    const calls = register();
    press('?', { repeat: true });
    expect(calls).not.toHaveBeenCalled();
  });

  it('dispose removes the listener', () => {
    const calls = register();
    registration?.dispose();
    registration?.dispose();
    press('?');
    expect(calls).not.toHaveBeenCalled();
  });
});
