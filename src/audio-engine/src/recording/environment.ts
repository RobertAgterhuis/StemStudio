/**
 * The browser facilities the recording needs (contract 2.5.0), read once from the page or from the test seams of
 * {@link EngineOptions}. Never opens anything.
 *
 * @packageDocumentation
 */
import type { EngineOptions } from '../types';

/** The part of `navigator.mediaDevices` the engine uses. */
export type MediaDevicesLike = Pick<MediaDevices, 'getUserMedia' | 'enumerateDevices' | 'addEventListener' | 'removeEventListener'>;

/** The part of `navigator.permissions` the engine uses. */
export interface PermissionsLike {
  /** `navigator.permissions.query`. */
  query(descriptor: { readonly name: 'microphone' }): Promise<{ readonly state: string }>;
}

/** Creates an `AudioWorkletNode`. */
export type WorkletNodeCreator = (context: AudioContext, name: string, options: AudioWorkletNodeOptions) => AudioWorkletNode;

/** Browser facilities of the recording. */
export interface MediaEnvironment {
  /** `isSecureContext` of the page. */
  readonly secure: boolean;
  /** `navigator.mediaDevices` (or the test override); undefined outside a secure context. */
  readonly mediaDevices: MediaDevicesLike | undefined;
  /** `navigator.permissions`, when present. */
  readonly permissions: PermissionsLike | undefined;
  /** `new AudioWorkletNode(...)`. */
  readonly createWorkletNode: WorkletNodeCreator;
}

interface PageGlobals {
  readonly isSecureContext?: boolean;
  readonly navigator?: { readonly mediaDevices?: MediaDevicesLike; readonly permissions?: PermissionsLike };
}

/**
 * @param options - Engine options (`mediaDevices` test override).
 * @returns The page's facilities.
 */
export function browserEnvironment(options: EngineOptions): MediaEnvironment {
  const page = globalThis as PageGlobals;
  return {
    secure: page.isSecureContext === true,
    mediaDevices: options.mediaDevices ?? page.navigator?.mediaDevices,
    permissions: page.navigator?.permissions,
    createWorkletNode: (context, name, nodeOptions) => new AudioWorkletNode(context, name, nodeOptions),
  };
}
