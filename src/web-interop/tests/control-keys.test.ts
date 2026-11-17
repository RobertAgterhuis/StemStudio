import { afterEach, describe, expect, it, vi } from 'vitest';
import { startControlKeys } from '../src/control-keys';
import type { Disposable } from '../src/types';

/** Keyboard help for the practice controls (012 FR-017): Page Up/Down steps, read-only sliders, spinbuttons. */

let handle: Disposable | undefined;

afterEach(() => {
  handle?.dispose();
  handle = undefined;
  document.body.replaceChildren();
});

function slider(attributes: Record<string, string>): HTMLInputElement {
  const input = document.createElement('input');
  input.type = 'range';
  input.min = '50';
  input.max = '120';
  input.step = '1';
  for (const [name, value] of Object.entries(attributes)) {
    input.setAttribute(name, value);
  }
  document.body.append(input);
  return input;
}

function spin(): HTMLElement {
  const element = document.createElement('div');
  element.setAttribute('role', 'spinbutton');
  element.setAttribute('data-ss-spin', '');
  element.tabIndex = 0;
  document.body.append(element);
  return element;
}

function press(target: EventTarget, key: string): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true });
  target.dispatchEvent(event);
  return event;
}

describe('Page Up/Down on sliders with data-page-step', () => {
  it.each([
    ['PageUp', '75', '85'],
    ['PageDown', '75', '65'],
    ['PageUp', '115', '120'],
    ['PageDown', '55', '50'],
  ])('%s from %s -> %s, with input and change events', (key, from, to) => {
    handle = startControlKeys(document);
    const input = slider({ 'data-page-step': '10' });
    input.value = from;
    const inputs = vi.fn();
    const changes = vi.fn();
    input.addEventListener('input', inputs);
    input.addEventListener('change', changes);

    const event = press(input, key);

    expect(event.defaultPrevented).toBe(true);
    expect(input.value).toBe(to);
    expect(inputs).toHaveBeenCalledOnce();
    expect(changes).toHaveBeenCalledOnce();
  });

  it('leaves other keys and sliders without data-page-step to the browser', () => {
    handle = startControlKeys(document);
    const stepped = slider({ 'data-page-step': '10' });
    const plain = slider({});

    expect(press(stepped, 'ArrowUp').defaultPrevented).toBe(false);
    expect(press(plain, 'PageUp').defaultPrevented).toBe(false);
  });

  it('does not change a disabled slider', () => {
    handle = startControlKeys(document);
    const input = slider({ 'data-page-step': '10' });
    input.value = '75';
    input.disabled = true;

    press(input, 'PageUp');

    expect(input.value).toBe('75');
  });
});

describe('read-only sliders', () => {
  it.each(['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', 'PageUp', 'PageDown'])('block %s', (key) => {
    handle = startControlKeys(document);
    const input = slider({ 'aria-readonly': 'true', 'data-page-step': '10' });
    input.value = '65';
    const inputs = vi.fn();
    input.addEventListener('input', inputs);

    expect(press(input, key).defaultPrevented).toBe(true);
    expect(input.value).toBe('65');
    expect(inputs).not.toHaveBeenCalled();
  });

  it('keep Tab', () => {
    handle = startControlKeys(document);
    const input = slider({ 'aria-readonly': 'true' });

    expect(press(input, 'Tab').defaultPrevented).toBe(false);
  });
});

describe('spinbuttons with data-ss-spin', () => {
  it.each([
    ['ArrowUp', true],
    ['ArrowDown', true],
    ['ArrowLeft', false],
    ['Tab', false],
  ])('%s prevented: %s (no page scroll)', (key, prevented) => {
    handle = startControlKeys(document);

    expect(press(spin(), key).defaultPrevented).toBe(prevented);
  });
});

describe('lifetime', () => {
  it('stops after dispose', () => {
    const started = startControlKeys(document);
    const input = slider({ 'data-page-step': '10' });
    input.value = '75';

    started.dispose();
    started.dispose();
    press(input, 'PageUp');

    expect(input.value).toBe('75');
  });

  it('ignores events from non-elements', () => {
    handle = startControlKeys(document);

    expect(press(document, 'PageUp').defaultPrevented).toBe(false);
  });
});
