/**
 * Phase 0 skeleton of the recording surface (contract 2.5.0): every method fails with
 * `Error("not implemented: <method>")` until the audio-engine lane replaces this module with the real input, capture,
 * calibration, recorder and take-channel modules (tasks T012-T035).
 *
 * @packageDocumentation
 */
import type {
  AudioEngine,
  CalibrationResult,
  CapturedTake,
  InputDevice,
  InputInfo,
  RecordingSupport,
} from '../types';

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

/** Skeleton implementation: async methods reject, synchronous methods throw. */
export class PendingRecording implements RecordingSurface {
  /** {@inheritDoc AudioEngine.recordingSupport} */
  public recordingSupport(): Promise<RecordingSupport> {
    return Promise.reject(notImplemented('recordingSupport'));
  }

  /** {@inheritDoc AudioEngine.listInputDevices} */
  public listInputDevices(): Promise<InputDevice[]> {
    return Promise.reject(notImplemented('listInputDevices'));
  }

  /** {@inheritDoc AudioEngine.openInput} */
  public openInput(): Promise<InputInfo> {
    return Promise.reject(notImplemented('openInput'));
  }

  /** {@inheritDoc AudioEngine.closeInput} */
  public closeInput(): void {
    throw notImplemented('closeInput');
  }

  /** {@inheritDoc AudioEngine.setMonitoring} */
  public setMonitoring(): void {
    throw notImplemented('setMonitoring');
  }

  /** {@inheritDoc AudioEngine.calibrateLatency} */
  public calibrateLatency(): Promise<CalibrationResult> {
    return Promise.reject(notImplemented('calibrateLatency'));
  }

  /** {@inheritDoc AudioEngine.startRecording} */
  public startRecording(): Promise<void> {
    return Promise.reject(notImplemented('startRecording'));
  }

  /** {@inheritDoc AudioEngine.stopRecording} */
  public stopRecording(): Promise<CapturedTake[]> {
    return Promise.reject(notImplemented('stopRecording'));
  }

  /** {@inheritDoc AudioEngine.captureWav} */
  public captureWav(): Blob {
    throw notImplemented('captureWav');
  }

  /** {@inheritDoc AudioEngine.releaseCapture} */
  public releaseCapture(): void {
    throw notImplemented('releaseCapture');
  }

  /** {@inheritDoc AudioEngine.loadTake} */
  public loadTake(): Promise<void> {
    return Promise.reject(notImplemented('loadTake'));
  }

  /** {@inheritDoc AudioEngine.unloadTake} */
  public unloadTake(): void {
    throw notImplemented('unloadTake');
  }

  /** {@inheritDoc AudioEngine.setTakeAdjustment} */
  public setTakeAdjustment(): void {
    throw notImplemented('setTakeAdjustment');
  }

  /** {@inheritDoc AudioEngine.setTakeCompare} */
  public setTakeCompare(): void {
    throw notImplemented('setTakeCompare');
  }
}
