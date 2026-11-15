import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { startTour, useDriverFactory } from '../src/tour';
import type { Disposable, TourRuntime } from '../src/types';
import { FakeDriver } from './fake-driver';
import { must } from './media';

let drivers: FakeDriver[];
let handle: Disposable | undefined;

const labels = { next: 'Next', previous: 'Back', done: 'Done', close: 'Close tour', progress: '{{current}} of {{total}}' };

function tour(targets: string[], reducedMotion = false): TourRuntime {
  return { id: 'import-basics', version: 1, reducedMotion, labels, steps: targets.map((t) => ({ target: t, title: `T ${t}`, text: `About ${t}` })) };
}

function page(...markers: string[]): void {
  document.body.innerHTML = markers.map((m) => `<button data-help-id="${m}">${m}</button>`).join('');
}

function start(t: TourRuntime): ReturnType<typeof vi.fn> {
  const calls = vi.fn(() => Promise.resolve(undefined));
  handle = startTour({ invokeMethodAsync: calls }, t);
  return calls;
}

function popover(): HTMLElement {
  const element = document.querySelector<HTMLElement>('[data-testid="tour-popover"]');
  if (!element) throw new Error('no popover');
  return element;
}

function key(name: string, shift = false): KeyboardEvent {
  const event = new KeyboardEvent('keydown', { key: name, shiftKey: shift, bubbles: true, cancelable: true });
  (document.activeElement ?? document.body).dispatchEvent(event);
  return event;
}

beforeEach(() => {
  drivers = [];
  useDriverFactory((config) => {
    const fake = new FakeDriver(config);
    drivers.push(fake);
    return fake;
  });
});

afterEach(() => {
  handle?.dispose();
  handle = undefined;
  document.body.innerHTML = '';
});

