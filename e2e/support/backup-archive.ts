/**
 * Reads a StemStudio backup archive (backup-archive 1.0.0) without dependencies: the ZIP central
 * directory (Zip64 aware, raw names), bounded inflation with `node:zlib`, the manifest, and a
 * validator for the subset of JSON Schema 2020-12 that `backup-manifest.schema.json` uses
 * (type, const, enum, required, properties, additionalProperties, items, min/max items, length
 * and value limits, uniqueItems, pattern, not, local `$ref`). Used by the 013 specs to check
 * archives the stack wrote, and by the stack-free self-test against the committed fixtures.
 *
 * @packageDocumentation
 */
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { inflateRawSync } from 'node:zlib';

/** One central-directory record as stored. */
export interface ZipRecord {
  /** Raw name bytes decoded as UTF-8 (no normalisation). */
  readonly name: string;
  readonly method: number;
  readonly compressedSize: number;
  /** Declared uncompressed size. */
  readonly size: number;
  /** High 16 bits of the external attributes (Unix mode). */
  readonly unixMode: number;
  readonly offset: number;
}

/** A manifest `entries[]` item. */
export interface ManifestEntry {
  readonly path: string;
  readonly category: string;
  readonly sizeBytes: number;
  readonly sha256: string;
  readonly entity?: { readonly type: string; readonly id: string };
}

/** The members of `manifest.json` the specs read (the schema checks the rest). */
export interface BackupManifest {
  readonly format: string;
  readonly formatVersion: string;
  readonly backupId: string;
  readonly createdAt: string;
  readonly app: { readonly name: string; readonly version: string; readonly backendApi: string };
  readonly schema: { readonly provider: string; readonly lastMigration: string; readonly migrations: readonly string[] };
  readonly contents: { readonly included: readonly string[]; readonly excluded: readonly { readonly category: string; readonly reason: string }[] };
  readonly counts: Readonly<Record<string, number>>;
  readonly totals: { readonly entries: number; readonly bytes: number };
  readonly entries: readonly ManifestEntry[];
  readonly voiceProfiles: readonly {
    readonly id: string;
    readonly displayName: string;
    readonly consent: { readonly statementVersion: string; readonly statementSha256: string; readonly personName: string; readonly consentedAt: string };
    readonly samplePath: string;
    readonly sampleSha256: string;
  }[];
  readonly warnings: readonly { readonly code: string; readonly message: string }[];
}

/** Repository root (the suite lives in `<root>/e2e`). */
export const repoRoot: string = path.resolve(__dirname, '..', '..');

/** The binding manifest schema of feature 013. */
export const manifestSchemaPath: string = path.join(repoRoot, 'specs', '013-library-care', 'contracts', 'backup-manifest.schema.json');

/** Folder of the committed fixture archives (qa T012). */
export const fixtureArchiveDir: string = path.join(repoRoot, 'tests', 'fixtures', 'backup', 'archives');

const MAX32 = 0xffffffff;
const S_IFMT = 0o170000;
const S_IFLNK = 0o120000;

function u16(data: Buffer, at: number): number {
  return data.readUInt16LE(at);
}

function u32(data: Buffer, at: number): number {
  return data.readUInt32LE(at);
}

function u64(data: Buffer, at: number): number {
  return Number(data.readBigUInt64LE(at));
}

/** Entry count and directory offset from the (Zip64) end records. */
function endOfDirectory(data: Buffer): { count: number; offset: number } {
  const at = data.lastIndexOf(Buffer.from('PK\x05\x06', 'latin1'));
  if (at < 0) throw new Error('not a ZIP: no end of central directory record');
  const locator = at - 20;
  if (locator >= 0 && data.toString('latin1', locator, locator + 4) === 'PK\x06\x07') {
    const record = u64(data, locator + 8);
    return { count: u64(data, record + 32), offset: u64(data, record + 48) };
  }
  return { count: u16(data, at + 10), offset: u32(data, at + 16) };
}

function zip64Values(extra: Buffer): number[] {
  for (let at = 0; at + 4 <= extra.length; at += 4 + u16(extra, at + 2)) {
    if (u16(extra, at) === 0x0001) {
      const values: number[] = [];
      for (let v = at + 4; v + 8 <= at + 4 + u16(extra, at + 2); v += 8) values.push(u64(extra, v));
      return values;
    }
  }
  return [];
}

