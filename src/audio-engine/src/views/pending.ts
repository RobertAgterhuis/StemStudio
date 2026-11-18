/**
 * Placeholder of the instrument-view surface (contract 2.6.0, feature 016, architect skeleton T004). Every method
 * throws until the audio-engine lane replaces this module with `timeline/`, `chords/`, `drums/`, `pitch/` and
 * `singing/` (tasks T053-T126).
 *
 * @packageDocumentation
 */

/** Message of every not-yet-built 2.6.0 method. */
export const PENDING_VIEWS_MESSAGE = 'not implemented yet (contract 2.6.0)';

/**
 * Throws the pending error for a 2.6.0 method.
 *
 * @param method - Name of the method (part of the message).
 * @param args - The call's arguments (accepted so the signatures stay those of the contract; not used).
 * @returns Never returns.
 */
export function pendingView(method: string, ...args: readonly unknown[]): never {
  throw new Error(`${method}: ${PENDING_VIEWS_MESSAGE} (${String(args.length)} arguments)`);
}
