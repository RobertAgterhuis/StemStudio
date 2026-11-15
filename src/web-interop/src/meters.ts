/**
 * Expert dB readouts (ui-test-ids `meter-db-<key>`, FR-017): the audio engine writes `data-peak-db` into each
 * meter element every frame (audio-engine contract 2.0.0); this mirrors that value as text into the readout
 * next to the meter, without a round trip to .NET. Nothing is done when no readout exists (below Expert).
 *
 * @packageDocumentation
 */
import type { Disposable } from './types';
import { once } from './validation';

/** dBFS at or below which a meter counts as silent (the engine clamps to -60). */
const SILENCE_DB = -60;

/**
 * Formats a `data-peak-db` value: one decimal, `-inf` for silence or a missing value.
 *
 * @param value - The attribute value.
 * @returns The readout text.
 */
export function formatPeakDb(value: string | null): string {
  const db = value === null ? Number.NaN : Number(value);
  return Number.isFinite(db) && db > SILENCE_DB ? db.toFixed(1) : '-inf';
}

function mirror(root: ParentNode, meter: Element): void {
  const testId = meter.getAttribute('data-testid') ?? '';
  const readout = root.querySelector(`[data-testid="meter-db-${testId.slice('meter-'.length)}"]`);
  if (testId.startsWith('meter-') && readout !== null) {
    readout.textContent = formatPeakDb(meter.getAttribute('data-peak-db'));
  }
}

/**
 * Starts mirroring `data-peak-db` changes below a root element into the matching readouts.
 *
 * @param root - The element to observe (normally `document.body`).
 * @returns Handle that stops observing.
 */
export function startMeterReadouts(root: Element): Disposable {
  const observer = new MutationObserver((records) => {
    records.forEach((record) => {
      mirror(root, record.target as Element);
    });
  });
  observer.observe(root, { subtree: true, attributes: true, attributeFilter: ['data-peak-db'] });
  return once(() => {
    observer.disconnect();
  });
}