/** Replaces a 32-bit size, compressed size or offset of 0xFFFFFFFF by the next Zip64 value, in that order. */
function widen(narrow: readonly [number, number, number], extra: Buffer): [number, number, number] {
  const wide = zip64Values(extra);
  const pick = (value: number): number => (value === MAX32 ? (wide.shift() ?? value) : value);
  return [pick(narrow[0]), pick(narrow[1]), pick(narrow[2])];
}

function readRecord(data: Buffer, at: number): { record: ZipRecord; next: number } {
  const nameLength = u16(data, at + 28);
  const extraLength = u16(data, at + 30);
  const commentLength = u16(data, at + 32);
  const name = data.toString('utf8', at + 46, at + 46 + nameLength);
  const extra = data.subarray(at + 46 + nameLength, at + 46 + nameLength + extraLength);
  const [size, compressedSize, offset] = widen([u32(data, at + 24), u32(data, at + 20), u32(data, at + 42)], extra);
  const record: ZipRecord = { name, method: u16(data, at + 10), compressedSize, size, unixMode: u32(data, at + 38) >>> 16, offset };
  return { record, next: at + 46 + nameLength + extraLength + commentLength };
}

/**
 * Every central-directory record in order, duplicates included.
 * @throws Error when there is no end record or the record count disagrees with it.
 */
export function readCentralDirectory(data: Buffer): ZipRecord[] {
  const end = endOfDirectory(data);
  const records: ZipRecord[] = [];
  let at = end.offset;
  while (at + 4 <= data.length && u32(data, at) === 0x02014b50) {
    const { record, next } = readRecord(data, at);
    records.push(record);
    at = next;
  }
  if (records.length !== end.count) throw new Error(`end record declares ${String(end.count)} entries, directory has ${String(records.length)}`);
  return records;
}

/** True when the record is a Unix symbolic link. */
export function isLink(record: ZipRecord): boolean {
  return (record.unixMode & S_IFMT) === S_IFLNK;
}

/** Decompressed content of a record, at most `limit` bytes (a zip bomb stops at the limit). */
export function readEntry(data: Buffer, record: ZipRecord, limit = record.size): Buffer {
  const start = record.offset + 30 + u16(data, record.offset + 26) + u16(data, record.offset + 28);
  const payload = data.subarray(start, start + record.compressedSize);
  if (record.method === 0) return Buffer.from(payload.subarray(0, limit));
  try {
    return inflateRawSync(payload, { maxOutputLength: Math.max(1, limit) });
  } catch (error) {
    if (error instanceof RangeError) return inflatePrefix(payload, limit);
    throw error;
  }
}

/** The first `limit` bytes of a deflate stream that inflates to more (bounded by chunks). */
function inflatePrefix(payload: Buffer, limit: number): Buffer {
  const whole = inflateRawSync(payload, { maxOutputLength: limit + 64 * 1024 * 1024 });
  return whole.subarray(0, limit);
}

/** Lower-case hex SHA-256. */
export function sha256(data: Buffer): string {
  return createHash('sha256').update(data).digest('hex');
}

/** A parsed archive. */
export interface BackupArchive {
  readonly records: readonly ZipRecord[];
  readonly manifest: BackupManifest;
  /** Content of an entry by name. */
  entry(name: string): Buffer;
}

/** Parses an archive and its manifest. */
export function openArchive(data: Buffer): BackupArchive {
  const records = readCentralDirectory(data);
  const byName = new Map(records.map((record) => [record.name, record]));
  const entry = (name: string): Buffer => {
    const record = byName.get(name);
    if (record === undefined) throw new Error(`archive has no entry ${name}`);
    return readEntry(data, record, record.size + 1);
  };
  return { records, manifest: JSON.parse(entry('manifest.json').toString('utf8')) as BackupManifest, entry };
}

/** Reads a committed fixture archive (`tests/fixtures/backup/archives/<name>`). */
export function fixtureArchive(name: string): Buffer {
  return readFileSync(path.join(fixtureArchiveDir, name));
}

// ---- JSON Schema subset ---------------------------------------------------------------

/** A JSON Schema node of the subset. */
type Schema = Readonly<Record<string, unknown>>;

