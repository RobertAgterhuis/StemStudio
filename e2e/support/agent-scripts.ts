/**
 * The fake-backend agent scripts of `e2e/agent-scripts/` (feature 011, agent-script.schema.json
 * 1.0.0) and the tag convention of `scripts/agent-e2e.{sh,ps1}` (TEST-STRATEGY 17.4): every agent
 * test carries `@agent`, a scripted test also `@<script name>`, the api is restarted once per
 * script with `STEMSTUDIO_AGENT_FAKE_SCRIPT=/scripts/<name>.json`.
 *
 * @packageDocumentation
 */
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

/** Every script file (basename without `.json`); the self-test keeps this list and the folder equal. */
export const agentScripts = [
  'us1-command',
  'us2-delete-mix',
  'us2-delete-track',
  'us2-detail',
  'us2-inject',
  'us5-mixer-undo',
  'us5-save-undo',
] as const;

/** Name of one script. */
export type AgentScriptName = (typeof agentScripts)[number];

/** Tag of every agent test (excluded from `npm test`). */
export const agentTag = '@agent';

/** Tag of the test that runs with agent backend `none` (`agent-e2e none`). */
export const unavailableTag = '@agent-unavailable';

/** Replaced by the fake backend with `SendMessageRequest.context.trackId` of the run's message. */
export const contextTrackId = '{{context.trackId}}';

/** Replaced by the fake backend with `SendMessageRequest.context.mixId` of the run's message. */
export const contextMixId = '{{context.mixId}}';

/** The only placeholders a script may use (TEST-STRATEGY 17.4). */
export const knownPlaceholders: readonly string[] = [contextTrackId, contextMixId];

/** One scripted tool call (schema `$defs.call`). */
export interface AgentScriptCall {
  readonly name: string;
  readonly arguments?: Readonly<Record<string, unknown>>;
}

/** One scripted model turn (schema `$defs.turn`). */
export interface AgentScriptTurn {
  readonly text?: string;
  readonly calls?: readonly AgentScriptCall[];
  readonly usage?: Readonly<Record<string, number>>;
}

/** A parsed script file. */
export interface AgentScript {
  readonly version: 1;
  readonly description?: string;
  readonly turns: readonly AgentScriptTurn[];
}

/** Folder of the script files (mounted read-only as `/scripts` into the api). */
export const agentScriptDir = path.resolve(__dirname, '..', 'agent-scripts');

/** Script names found in {@link agentScriptDir}, sorted. */
export function scriptFilesOnDisk(): string[] {
  return readdirSync(agentScriptDir)
    .filter((file) => file.endsWith('.json'))
    .map((file) => path.basename(file, '.json'))
    .sort();
}

/** Path of the agent spec run by `scripts/agent-e2e.{sh,ps1}`. */
export const agentSpecFile = path.resolve(__dirname, '..', 'tests', 'agent.spec.ts');

/**
 * The tag lists of the tests in `file` (`tag: '@x'` or `tag: ['@x', '@y']`, the only forms the
 * suite uses; ESLint `playwright/valid-test-tags` requires literals), in source order.
 */
export function specTagLists(file: string = agentSpecFile): string[][] {
  const source = readFileSync(file, 'utf-8');
  return [...source.matchAll(/tag:\s*(\[[^\]]*\]|'[^']*')/g)].map((match) => [...(match[1] ?? '').matchAll(/'(@[^']+)'/g)].map((tag) => tag[1] ?? ''));
}

/** Problems of one test's tags against the convention (TEST-STRATEGY 17.4); `[]` when it follows it. */
export function tagProblems(tags: readonly string[]): string[] {
  const scripts = tags.filter((tag) => (agentScripts as readonly string[]).includes(tag.slice(1)));
  const problems: string[] = [];
  if (!tags.includes(agentTag)) problems.push(`${tags.join(' ')}: missing ${agentTag}`);
  if (scripts.length > 1) problems.push(`${tags.join(' ')}: more than one script tag`);
  if (tags.includes(unavailableTag) && scripts.length > 0) problems.push(`${tags.join(' ')}: ${unavailableTag} with a script tag`);
  return problems;
}

/** Parses one script file (no validation; see `agent-script-check.ts`). */
export function readAgentScript(name: AgentScriptName): AgentScript {
  return JSON.parse(readFileSync(path.join(agentScriptDir, `${name}.json`), 'utf-8')) as AgentScript;
}

/**
 * Text of the last turn of a script: the end of the assistant answer the panel shows when the run
 * completes, so tests wait for it instead of a fixed time.
 * @throws Error when the last turn has no text (every committed script ends with an answer).
 */
export function finalText(name: AgentScriptName): string {
  const text = readAgentScript(name).turns.at(-1)?.text;
  if (text === undefined || text === '') {
    throw new Error(`agent script ${name} does not end with a text turn`);
  }
  return text;
}

/** Tool names a script calls, in order (for asserting the step rows). */
export function scriptedTools(name: AgentScriptName): string[] {
  return readAgentScript(name).turns.flatMap((turn) => (turn.calls ?? []).map((call) => call.name));
}
