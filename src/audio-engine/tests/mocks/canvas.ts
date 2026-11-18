import { vi } from 'vitest';

/** One recorded drawing call. */
export interface Primitive {
  readonly op: string;
  readonly args: readonly number[];
  /** `fillStyle` / `strokeStyle` / `globalAlpha` at the call. */
  readonly fill: string;
  readonly stroke: string;
  readonly alpha: number;
}

/** Recording fake of the parts of `CanvasRenderingContext2D` the views use. */
export class FakeContext2D {
  public static readonly instances: FakeContext2D[] = [];
  public readonly calls: Primitive[] = [];
  public fillStyle = '#000';
  public strokeStyle = '#000';
  public globalAlpha = 1;
  public lineWidth = 1;

  public constructor(public readonly canvas: HTMLCanvasElement) {
    FakeContext2D.instances.push(this);
  }

  /** The context of the most recently created canvas. */
  public static latest(): FakeContext2D {
    const latest = FakeContext2D.instances.at(-1);
    if (latest === undefined) {
      throw new Error('no 2D context was created');
    }
    return latest;
  }

  public clearRect(...args: number[]): void {
    this.record('clearRect', args);
  }

  public fillRect(...args: number[]): void {
    this.record('fillRect', args);
  }

  public roundRect(...args: number[]): void {
    this.record('roundRect', args);
  }

  public beginPath(): void {
    this.record('beginPath', []);
  }

  public moveTo(...args: number[]): void {
    this.record('moveTo', args);
  }

  public lineTo(...args: number[]): void {
    this.record('lineTo', args);
  }

  public fill(): void {
    this.record('fill', []);
  }

  public stroke(): void {
    this.record('stroke', []);
  }

  public setTransform(...args: number[]): void {
    this.record('setTransform', args);
  }

  /** Calls since the last {@link FakeContext2D.reset}, optionally of one operation. */
  public ops(op?: string): Primitive[] {
    return op === undefined ? this.calls : this.calls.filter((c) => c.op === op);
  }

  /** Forgets the recorded calls. */
  public reset(): void {
    this.calls.length = 0;
  }

  private record(op: string, args: number[]): void {
    this.calls.push({ op, args, fill: this.fillStyle, stroke: this.strokeStyle, alpha: this.globalAlpha });
  }
}

/** Makes every `canvas.getContext('2d')` return a new {@link FakeContext2D}. */
export function stubCanvas(): void {
  FakeContext2D.instances.length = 0;
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockImplementation(function (this: HTMLCanvasElement) {
    return new FakeContext2D(this) as unknown as CanvasRenderingContext2D;
  } as typeof HTMLCanvasElement.prototype.getContext);
}
