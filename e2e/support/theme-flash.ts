/**
 * First-frame theme check (SC-004, quickstart V-D1): the colour of one pixel captured at
 * `domcontentloaded` compared with the page background of both themes.
 *
 * @packageDocumentation
 */
import { inflateSync } from 'node:zlib';
import type { Page } from '@playwright/test';
import type { ResolvedTheme } from '../pages/app-shell';

/** An sRGB colour, 0-255 per channel. */
export type Rgb = readonly [number, number, number];

/** Page background (`body`) per theme. */
export type ThemeBackgrounds = Readonly<Record<ResolvedTheme, Rgb>>;

/**
 * Reads the `body` background of both themes by setting `html[data-theme]` in turn; any CSS
 * colour syntax (hex, `rgb()`, `oklch()`) is resolved to sRGB through a 1 x 1 canvas.
 */
export async function themeBackgrounds(page: Page): Promise<ThemeBackgrounds> {
  return page.evaluate(() => {
    const root = document.documentElement;
    const original = root.dataset.theme;
    const read = (theme: string): [number, number, number] => {
      root.dataset.theme = theme;
      const context = document.createElement('canvas').getContext('2d', { willReadFrequently: true });
      if (context === null) throw new Error('no 2d canvas');
      context.fillStyle = getComputedStyle(document.body).backgroundColor;
      context.fillRect(0, 0, 1, 1);
      const [r = 0, g = 0, b = 0] = context.getImageData(0, 0, 1, 1).data;
      return [r, g, b];
    };
    const result = { light: read('light'), dark: read('dark') };
    if (original === undefined) delete root.dataset.theme;
    else root.dataset.theme = original;
    return result;
  });
}

/** Reloads, and at `domcontentloaded` returns `html[data-theme]` and the bottom-right pixel. */
export async function firstFrame(page: Page): Promise<{ attribute: string | null; pixel: Rgb }> {
  await page.reload({ waitUntil: 'domcontentloaded' });
  const attribute = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
  const size = page.viewportSize() ?? { width: 1280, height: 720 };
  const png = await page.screenshot({ clip: { x: size.width - 2, y: size.height - 2, width: 1, height: 1 }, scale: 'css' });
  return { attribute, pixel: decodeSinglePixelPng(png) };
}

/** The theme whose background is nearer to `pixel` (Euclidean distance in sRGB). */
export function nearerTheme(pixel: Rgb, backgrounds: ThemeBackgrounds): ResolvedTheme {
  return distance(pixel, backgrounds.dark) < distance(pixel, backgrounds.light) ? 'dark' : 'light';
}

/** Euclidean distance between two colours. */
export function distance(a: Rgb, b: Rgb): number {
  return Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]);
}

/**
 * Colour of a 1 x 1 8-bit RGB or RGBA PNG. With a single pixel every PNG row filter predicts 0,
 * so the inflated bytes after the filter byte are the raw channel values.
 * @throws Error for any other PNG layout.
 */
export function decodeSinglePixelPng(png: Buffer): Rgb {
  const header = chunk(png, 'IHDR');
  const layout = [header.readUInt32BE(0), header.readUInt32BE(4), header.readUInt8(8), header.readUInt8(9)].join(',');
  if (layout !== '1,1,8,2' && layout !== '1,1,8,6') {
    throw new Error(`expected a 1 x 1 8-bit RGB(A) PNG, got width,height,depth,type = ${layout}`);
  }
  const raw = inflateSync(chunk(png, 'IDAT'));
  return [raw.readUInt8(1), raw.readUInt8(2), raw.readUInt8(3)];
}

/** Concatenated data of all chunks of `type`. */
function chunk(png: Buffer, type: string): Buffer {
  const parts: Buffer[] = [];
  for (let offset = 8; offset + 8 <= png.length; ) {
    const length = png.readUInt32BE(offset);
    if (png.toString('ascii', offset + 4, offset + 8) === type) parts.push(png.subarray(offset + 8, offset + 8 + length));
    offset += 12 + length;
  }
  return Buffer.concat(parts);
}
