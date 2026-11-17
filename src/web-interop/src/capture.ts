/**
 * "Learn" a chord (web-interop 1.1.0 section 5.1 `captureChord`): the next key press anywhere is reported once to
 * `OnChordCaptured(chord | null)` and prevented; Escape and the timeout report null. Lone modifier keys, repeats and
 * Alt/OS-key chords are ignored (they can never be bound).
 *
 * @packageDocumentation
 */
import { inputOf } from './chords';
import type { Disposable, DotNetRef, KeyChord } from './types';
import { once } from './validation';

/** Shortest and longest capture timeout. */
export const CAPTURE_TIMEOUT_RANGE = { min: 1000, max: 30000 } as const;

const MODIFIERS = new Set(['control', 'shift', 'alt', 'meta', 'os', 'altgraph']);

/** Options of {@link captureChord}. */
export interface CaptureOptions {
  /** Timeout in milliseconds, clamped to 1000..30000. */
  readonly timeoutMs: number;
}

function captured(event: KeyboardEvent): KeyChord | null | undefined {
  const input = inputOf(event);
  if (event.repeat || input.foreign || MODIFIERS.has(input.key)) {
    return undefined;
  }

  return input.key === 'escape' ? null : { key: input.key, mod: input.mod, shift: input.shift };
}

/**
 * Starts capturing the next chord.
 *
 * @param dotnet - Receiver of `OnChordCaptured`.
 * @param options - The timeout.
 * @returns Handle that stops capturing without a report.
 */
export function captureChord(dotnet: DotNetRef, options: CaptureOptions): Disposable {
  const timeout = Math.min(CAPTURE_TIMEOUT_RANGE.max, Math.max(CAPTURE_TIMEOUT_RANGE.min, options.timeoutMs));
  const report = (chord: KeyChord | null): void => {
    registration.dispose();
    void dotnet.invokeMethodAsync('OnChordCaptured', chord);
  };
  const onKeyDown = (event: KeyboardEvent): void => {
    const chord = captured(event);
    if (chord !== undefined) {
      event.preventDefault();
      event.stopImmediatePropagation();
      report(chord);
    }
  };
  const timer = setTimeout(() => {
    report(null);
  }, timeout);
  document.addEventListener('keydown', onKeyDown, true);
  const registration = once(() => {
    clearTimeout(timer);
    document.removeEventListener('keydown', onKeyDown, true);
  });
  return registration;
}
