/**
 * The recording surface type of contract 2.5.0 and the error of the methods whose user story is not delivered yet
 * (`calibrateLatency` US2, `setTakeCompare` US4, `setTakeAdjustment` US7).
 *
 * @packageDocumentation
 */
import type { AudioEngine } from '../types';

/** The recording and take-channel methods of {@link AudioEngine} (contract 2.5.0). */
export type RecordingSurface = Pick<
  AudioEngine,
  | 'recordingSupport'
  | 'listInputDevices'
  | 'openInput'
  | 'closeInput'
  | 'setMonitoring'
  | 'calibrateLatency'
  | 'startRecording'
  | 'stopRecording'
  | 'captureWav'
  | 'releaseCapture'
  | 'loadTake'
  | 'unloadTake'
  | 'setTakeAdjustment'
  | 'setTakeCompare'
>;

/**
 * @param method - Name of the contract method.
 * @returns The skeleton error of that method.
 */
export function notImplemented(method: keyof RecordingSurface): Error {
  return new Error(`not implemented: ${method}`);
}
