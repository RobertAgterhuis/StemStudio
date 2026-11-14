/**
 * Generic interaction with the mixer's form controls. The UI test-id contract puts each id on the
 * interactive element and exposes slider values via `aria-valuenow`; these helpers work for native
 * inputs (range/number/text) and for ARIA sliders and toggle buttons.
 *
 * @packageDocumentation
 */
import { expect, type Locator } from '@playwright/test';

const maxKeySteps = 250;

/** Numeric value of a control: `aria-valuenow` if present, else the input's value. */
export async function numericValue(control: Locator): Promise<number> {
  const aria = await control.getAttribute('aria-valuenow');
  return Number(aria ?? (await control.inputValue()));
}

/**
 * Sets a numeric control. Native inputs get the value plus `input` and `change` events (Blazor
 * binds either); ARIA sliders are moved with the arrow keys until `aria-valuenow` matches.
 */
export async function setNumericValue(control: Locator, value: number): Promise<void> {
  const isInput = await control.evaluate((el) => el instanceof HTMLInputElement);
  if (isInput) {
    await control.evaluate(assignInputValue, String(value));
  } else {
    await stepSliderTo(control, value);
  }
  await expect.poll(() => numericValue(control)).toBeCloseTo(value, 0);
}

function assignInputValue(el: Element, value: string): void {
  const input = el as HTMLInputElement;
  input.focus();
  input.value = value;
  input.dispatchEvent(new Event('input', { bubbles: true }));
  input.dispatchEvent(new Event('change', { bubbles: true }));
  input.blur();
}

async function stepSliderTo(slider: Locator, value: number): Promise<void> {
  await slider.focus();
  for (let step = 0; step < maxKeySteps; step++) {
    const current = await numericValue(slider);
    if (Math.abs(current - value) < 0.5) {
      return;
    }
    await slider.press(current < value ? 'ArrowRight' : 'ArrowLeft');
  }
}

/** On/off state of a toggle: `aria-pressed`, `aria-checked` or the checkbox's `checked`. */
export async function isOn(toggle: Locator): Promise<boolean> {
  return toggle.evaluate((el) => {
    const aria = el.getAttribute('aria-pressed') ?? el.getAttribute('aria-checked');
    if (aria !== null) {
      return aria === 'true';
    }
    return el instanceof HTMLInputElement ? el.checked : el.classList.contains('active');
  });
}

/** Clicks a toggle until it reaches the wanted state (at most once). */
export async function setToggle(toggle: Locator, on: boolean): Promise<void> {
  if ((await isOn(toggle)) !== on) {
    await toggle.click();
  }
  await expect.poll(() => isOn(toggle)).toBe(on);
}

/** Fills a text/number input and commits it with Tab (fires `change`). */
export async function fillAndCommit(input: Locator, value: string): Promise<void> {
  await input.fill(value);
  await input.press('Tab');
}

/** Peak level written by the engine into a meter (`data-peak-db`); -60 when not written yet. */
export async function peakDb(meter: Locator): Promise<number> {
  return Number((await meter.getAttribute('data-peak-db')) ?? '-60');
}
