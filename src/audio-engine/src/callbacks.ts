/**
 * Bridges transport events to the Blazor `[JSInvokable]` callbacks.
 *
 * @packageDocumentation
 */
import type { TransportListener } from './transport';
import type { CapturedTake, DotNetCallbacks, DotNetObjectReference, LoopPassInfo, PlaybackState, PracticeStatus, RecordingProgress, RecordingState } from './types';

/** Interval of position reports while playing (10 Hz). UI feedback only, never audible timing. */
export const POSITION_REPORT_INTERVAL_MS = 100;

/**
 * Sends `OnPlaybackStateChanged` on every state change, `OnPositionChanged` after every
 * reposition and at most every {@link POSITION_REPORT_INTERVAL_MS} ms while playing.
 */
export class DotNetNotifier implements TransportListener {
  private timer: ReturnType<typeof setInterval> | undefined;

  /**
   * @param ref - .NET object implementing the callbacks.
   * @param position - Reads the current transport position.
   */
  public constructor(
    private readonly ref: DotNetObjectReference,
    private readonly position: () => number,
  ) {}

  /** {@inheritDoc TransportListener.onStateChanged} */
  public onStateChanged(state: PlaybackState): void {
    this.invoke('OnPlaybackStateChanged', state);
    if (state === 'playing') {
      this.startReports();
    } else {
      this.dispose();
    }
  }

  /** {@inheritDoc TransportListener.onReposition} */
  public onReposition(position: number): void {
    this.invoke('OnPositionChanged', position);
  }

  /** Stops the periodic position reports. */
  public dispose(): void {
    clearInterval(this.timer);
    this.timer = undefined;
  }

  private startReports(): void {
    this.dispose();
    this.timer = setInterval(() => {
      this.invoke('OnPositionChanged', this.position());
    }, POSITION_REPORT_INTERVAL_MS);
  }

  private invoke(method: DotNetCallbacks, value: unknown): void {
    // A disconnected circuit rejects; the engine must keep working regardless.
    this.ref.invokeMethodAsync(method, value).catch(() => undefined);
  }
}

/** Shortest interval between two `OnPracticeChanged` calls (10 Hz). UI feedback only, never audible timing. */
export const PRACTICE_REPORT_INTERVAL_MS = 100;

/**
 * Sends `OnPracticeChanged` (contract 2.3.0) at most every {@link PRACTICE_REPORT_INTERVAL_MS} ms: the first
 * report of a burst immediately, the rest coalesced into one trailing report of the status at that time.
 */
export class PracticeNotifier {
  private last = -Infinity;
  private timer: ReturnType<typeof setTimeout> | undefined;

  /**
   * @param ref - .NET object implementing the callbacks.
   * @param status - Reads the current practice status when a report is sent.
   * @param now - Wall clock in ms; injectable for tests.
   */
  public constructor(
    private readonly ref: DotNetObjectReference,
    private readonly status: () => PracticeStatus,
    private readonly now: () => number = () => Date.now(),
  ) {}

  /** Requests a report. */
  public changed(): void {
    if (this.timer !== undefined) {
      return;
    }
    const wait = this.last + PRACTICE_REPORT_INTERVAL_MS - this.now();
    if (wait <= 0) {
      this.send();
    } else {
      this.timer = setTimeout(() => {
        this.timer = undefined;
        this.send();
      }, wait);
    }
  }

  /** Cancels a pending trailing report. */
  public dispose(): void {
    clearTimeout(this.timer);
    this.timer = undefined;
  }

  private send(): void {
    this.last = this.now();
    // A disconnected circuit rejects; the engine must keep working regardless.
    this.ref.invokeMethodAsync('OnPracticeChanged', this.status()).catch(() => undefined);
  }
}

/**
 * Sends `OnLoopPass` (contract 2.4.0): at most one call per completed loop pass, so no rate limit is needed.
 *
 * @param ref - .NET object implementing the callbacks.
 * @returns The report function handed to the loop-pass tracker.
 */
export function loopPassNotifier(ref: DotNetObjectReference): (info: LoopPassInfo) => void {
  return (info) => {
    // A disconnected circuit rejects; the engine must keep working regardless.
    ref.invokeMethodAsync('OnLoopPass', info).catch(() => undefined);
  };
}

/** The recording callbacks to .NET (contract 2.5.0). */
export interface RecordingNotifier {
  /** `OnRecordingStateChanged`. */
  state(state: RecordingState): void;
  /** `OnRecordingProgress` (the recorder sends at most 4 Hz). */
  progress(progress: RecordingProgress): void;
  /** `OnTakesCaptured`. */
  takes(takes: CapturedTake[]): void;
  /** `OnInputLost`. */
  inputLost(reason: string): void;
  /** `OnInputDevicesChanged`. */
  devicesChanged(): void;
}

/**
 * @param ref - .NET object implementing the callbacks.
 * @returns The recording notifier.
 */
export function recordingNotifier(ref: DotNetObjectReference): RecordingNotifier {
  const send = (method: DotNetCallbacks, ...args: unknown[]): void => {
    // A disconnected circuit rejects; the engine must keep working regardless.
    ref.invokeMethodAsync(method, ...args).catch(() => undefined);
  };
  return {
    state: (state) => {
      send('OnRecordingStateChanged', state);
    },
    progress: (progress) => {
      send('OnRecordingProgress', progress);
    },
    takes: (takes) => {
      send('OnTakesCaptured', takes);
    },
    inputLost: (reason) => {
      send('OnInputLost', reason);
    },
    devicesChanged: () => {
      send('OnInputDevicesChanged');
    },
  };
}
