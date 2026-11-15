/**
 * SC-004 probe (ADR-008 #5): records when the audio engine applies a mixer change.
 *
 * An init script wraps the `AudioParam` automation methods and the `value` setter; every call is
 * stored with its `performance.now()` timestamp in `window.__stemStudioE2E.calls`. The engine
 * applies `setChannel`/`setMaster` through exactly these calls (contracts/audio-engine.md, gain
 * ramps <= 10 ms), so "user action -> first AudioParam call" bounds "user action -> engine".
 *
 * @packageDocumentation
 */
import type { Locator, Page } from '@playwright/test';

/** One recorded AudioParam call. */
export interface ProbeCall {
  readonly method: string;
  readonly at: number;
}

/** What a measured action does to the control, executed inside the page. */
export type ProbeAction =
  | { readonly kind: 'click' }
  | { readonly kind: 'value'; readonly value: number }
  | { readonly kind: 'key'; readonly key: string };

interface ProbeState {
  calls: ProbeCall[];
  mark?: { readonly before: number; readonly start: number };
}

interface ProbeWindow {
  __stemStudioE2E?: ProbeState;
}

/** Installs the probe before any page script runs (call before `page.goto`). */
export async function installEngineProbe(page: Page): Promise<void> {
  await page.addInitScript(probeInitScript);
}

function probeInitScript(): void {
  const calls: ProbeCall[] = [];
  (window as ProbeWindow).__stemStudioE2E = { calls };
  const proto = AudioParam.prototype as unknown as Record<string, (...args: unknown[]) => unknown>;
  for (const method of ['setValueAtTime', 'setTargetAtTime', 'linearRampToValueAtTime']) {
    const original = proto[method];
    if (original === undefined) {
      continue;
    }
    proto[method] = function (this: AudioParam, ...args: unknown[]): unknown {
      calls.push({ method, at: performance.now() });
      return original.apply(this, args);
    };
  }
  const value = Object.getOwnPropertyDescriptor(AudioParam.prototype, 'value');
  Object.defineProperty(AudioParam.prototype, 'value', {
    get(this: AudioParam): unknown {
      return value?.get?.call(this);
    },
    set(this: AudioParam, v: number): void {
      calls.push({ method: 'value', at: performance.now() });
      value?.set?.call(this, v);
    },
  });
}

/**
 * Performs `action` on `control` inside the page and returns the milliseconds until the engine's
 * first AudioParam call after it; `Infinity` if none arrives within `windowMs`. Both timestamps
 * are taken in the page, so Playwright's round trips are not part of the measurement.
 */
export async function measureActionToEngineMs(
  control: Locator,
  action: ProbeAction,
  windowMs = 1000,
): Promise<number> {
  await control.evaluate(markAndAct, action);
  return control.page().evaluate(waitForEngineCall, windowMs);
}

/**
 * Chooses the in-page action that moves a slider: a value change for native inputs, an
 * `ArrowLeft` key press for ARIA sliders.
 */
export async function sliderAction(control: Locator, value: number): Promise<ProbeAction> {
  const isInput = await control.evaluate((el) => el instanceof HTMLInputElement);
  return isInput ? { kind: 'value', value } : { kind: 'key', key: 'ArrowLeft' };
}

/** Controls of one SC-004 measurement: a fader moved to 50 %, a mute and a solo toggle. */
export interface Sc004Controls {
  readonly fader: Locator;
  readonly mute: Locator;
  readonly solo: Locator;
}

/** Milliseconds from user action to the engine's first AudioParam call, per control. */
export async function measureSc004(controls: Sc004Controls): Promise<Record<keyof Sc004Controls, number>> {
  return {
    fader: await measureActionToEngineMs(controls.fader, await sliderAction(controls.fader, 50)),
    mute: await measureActionToEngineMs(controls.mute, { kind: 'click' }),
    solo: await measureActionToEngineMs(controls.solo, { kind: 'click' }),
  };
}

// markAndAct and waitForEngineCall are serialised into the page by Playwright: they must not
// reference anything outside their own bodies.
function markAndAct(el: Element, action: ProbeAction): void {
  const state = (window as ProbeWindow).__stemStudioE2E;
  if (state === undefined) {
    throw new Error('engine probe not installed (call installEngineProbe before page.goto)');
  }
  state.mark = { before: state.calls.length, start: performance.now() };
  if (action.kind === 'click') {
    (el as HTMLElement).click();
  } else if (action.kind === 'key') {
    el.dispatchEvent(new KeyboardEvent('keydown', { key: action.key, bubbles: true }));
  } else {
    (el as HTMLInputElement).value = String(action.value);
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  }
}

function waitForEngineCall(windowMs: number): Promise<number> {
  const state = (window as ProbeWindow).__stemStudioE2E;
  const mark = state?.mark ?? { before: 0, start: performance.now() };
  return new Promise((resolve) => {
    const check = (): void => {
      const first = state?.calls[mark.before];
      if (first !== undefined) {
        resolve(first.at - mark.start);
      } else if (performance.now() - mark.start > windowMs) {
        resolve(Number.POSITIVE_INFINITY);
      } else {
        requestAnimationFrame(check);
      }
    };
    check();
  });
}
