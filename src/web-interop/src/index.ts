/**
 * StemStudio web-interop entry point (ES module loaded by Blazor via JS interop), contract
 * web-interop.md 1.0.0. Phase 0 skeleton: every function throws until the frontend agent
 * implements it (tasks T013-T059); the signatures are binding.
 *
 * @packageDocumentation
 */
import type {
  Disposable,
  DotNetRef,
  TourRuntime,
  WebInteropContractVersion,
} from './types';

export type * from './types';
export { readPreferences, writePreferences, watchPreferences } from './preferences';
export { applyAppearance, watchSystemAppearance } from './theme';
export { registerShortcuts } from './shortcuts';

/** Version of the web-interop contract implemented by this module. */
export const WEB_INTEROP_CONTRACT_VERSION: WebInteropContractVersion = '1.0.0';

function notImplemented(name: string, args: readonly unknown[]): never {
  throw new Error(`not implemented: ${name} (${String(args.length)} argument(s))`);
}

/**
 * Starts a tour (driver.js); steps whose target is missing or hidden are skipped. Calls
 * `dotnet.invokeMethodAsync("OnTourEnded", tourId, "completed" | "dismissed", lastStep)`.
 *
 * @param dotnet - Receiver of the end callback.
 * @param tour - Tour with resolved texts.
 * @returns Handle that destroys the running tour.
 */
export function startTour(dotnet: DotNetRef, tour: TourRuntime): Disposable {
  return notImplemented('startTour', [dotnet, tour]);
}

/**
 * Returns the `data-help-id` of the focused element or of its closest ancestor.
 *
 * @returns The help id, or null when none applies.
 */
export function focusedHelpId(): string | null {
  return notImplemented('focusedHelpId', []);
}

/**
 * Remembers the focused element.
 *
 * @returns Handle that restores focus to that element on dispose.
 */
export function captureFocus(): Disposable {
  return notImplemented('captureFocus', []);
}
