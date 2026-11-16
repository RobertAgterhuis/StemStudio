/**
 * Sentinel Claude keys and the leak search of the 011 follow-up claude-key E2E specs (CK-SC-001).
 *
 * The repository secret scan (`scripts/secret_scan.py`) fails on the key prefix followed by a key
 * character anywhere in a file, so no sentinel is ever written literally: they are built at run
 * time from parts, as `tests/Shared/ClaudeKey/ClaudeKeySentinels.cs` does for the .NET tests. A
 * sentinel is well formed for `ClaudeApiKey.Parse` (prefix, 40-256 characters, only letters,
 * digits, `-` and `_`, not an admin key), so the API stores it; it is never sent to Anthropic
 * (the specs never press **Test key**).
 *
 * @packageDocumentation
 */
import { runId } from './env';

/** The key prefix, assembled so this file never holds it next to a key character. */
export const claudeKeyPrefix: string = ['sk', 'ant', ''].join('-');

/** Name of the API's environment variable (assembled for the same reason). */
export const claudeKeyVariable: string = ['ANTHROPIC', 'API', 'KEY'].join('_');

/** Marker letters allowed in a sentinel (the run id is base36: lower-case letters and digits). */
function marker(name: string): string {
  return `${name}-${runId()}`.replace(/[^a-z0-9-]/g, '');
}

/**
 * A well-formed sentinel: prefix + `api03-` + marker + `-e2e-sentinel-` + 38 x `x` + `lastFour`.
 * @param name Lower-case letters naming the sentinel's role.
 * @param lastFour The last four key characters the status must show.
 */
export function sentinelKey(name: string, lastFour: string): string {
  return [claudeKeyPrefix, 'api03-', marker(name), '-e2e-sentinel-', 'x'.repeat(38), lastFour].join('');
}

/** The sentinels of one suite run (unique per `E2E_RUN_ID`, so leftovers of a run never match). */
export const sentinels = {
  /** Saved through the UI first; ends with `STO1`. */
  stored: sentinelKey('stored', 'STO1'),
  /** Saved by "Replace key"; ends with `STO2`. */
  replacement: sentinelKey('replace', 'STO2'),
  /** The api's environment value in the "environment wins" test; ends with `ENV1`. */
  environment: sentinelKey('env', 'ENV1'),
  /** Saved after the key ring was lost; ends with `REC1`. */
  recovered: sentinelKey('recover', 'REC1'),
  /** Admin-shaped: refused by the format check (reason `admin-key`), never stored. */
  admin: [claudeKeyPrefix, 'admin01-', marker('admin'), 'a'.repeat(60), 'ADM1'].join(''),
} as const;

/** Every sentinel of the run, for the scans that cover the whole run (logs, database). */
export const allSentinels: readonly string[] = Object.values(sentinels);

/** One searched form of a sentinel. */
export interface SearchForm {
  /** `plain`, `base64`, `utf16-base64` or `utf16` (raw little-endian bytes, binary scans only). */
  readonly form: string;
  readonly bytes: Buffer;
}

/** The forms a leak search looks for: the value, its UTF-8 base64, its UTF-16LE base64 and raw UTF-16LE bytes. */
export function searchForms(sentinel: string): SearchForm[] {
  const utf16 = Buffer.from(sentinel, 'utf16le');
  return [
    { form: 'plain', bytes: Buffer.from(sentinel, 'utf8') },
    { form: 'base64', bytes: Buffer.from(Buffer.from(sentinel, 'utf8').toString('base64'), 'utf8') },
    { form: 'utf16-base64', bytes: Buffer.from(utf16.toString('base64'), 'utf8') },
    { form: 'utf16', bytes: utf16 },
  ];
}

/** Where a sentinel was found: `<source>: <sentinel role> as <form>` (never the value itself). */
export type LeakReport = string;

/**
 * Searches `haystack` for every form of every sentinel.
 * @param source Name of what was scanned (shown in the report).
 * @param haystack The scanned text or bytes.
 * @param keys The sentinels to look for (default: all of the run).
 * @returns One report per hit; empty when clean. Reports name the role, never the value.
 */
export function findLeaks(source: string, haystack: string | Buffer, keys: readonly string[] = allSentinels): LeakReport[] {
  const bytes = typeof haystack === 'string' ? Buffer.from(haystack, 'utf8') : haystack;
  return keys.flatMap((key) =>
    searchForms(key)
      .filter((form) => bytes.includes(form.bytes))
      .map((form) => `${source}: sentinel ${roleOf(key)} as ${form.form}`),
  );
}

/** The role name of a sentinel (key of `sentinels`), or `unknown`. */
function roleOf(key: string): string {
  return Object.entries(sentinels).find(([, value]) => value === key)?.[0] ?? 'unknown';
}

/** The last four characters of a key (what the status may show). */
export function lastFour(key: string): string {
  return key.slice(-4);
}
