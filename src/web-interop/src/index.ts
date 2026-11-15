/**
 * StemStudio web-interop entry point (ES module loaded by Blazor via JS interop), contract
 * web-interop.md 1.0.0. The functions live in the topic modules
 * (preferences, theme, shortcuts, tour, focus) and are re-exported here.
 *
 * @packageDocumentation
 */
import { startMeterReadouts } from './meters';
import type { WebInteropContractVersion } from './types';

export type * from './types';
export { readPreferences, writePreferences, watchPreferences } from './preferences';
export { applyAppearance, watchSystemAppearance } from './theme';
export { registerShortcuts } from './shortcuts';
export { startTour } from './tour';
export { captureFocus, focusedHelpId } from './focus';
export { startMeterReadouts } from './meters';

/** Version of the web-interop contract implemented by this module. */
export const WEB_INTEROP_CONTRACT_VERSION: WebInteropContractVersion = '1.0.0';


// Expert dB readouts follow the engine's meter attributes for the life of the page (no .NET round trip).
startMeterReadouts(document.body);