describe('startTour (web-interop.md 2-3, FR-027)', () => {
  it('skips missing and hidden targets', () => {
    page('import-url', 'legal-notice');
    must(document.querySelector<HTMLElement>('[data-help-id="legal-notice"]')).style.display = 'none';
    start(tour(['import-url', 'missing', 'legal-notice']));
    const steps = must(drivers[0]).config.steps ?? [];
    expect(steps).toHaveLength(1);
    expect(must(steps[0]).element).toBe(document.querySelector('[data-help-id="import-url"]'));
    expect(must(steps[0]).popover).toMatchObject({ title: 'T import-url', description: 'About import-url' });
  });

  it('treats an element inside [hidden] as hidden', () => {
    document.body.innerHTML = '<div hidden><button data-help-id="a">a</button></div><button data-help-id="b">b</button>';
    start(tour(['a', 'b']));
    expect(must(drivers[0]).config.steps).toHaveLength(1);
  });

  it('ends at once as dismissed with step 0 when no step is visible', () => {
    page();
    const calls = start(tour(['missing']));
    expect(drivers).toHaveLength(0);
    expect(calls).toHaveBeenCalledWith('OnTourEnded', 'import-basics', 'dismissed', 0);
  });

  it('labels the popover with test ids, step attributes and dialog semantics, and focuses Next', () => {
    page('a', 'b');
    start(tour(['a', 'b']));
    const element = popover();
    expect(element.dataset).toMatchObject({ tourId: 'import-basics', step: '1', steps: '2' });
    expect(element.getAttribute('role')).toBe('dialog');
    expect(element.querySelector('[data-testid="tour-next"]')).toBe(must(must(drivers[0]).popover).nextButton);
    expect(element.querySelector('[data-testid="tour-prev"]')).toBe(must(must(drivers[0]).popover).previousButton);
    expect(element.querySelector('[data-testid="tour-close"]')).toBe(must(must(drivers[0]).popover).closeButton);
    expect(document.activeElement).toBe(must(must(drivers[0]).popover).nextButton);
  });

  it('passes labels, progress and animation settings to driver.js', () => {
    page('a');
    start(tour(['a'], true));
    expect(must(drivers[0]).config).toMatchObject({
      animate: false,
      allowKeyboardControl: false,
      showProgress: true,
      progressText: '{{current}} of {{total}}',
      nextBtnText: 'Next',
      prevBtnText: 'Back',
      doneBtnText: 'Done',
      closeBtnLabel: 'Close tour',
      popoverClass: 'ss-tour',
    });
  });

  it('animates when motion is not reduced', () => {
    page('a');
    start(tour(['a']));
    expect(must(drivers[0]).config.animate).toBe(true);
  });

  it('ArrowRight and Enter move on, ArrowLeft goes back, Enter on the last step completes', () => {
    page('a', 'b');
    const calls = start(tour(['a', 'b']));
    key('ArrowRight');
    expect(popover().dataset.step).toBe('2');
    key('ArrowLeft');
    expect(popover().dataset.step).toBe('1');
    expect(key('Enter').defaultPrevented).toBe(true);
    key('Enter');
    expect(calls).toHaveBeenCalledWith('OnTourEnded', 'import-basics', 'completed', 1);
    expect(must(drivers[0]).destroyed).toBe(true);
  });

  it('ArrowLeft on the first step stays there', () => {
    page('a', 'b');
    start(tour(['a', 'b']));
    key('ArrowLeft');
    expect(popover().dataset.step).toBe('1');
  });

  it('Escape dismisses with the last step shown, and focus returns', () => {
    page('a', 'b');
    const opener = must(document.querySelector<HTMLElement>('[data-help-id="b"]'));
    opener.focus();
    const calls = start(tour(['a', 'b']));
    key('ArrowRight');
    key('Escape');
    expect(calls).toHaveBeenCalledWith('OnTourEnded', 'import-basics', 'dismissed', 1);
    expect(document.activeElement).toBe(opener);
  });

  it('the popover buttons drive the tour too', () => {
    page('a', 'b');
    const calls = start(tour(['a', 'b']));
    must(must(drivers[0]).popover).nextButton.click();
    must(must(drivers[0]).popover).previousButton.click();
    expect(popover().dataset.step).toBe('1');
    must(must(drivers[0]).popover).closeButton.click();
    expect(calls).toHaveBeenCalledWith('OnTourEnded', 'import-basics', 'dismissed', 0);
  });

  it('traps Tab inside the popover', () => {
    page('a', 'b');
    start(tour(['a', 'b']));
    const buttons = [...popover().querySelectorAll('button')];
    must(buttons.at(-1)).focus();
    expect(key('Tab').defaultPrevented).toBe(true);
    expect(document.activeElement).toBe(buttons[0]);
    key('Tab', true);
    expect(document.activeElement).toBe(buttons.at(-1));
    must(buttons[1]).focus();
    expect(key('Tab').defaultPrevented).toBe(false);
  });

  it('ignores other keys', () => {
    page('a', 'b');
    const calls = start(tour(['a', 'b']));
    expect(key('x').defaultPrevented).toBe(false);
    expect(calls).not.toHaveBeenCalled();
  });

  it('reports once even when driver.js destroys itself afterwards', () => {
    page('a');
    const calls = start(tour(['a']));
    key('Escape');
    must(drivers[0]).destroy();
    expect(calls).toHaveBeenCalledTimes(1);
  });

  it('reports dismissed when driver.js is destroyed from outside the wrapper', () => {
    page('a');
    const calls = start(tour(['a']));
    must(drivers[0]).destroy();
    expect(calls).toHaveBeenCalledWith('OnTourEnded', 'import-basics', 'dismissed', 0);
  });

  it('dispose destroys the driver without a callback and stops listening', () => {
    page('a', 'b');
    const calls = start(tour(['a', 'b']));
    handle?.dispose();
    handle?.dispose();
    key('ArrowRight');
    expect(must(drivers[0]).destroyed).toBe(true);
    expect(calls).not.toHaveBeenCalled();
  });

  it('reports step 0 when driver.js has no active step', () => {
    page('a');
    const calls = start(tour(['a']));
    must(drivers[0]).index = -1;
    must(drivers[0]).destroy();
    expect(calls).toHaveBeenCalledWith('OnTourEnded', 'import-basics', 'dismissed', 0);
  });

  it('uses driver.js by default', async () => {
    const module = await import('../src/tour');
    const real = module.defaultDriverFactory({ steps: [] });
    expect(typeof real.drive).toBe('function');
    expect(real.isLastStep()).toBe(false);
  });
});
