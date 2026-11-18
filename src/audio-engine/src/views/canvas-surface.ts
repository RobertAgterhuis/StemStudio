/**
 * Engine-owned `<canvas>` of a view (contract 2.6.0 hit lanes and pitch view): sized to the host times
 * `devicePixelRatio` when built and on resize, colours read from the host's CSS custom properties at the same time
 * (never per frame), and every drawing call counted for the frame budget (research R-12).
 *
 * @packageDocumentation
 */
import { HostSize } from '../timeline/host-size';

/** Counted subset of the 2D context the views draw with. */
export class Painter {
  private count = 0;

  /** @param ctx - The canvas context, or null without canvas support (nothing is drawn). */
  public constructor(private readonly ctx: CanvasRenderingContext2D | null) {}

  /** Drawing calls so far. */
  public get primitives(): number {
    return this.count;
  }

  /** Clears the whole surface. */
  public clear(width: number, height: number): void {
    this.call((ctx) => {
      ctx.clearRect(0, 0, width, height);
    });
  }

  /** Starts a filled path of rounded rectangles in `colour`. */
  public beginFill(colour: string): void {
    this.call((ctx) => {
      ctx.fillStyle = colour;
      ctx.beginPath();
    });
  }

  /** Adds a rounded rectangle to the current path. */
  public roundRect(x: number, y: number, width: number, height: number): void {
    this.call((ctx) => {
      ctx.roundRect(x, y, width, height, Math.min(3, height / 2));
    });
  }

  /** Fills the current path. */
  public fill(): void {
    this.call((ctx) => {
      ctx.fill();
    });
  }

  /** Starts a stroked path in `colour`. */
  public beginStroke(colour: string, lineWidth: number): void {
    this.call((ctx) => {
      ctx.strokeStyle = colour;
      ctx.lineWidth = lineWidth;
      ctx.beginPath();
    });
  }

  /** Moves the pen. */
  public moveTo(x: number, y: number): void {
    this.call((ctx) => {
      ctx.moveTo(x, y);
    });
  }

  /** Adds a line segment. */
  public lineTo(x: number, y: number): void {
    this.call((ctx) => {
      ctx.lineTo(x, y);
    });
  }

  /** Strokes the current path. */
  public stroke(): void {
    this.call((ctx) => {
      ctx.stroke();
    });
  }

  private call(draw: (ctx: CanvasRenderingContext2D) => void): void {
    if (this.ctx !== null) {
      draw(this.ctx);
      this.count += 1;
    }
  }
}

/** Colour fallbacks keyed by CSS custom property name. */
export type Palette = Readonly<Record<string, string>>;

/** The canvas of one attached view. */
export class CanvasSurface {
  /** Counted drawing calls. */
  public readonly painter: Painter;
  private readonly canvas: HTMLCanvasElement;
  private readonly ctx: CanvasRenderingContext2D | null;
  private readonly size: HostSize;
  private colours: Palette;

  /**
   * @param host - Host element (its children are replaced by the canvas).
   * @param palette - CSS custom properties with their fallback colours.
   * @param onResize - Called after a resize was applied.
   */
  public constructor(
    private readonly host: HTMLElement,
    private readonly palette: Palette,
    private readonly onResize: () => void,
  ) {
    this.canvas = host.ownerDocument.createElement('canvas');
    this.canvas.setAttribute('aria-hidden', 'true');
    host.replaceChildren(this.canvas);
    this.ctx = this.canvas.getContext('2d');
    this.painter = new Painter(this.ctx);
    this.colours = palette;
    this.size = new HostSize(host, this.resized);
    this.apply();
  }

  /** CSS width (px). */
  public get width(): number {
    return this.size.width;
  }

  /** CSS height (px). */
  public get height(): number {
    return this.size.height;
  }

  /** @returns The colour of a palette entry as last read from the host. */
  public colour(name: string): string {
    return this.colours[name] ?? '';
  }

  /** Removes the canvas and stops observing. */
  public dispose(): void {
    this.size.dispose();
    this.host.replaceChildren();
  }

  private readonly resized = (): void => {
    this.apply();
    this.onResize();
  };

  private apply(): void {
    const ratio = (globalThis as { devicePixelRatio?: number }).devicePixelRatio ?? 1;
    this.canvas.width = Math.round(this.size.width * ratio);
    this.canvas.height = Math.round(this.size.height * ratio);
    this.ctx?.setTransform(ratio, 0, 0, ratio, 0, 0);
    this.colours = readPalette(this.host, this.palette);
  }
}

function nonEmpty(value: string | undefined): string | undefined {
  return value === '' ? undefined : value;
}

function readPalette(host: HTMLElement, palette: Palette): Palette {
  const style = host.ownerDocument.defaultView?.getComputedStyle(host);
  const entries = Object.entries(palette).map(([name, fallback]) => [name, nonEmpty(style?.getPropertyValue(name).trim()) ?? fallback]);
  return Object.fromEntries(entries) as Palette;
}
