/**
 * Text matching helpers shared by page objects and support code.
 *
 * @packageDocumentation
 */

/** Escapes a literal for use inside a regular expression. */
function escape(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * A case-insensitive pattern that matches a whole (trimmed) text equal to one of `values`.
 * @param values Literal alternatives, for example status names.
 */
export function oneOf(values: readonly string[]): RegExp {
  return new RegExp('^\\s*(?:' + values.map(escape).join('|') + ')\\s*$', 'i');
}
