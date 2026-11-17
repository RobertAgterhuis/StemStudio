/**
 * `recordingSupport()` (contract 2.5.0, ADR-024 D7/D8): checks, in this order, the secure context, getUserMedia and
 * AudioWorklet, then a rate mismatch seen earlier; reports the microphone permission. No side effects.
 *
 * @packageDocumentation
 */
import type { PermissionStateValue, RecordingSupport, RecordingUnsupportedReason } from '../types';
import type { MediaEnvironment } from './environment';

/** Readable reason (with the next step) per reason code. */
export const UNSUPPORTED_REASONS: Readonly<Record<RecordingUnsupportedReason, string>> = {
  insecureContext: 'Open StemStudio on the computer it runs on to record (recording needs http://localhost or https).',
  noMediaDevices: 'This browser gives no access to audio inputs. Use a current Chrome or Edge.',
  noAudioWorklet: 'This browser cannot process audio input. Use a current Chrome or Edge.',
  rateMismatch: 'This browser cannot record from an input whose sample rate differs from 44.1 kHz. Use Chrome or Edge, or set the interface to 44.1 kHz.',
};

const PERMISSION_STATES: readonly PermissionStateValue[] = ['granted', 'prompt', 'denied'];

function reasonOf(env: MediaEnvironment, context: AudioContext, rateMismatch: boolean): RecordingUnsupportedReason | null {
  const worklet = (context as Partial<Pick<AudioContext, 'audioWorklet'>>).audioWorklet;
  const checks: readonly [boolean, RecordingUnsupportedReason][] = [
    [!env.secure, 'insecureContext'],
    [typeof env.mediaDevices?.getUserMedia !== 'function', 'noMediaDevices'],
    [worklet === undefined, 'noAudioWorklet'],
    [rateMismatch, 'rateMismatch'],
  ];
  return checks.find(([failed]) => failed)?.[1] ?? null;
}

async function permissionOf(env: MediaEnvironment): Promise<PermissionStateValue> {
  try {
    const status = await env.permissions?.query({ name: 'microphone' });
    return PERMISSION_STATES.find((state) => state === status?.state) ?? 'unknown';
  } catch {
    return 'unknown';
  }
}

/**
 * @param env - Browser facilities.
 * @param context - The engine's AudioContext.
 * @param rateMismatch - Whether opening an input failed with a rate mismatch earlier.
 * @returns Support with reason code, readable reason and permission.
 */
export async function checkSupport(env: MediaEnvironment, context: AudioContext, rateMismatch: boolean): Promise<RecordingSupport> {
  const reasonCode = reasonOf(env, context, rateMismatch);
  const permission = await permissionOf(env);
  return { supported: reasonCode === null, reasonCode, reason: reasonCode === null ? null : UNSUPPORTED_REASONS[reasonCode], permission };
}
