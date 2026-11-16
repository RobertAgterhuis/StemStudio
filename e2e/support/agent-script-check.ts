/**
 * Stack-free check of an agent script against `specs/011-ai-composer-agent/contracts/
 * agent-script.schema.json` 1.0.0 (no JSON-schema dependency: the schema is small and closed),
 * plus the suite's own rule that `{{...}}` strings are only the known context placeholders.
 * Returns readable problems; an empty list means valid.
 *
 * @packageDocumentation
 */
import { knownPlaceholders } from './agent-scripts';

type Json = Readonly<Record<string, unknown>>;

const rootKeys = ['version', 'description', 'turns'];
const turnKeys = ['text', 'calls', 'usage'];
const callKeys = ['name', 'arguments'];
const usageKeys = ['inputTokens', 'outputTokens', 'cachedInputTokens'];
const limits = { turns: 50, calls: 20, text: 16_000, description: 500, name: 100 } as const;

/** Problems of a parsed script file; `[]` when it is valid. */
export function scriptProblems(script: unknown): string[] {
  if (!isObject(script)) return ['the script is not a JSON object'];
  const problems = [...unknownKeys(script, rootKeys, 'script'), ...rootProblems(script)];
  const turns = Array.isArray(script.turns) ? (script.turns as unknown[]) : [];
  turns.forEach((turn, index) => problems.push(...turnProblems(turn, `turns[${String(index)}]`)));
  return problems;
}

function rootProblems(script: Json): string[] {
  const turns = script.turns;
  const checks: [boolean, string][] = [
    [script.version === 1, 'version must be 1'],
    [optionalString(script.description, limits.description), 'description must be a string <= 500'],
    [Array.isArray(turns) && turns.length >= 1 && turns.length <= limits.turns, 'turns must hold 1-50 turns'],
  ];
  return failed(checks);
}

function turnProblems(turn: unknown, where: string): string[] {
  if (!isObject(turn)) return [`${where} is not an object`];
  const problems = [...unknownKeys(turn, turnKeys, where)];
  if (!optionalString(turn.text, limits.text)) problems.push(`${where}.text must be a string <= 16000`);
  problems.push(...callsProblems(turn.calls, where), ...usageProblems(turn.usage, where));
  return problems;
}

function callsProblems(calls: unknown, where: string): string[] {
  if (calls === undefined) return [];
  if (!Array.isArray(calls) || calls.length > limits.calls) return [`${where}.calls must be an array of <= 20 calls`];
  return (calls as unknown[]).flatMap((call, index) => callProblems(call, `${where}.calls[${String(index)}]`));
}

function callProblems(call: unknown, where: string): string[] {
  if (!isObject(call)) return [`${where} is not an object`];
  const checks: [boolean, string][] = [
    [isName(call.name), `${where}.name must be a string of 1-100`],
    [call.arguments === undefined || isObject(call.arguments), `${where}.arguments must be an object`],
  ];
  return [...unknownKeys(call, callKeys, where), ...failed(checks), ...placeholderProblems(call.arguments, `${where}.arguments`)];
}

function isName(name: unknown): boolean {
  return typeof name === 'string' && name.length >= 1 && name.length <= limits.name;
}

function usageProblems(usage: unknown, where: string): string[] {
  if (usage === undefined) return [];
  if (!isObject(usage)) return [`${where}.usage is not an object`];
  const bad = Object.values(usage).some((value) => !Number.isInteger(value) || (value as number) < 0);
  return [...unknownKeys(usage, usageKeys, `${where}.usage`), ...(bad ? [`${where}.usage values must be integers >= 0`] : [])];
}

/** Every `{{...}}` string inside `value` (any depth) must be one of the known placeholders, as the whole string. */
function placeholderProblems(value: unknown, where: string): string[] {
  if (typeof value === 'string') return isUnknownPlaceholder(value) ? [`${where} uses the unknown placeholder ${value}`] : [];
  if (Array.isArray(value)) return (value as unknown[]).flatMap((item, index) => placeholderProblems(item, `${where}[${String(index)}]`));
  if (isObject(value)) return Object.entries(value).flatMap(([key, item]) => placeholderProblems(item, `${where}.${key}`));
  return [];
}

function isUnknownPlaceholder(text: string): boolean {
  return text.includes('{{') && !knownPlaceholders.includes(text);
}

function unknownKeys(value: Json, allowed: readonly string[], where: string): string[] {
  return Object.keys(value)
    .filter((key) => !allowed.includes(key))
    .map((key) => `${where} has the unknown property ${key}`);
}

function failed(checks: readonly [boolean, string][]): string[] {
  return checks.filter(([ok]) => !ok).map(([, problem]) => problem);
}

function optionalString(value: unknown, maxLength: number): boolean {
  return value === undefined || (typeof value === 'string' && value.length <= maxLength);
}

function isObject(value: unknown): value is Json {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
