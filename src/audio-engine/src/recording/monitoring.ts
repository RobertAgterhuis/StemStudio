/**
 * Input monitoring (contract 2.5.0, FR-006): input -> Gain(volume) -> destination, bypassing the master and the take
 * channel, so monitored sound is never captured or exported. Off by default; forced off during calibration.
 *
 * @packageDocumentation
 */
import { rampTo } from '../channel';
import type { MonitoringSettings } from '../types';

/** Default monitoring settings (research R-12: off, volume 0.7). */
export const DEFAULT_MONITORING: MonitoringSettings = { enabled: false, volume: 0.7 };

/**
 * @param settings - Requested settings.
 * @returns A plain copy.
 * @throws Error `Invalid monitoring volume: <v> (0..1)` outside 0..1 or for a non-finite volume.
 */
export function validateMonitoring(settings: MonitoringSettings): MonitoringSettings {
  if (!(settings.volume >= 0 && settings.volume <= 1)) {
    throw new Error(`Invalid monitoring volume: ${String(settings.volume)} (0..1)`);
  }
  return { enabled: settings.enabled, volume: settings.volume };
}

/** The monitoring path of the open input. */
export class Monitor {
  private gain: GainNode | null = null;
  private current: MonitoringSettings = DEFAULT_MONITORING;
  private suppressed = false;

  /** @param context - The engine's AudioContext. */
  public constructor(private readonly context: BaseAudioContext) {}

  /** Current (validated) settings. */
  public get settings(): MonitoringSettings {
    return this.current;
  }

  /** @param input - Selected-channel node of the open input, or null when the input closed. */
  public attach(input: AudioNode | null): void {
    this.gain?.disconnect();
    this.gain = null;
    if (input === null) {
      return;
    }
    this.gain = this.context.createGain();
    this.gain.gain.value = this.target();
    input.connect(this.gain).connect(this.context.destination);
  }

  /** @param settings - Enabled and volume; ramps <= 10 ms. */
  public set(settings: MonitoringSettings): void {
    this.current = validateMonitoring(settings);
    this.apply();
  }

  /** @param on - True forces the monitor silent (calibration); false restores the settings. */
  public suppress(on: boolean): void {
    this.suppressed = on;
    this.apply();
  }

  private target(): number {
    return this.current.enabled && !this.suppressed ? this.current.volume : 0;
  }

  private apply(): void {
    if (this.gain !== null) {
      rampTo(this.gain.gain, this.target(), this.context.currentTime);
    }
  }
}