interface Context {
  readonly root: Schema;
  readonly errors: string[];
}

/** One keyword check: pushes problems for `value` at `at`. */
type Rule = (schema: Schema, value: unknown, at: string, context: Context) => void;

function typeOf(value: unknown): string {
  if (value === null) return 'null';
  if (Array.isArray(value)) return 'array';
  if (typeof value === 'number') return Number.isInteger(value) ? 'integer' : 'number';
  return typeof value;
}

function resolve(schema: Schema, context: Context): Schema {
  const ref = schema.$ref;
  if (typeof ref !== 'string') return schema;
  return ref.replace(/^#\//, '').split('/').reduce<Schema>((node, key) => node[key] as Schema, context.root);
}

function typeMatches(expected: unknown, actual: string): boolean {
  const types = Array.isArray(expected) ? (expected as string[]) : [expected as string];
  return types.some((type) => type === actual || (type === 'number' && actual === 'integer'));
}

const same = (left: unknown, right: unknown): boolean => JSON.stringify(left) === JSON.stringify(right);

/** Length in code points (the schema's maxLength semantics); the texts are ASCII paths and names. */
const codePoints = (text: string): number => Array.from(text).length;

/** JSON type of a value with integers counted as numbers. */
const kindOf = (value: unknown): string => typeOf(value).replace('integer', 'number');

/** A rule that runs `failed` when `keyword` is present and the value has the JSON type `kind`. */
function limit(keyword: string, kind: string, failed: (bound: unknown, value: unknown) => boolean, message: string): Rule {
  return (schema, value, at, context) => {
    const bound = schema[keyword];
    if (bound === undefined || kindOf(value) !== kind) return;
    if (failed(bound, value)) context.errors.push(`${at}: ${message} ${JSON.stringify(bound)}`);
  };
}

const ruleConst: Rule = (schema, value, at, context) => {
  if ('const' in schema && !same(schema.const, value)) context.errors.push(`${at}: must equal ${JSON.stringify(schema.const)}`);
};

const ruleEnum: Rule = (schema, value, at, context) => {
  if (Array.isArray(schema.enum) && !schema.enum.some((option) => same(option, value))) context.errors.push(`${at}: not in enum`);
};

const ruleNot: Rule = (schema, value, at, context) => {
  if (schema.not === undefined) return;
  const inner: Context = { root: context.root, errors: [] };
  check(schema.not as Schema, value, at, inner);
  if (inner.errors.length === 0) context.errors.push(`${at}: matches a forbidden schema`);
};

const ruleRequired: Rule = (schema, value, at, context) => {
  if (typeOf(value) !== 'object') return;
  const missing = ((schema.required ?? []) as string[]).filter((name) => !(name in (value as object)));
  context.errors.push(...missing.map((name) => `${at}: missing ${name}`));
};

/** Checks one object member against its property schema, or the `additionalProperties: false` rule. */
function checkMember(schema: Schema, member: readonly [string, unknown], at: string, context: Context): void {
  const child = ((schema.properties ?? {}) as Readonly<Record<string, Schema | undefined>>)[member[0]];
  if (child !== undefined) check(child, member[1], `${at}.${member[0]}`, context);
  else if (schema.additionalProperties === false) context.errors.push(`${at}: unexpected ${member[0]}`);
}

const ruleProperties: Rule = (schema, value, at, context) => {
  if (typeOf(value) !== 'object') return;
  for (const member of Object.entries(value as object)) checkMember(schema, member, at, context);
};

const ruleItems: Rule = (schema, value, at, context) => {
  if (schema.items === undefined || !Array.isArray(value)) return;
  value.forEach((item, index) => {
    check(schema.items as Schema, item, `${at}[${String(index)}]`, context);
  });
};

const ruleUnique: Rule = (schema, value, at, context) => {
  if (schema.uniqueItems !== true || !Array.isArray(value)) return;
  if (new Set(value.map((item) => JSON.stringify(item))).size !== value.length) context.errors.push(`${at}: items not unique`);
};

const rules: readonly Rule[] = [
  ruleConst,
  ruleEnum,
  limit('pattern', 'string', (pattern, text) => !new RegExp(pattern as string, 'u').test(text as string), 'does not match'),
  limit('maxLength', 'string', (max, text) => codePoints(text as string) > (max as number), 'longer than'),
  limit('minLength', 'string', (min, text) => codePoints(text as string) < (min as number), 'shorter than'),
  limit('minimum', 'number', (min, number) => (number as number) < (min as number), 'below'),
  limit('maximum', 'number', (max, number) => (number as number) > (max as number), 'above'),
  limit('minItems', 'array', (min, items) => (items as readonly unknown[]).length < (min as number), 'fewer items than'),
  limit('maxItems', 'array', (max, items) => (items as readonly unknown[]).length > (max as number), 'more items than'),
  ruleUnique,
  ruleNot,
  ruleRequired,
  ruleProperties,
  ruleItems,
];

function check(node: Schema, value: unknown, at: string, context: Context): void {
  const schema = resolve(node, context);
  if (schema.type !== undefined && !typeMatches(schema.type, typeOf(value))) {
    context.errors.push(`${at}: expected ${JSON.stringify(schema.type)}, got ${typeOf(value)}`);
    return;
  }
  for (const rule of rules) rule(schema, value, at, context);
}

/** Validates `value` against a schema of the subset; returns the problems (empty = valid). */
export function validateSchema(schema: Readonly<Record<string, unknown>>, value: unknown): string[] {
  const context: Context = { root: schema, errors: [] };
  check(schema, value, '$', context);
  return context.errors;
}

/** Validates a manifest against `backup-manifest.schema.json` 1.0.0. */
export function validateManifest(manifest: unknown): string[] {
  return validateSchema(JSON.parse(readFileSync(manifestSchemaPath, 'utf8')) as Schema, manifest);
}

const packaging = new Set(['manifest.json', 'README.txt']);

/** Problems of the entry list itself: order, README, duplicates, links, unlisted entries. */
function layoutProblems(archive: BackupArchive): string[] {
  const names = archive.records.map((record) => record.name);
  const listed = new Set(archive.manifest.entries.map((entry) => entry.path));
  const checks: readonly [boolean, string][] = [
    [names.at(-1) !== 'manifest.json', 'manifest.json is not the last entry'],
    [!names.includes('README.txt'), 'README.txt missing'],
    [new Set(names).size !== names.length, 'duplicate entry names'],
    [archive.records.some(isLink), 'link entry'],
  ];
  const unlisted = names.filter((name) => !listed.has(name) && !packaging.has(name) && !name.endsWith('/'));
  return [...checks.filter(([failed]) => failed).map(([, message]) => message), ...unlisted.map((name) => `unlisted entry ${name}`)];
}

/** Problem of one listed entry (missing, wrong size or hash), or undefined. */
function entryProblem(archive: BackupArchive, entry: ManifestEntry): string | undefined {
  if (!archive.records.some((record) => record.name === entry.path)) return `missing entry ${entry.path}`;
  const content = archive.entry(entry.path);
  return content.length === entry.sizeBytes && sha256(content) === entry.sha256 ? undefined : `size or hash differs: ${entry.path}`;
}

/** Totals that disagree with `entries[]`. */
function totalsProblems(manifest: BackupManifest): string[] {
  const bytes = manifest.entries.reduce((sum, entry) => sum + entry.sizeBytes, 0);
  return manifest.totals.entries === manifest.entries.length && manifest.totals.bytes === bytes ? [] : ['totals differ from entries[]'];
}

/**
 * Structural check of an archive the stack wrote (backup-archive 1.0.0 sections 1-3): manifest
 * last and schema-valid, README present, no other entries, every listed entry present with its
 * size and SHA-256, totals consistent, no links or duplicates. Returns the problems.
 */
export function archiveProblems(data: Buffer): string[] {
  const archive = openArchive(data);
  const entries = archive.manifest.entries.map((entry) => entryProblem(archive, entry)).filter((problem) => problem !== undefined);
  return [...validateManifest(archive.manifest), ...layoutProblems(archive), ...entries, ...totalsProblems(archive.manifest)];
}

/** `files/<path>` hash map of an archive's manifest (path below the data directory -> SHA-256). */
export function fileHashes(manifest: BackupManifest): Map<string, string> {
  return new Map(manifest.entries.filter((entry) => entry.path.startsWith('files/')).map((entry) => [entry.path.slice('files/'.length), entry.sha256]));
}
