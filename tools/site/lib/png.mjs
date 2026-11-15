/**
 * Minimal PNG reader/writer (8-bit RGB or RGBA, not interlaced) used to paint over the magenta
 * masks that the visual-regression suite draws over volatile regions (meters, dates). Masked
 * pixels take the colour of the nearest unmasked pixel to their left, so a mask on a panel
 * becomes that panel's background. No dependencies (node:zlib only).
 * @module
 */
import { deflateSync, inflateSync } from "node:zlib";

const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
const CHANNELS = { 2: 3, 6: 4 };

/** @typedef {{ width: number, height: number, colorType: number, channels: number, pixels: Buffer }} Image */

/** CRC-32 table of the PNG specification. */
const CRC_TABLE = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let k = 0; k < 8; k += 1) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});

/** @param {Buffer} data bytes @returns {number} CRC-32 */
function crc32(data) {
  let c = 0xffffffff;
  for (const byte of data) c = CRC_TABLE[(c ^ byte) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

/**
 * Splits a PNG file into its chunks.
 * @param {Buffer} file PNG bytes
 * @returns {{ type: string, data: Buffer }[]} chunks in file order
 */
function chunks(file) {
  if (!file.subarray(0, 8).equals(SIGNATURE)) throw new Error("not a PNG file");
  const list = [];
  for (let at = 8; at < file.length; ) {
    const length = file.readUInt32BE(at);
    list.push({ type: file.toString("latin1", at + 4, at + 8), data: file.subarray(at + 8, at + 8 + length) });
    at += 12 + length;
  }
  return list;
}

/** @param {number} a left @param {number} b up @param {number} c up-left @returns {number} Paeth predictor */
function paeth(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  return pb <= pc ? b : c;
}

/**
 * Reverses the scanline filter of one row in place.
 * @param {number} filter filter type 0-4
 * @param {Buffer} row the row's bytes (filtered in, raw out)
 * @param {Buffer} prev the previous raw row (zeros for the first row)
 * @param {number} bpp bytes per pixel
 */
function unfilterRow(filter, row, prev, bpp) {
  for (let i = 0; i < row.length; i += 1) {
    const a = i >= bpp ? row[i - bpp] : 0;
    const c = i >= bpp ? prev[i - bpp] : 0;
    const predictors = [0, a, prev[i], (a + prev[i]) >> 1, paeth(a, prev[i], c)];
    row[i] = (row[i] + predictors[filter]) & 0xff;
  }
}

/**
 * Decodes a PNG file.
 * @param {Buffer} file PNG bytes
 * @returns {Image} decoded image
 */
export function decodePng(file) {
  const list = chunks(file);
  const header = list[0].data;
  const [width, height, depth, colorType, interlace] = [header.readUInt32BE(0), header.readUInt32BE(4), header[8], header[9], header[12]];
  const channels = CHANNELS[colorType];
  if (depth !== 8 || !channels || interlace !== 0) throw new Error("only 8-bit RGB/RGBA non-interlaced PNGs are supported");
  const raw = inflateSync(Buffer.concat(list.filter((c) => c.type === "IDAT").map((c) => c.data)));
  const stride = width * channels;
  const pixels = Buffer.alloc(stride * height);
  let prev = Buffer.alloc(stride);
  for (let y = 0; y < height; y += 1) {
    const row = Buffer.from(raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1)));
    unfilterRow(raw[y * (stride + 1)], row, prev, channels);
    row.copy(pixels, y * stride);
    prev = row;
  }
  return { width, height, colorType, channels, pixels };
}

/** @param {string} type chunk type @param {Buffer} data chunk data @returns {Buffer} the encoded chunk */
function chunk(type, data) {
  const typed = Buffer.concat([Buffer.from(type, "latin1"), data]);
  const out = Buffer.alloc(12 + data.length);
  out.writeUInt32BE(data.length, 0);
  typed.copy(out, 4);
  out.writeUInt32BE(crc32(typed), 8 + data.length);
  return out;
}

/**
 * Encodes an image as PNG; every row uses the "Sub" filter (good for flat UI screenshots).
 * @param {Image} image the image
 * @returns {Buffer} PNG bytes
 */
export function encodePng(image) {
  const { width, height, colorType, channels, pixels } = image;
  const stride = width * channels;
  const raw = Buffer.alloc((stride + 1) * height);
  for (let y = 0; y < height; y += 1) {
    const base = y * (stride + 1);
    raw[base] = 1;
    for (let i = 0; i < stride; i += 1) {
      const left = i >= channels ? pixels[y * stride + i - channels] : 0;
      raw[base + 1 + i] = (pixels[y * stride + i] - left) & 0xff;
    }
  }
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0);
  header.writeUInt32BE(height, 4);
  header.set([8, colorType, 0, 0, 0], 8);
  return Buffer.concat([SIGNATURE, chunk("IHDR", header), chunk("IDAT", deflateSync(raw, { level: 9 })), chunk("IEND", Buffer.alloc(0))]);
}

/** @param {Buffer} p pixels @param {number} at byte offset @returns {boolean} pure magenta mask pixel */
const isMask = (p, at) => p[at] === 255 && p[at + 1] === 0 && p[at + 2] === 255;

/**
 * Paints every magenta mask pixel with the nearest unmasked colour to its left in the same row.
 * @param {Image} image decoded image (changed in place)
 * @returns {number} number of repainted pixels
 */
export function paintOverMasks(image) {
  const { width, height, channels, pixels } = image;
  let painted = 0;
  for (let y = 0; y < height; y += 1) {
    let fill = -1;
    for (let x = 0; x < width; x += 1) {
      const at = (y * width + x) * channels;
      if (!isMask(pixels, at)) fill = at;
      else if (fill >= 0) {
        pixels.copyWithin(at, fill, fill + channels);
        painted += 1;
      }
    }
  }
  return painted;
}

/**
 * Removes the visual-test masks from a PNG file; files without masks are returned unchanged.
 * @param {Buffer} file PNG bytes
 * @returns {Buffer} cleaned PNG bytes
 */
export function cleanScreenshot(file) {
  const image = decodePng(file);
  return paintOverMasks(image) > 0 ? encodePng(image) : file;
}
