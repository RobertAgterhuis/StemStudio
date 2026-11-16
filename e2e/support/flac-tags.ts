/**
 * Reader for the Vorbis comments of a FLAC file, used to check the AI labels written into every
 * take (009 FR-008, 010 FR-011: `AI_GENERATED=true`, `GENERATOR`, `AI_VOICE`, ...; SC-005).
 *
 * @packageDocumentation
 */

/** Vorbis comments by upper-case field name (a field may repeat). */
export type VorbisComments = Readonly<Record<string, readonly string[]>>;

const vorbisCommentBlock = 4;
const headerBytes = 4;

/** Start of the VORBIS_COMMENT block's body, or undefined when the file has none. */
function findCommentBlock(bytes: Buffer): number | undefined {
  let offset = 4;
  for (;;) {
    const header = bytes.readUInt8(offset);
    if ((header & 0x7f) === vorbisCommentBlock) {
      return offset + headerBytes;
    }
    offset += headerBytes + bytes.readUIntBE(offset + 1, 3);
    if (isLastBlock(header) || offset >= bytes.length) {
      return undefined;
    }
  }
}

function isLastBlock(header: number): boolean {
  return (header & 0x80) !== 0;
}

/**
 * Reads the Vorbis comments of a FLAC file (empty when the file has no comment block).
 * @throws Error when `bytes` is not a FLAC file.
 */
export function readVorbisComments(bytes: Buffer): VorbisComments {
  if (bytes.toString('ascii', 0, 4) !== 'fLaC') {
    throw new Error('not a FLAC file');
  }
  const block = findCommentBlock(bytes);
  const comments: Record<string, string[]> = {};
  for (const entry of block === undefined ? [] : commentEntries(bytes, block)) {
    const split = entry.indexOf('=');
    (comments[entry.slice(0, split).toUpperCase()] ??= []).push(entry.slice(split + 1));
  }
  return comments;
}

/** The `KEY=value` strings of a VORBIS_COMMENT block body starting at `block` (vendor skipped). */
function commentEntries(bytes: Buffer, block: number): string[] {
  let at = block + 4 + bytes.readUInt32LE(block);
  const count = bytes.readUInt32LE(at);
  at += 4;
  const entries: string[] = [];
  for (let i = 0; i < count; i++) {
    const size = bytes.readUInt32LE(at);
    entries.push(bytes.toString('utf-8', at + 4, at + 4 + size));
    at += 4 + size;
  }
  return entries;
}

/** Builds a minimal FLAC file with a STREAMINFO and a VORBIS_COMMENT block (self-tests only). */
export function buildFlacWithComments(fields: readonly string[], vendor = 'stemstudio-e2e'): Buffer {
  const parts = [u32le(Buffer.byteLength(vendor)), Buffer.from(vendor), u32le(fields.length)];
  for (const field of fields) {
    parts.push(u32le(Buffer.byteLength(field)), Buffer.from(field));
  }
  const comment = Buffer.concat(parts);
  const streamInfo = Buffer.concat([Buffer.from([0x00, 0x00, 0x00, 34]), Buffer.alloc(34)]);
  const commentHeader = Buffer.from([0x80 | vorbisCommentBlock, 0, 0, 0]);
  commentHeader.writeUIntBE(comment.length, 1, 3);
  return Buffer.concat([Buffer.from('fLaC'), streamInfo, commentHeader, comment]);
}

function u32le(value: number): Buffer {
  const buffer = Buffer.alloc(4);
  buffer.writeUInt32LE(value);
  return buffer;
}
