/**
 * Text of a PDF for the 014 setlist E2E (T094): pypdf in the worker's locked environment (`uv run --project worker`), so
 * the test reads what a person would read. No shell: the bytes go in on stdin.
 *
 * @packageDocumentation
 */
import { execFileSync } from 'node:child_process';
import path from 'node:path';

/** The worker project (holds pypdf 6.x in its lock file). */
const workerProject = path.resolve(__dirname, '..', '..', 'worker');

/** Extracts every page's text with pypdf, pages joined by a form feed. */
const script = [
  'import io, sys',
  'from pypdf import PdfReader',
  'reader = PdfReader(io.BytesIO(sys.stdin.buffer.read()))',
  "sys.stdout.write('\\f'.join(page.extract_text() or '' for page in reader.pages))",
].join('\n');

/** The text of every page of `pdf`. */
export function pdfText(pdf: Buffer): string[] {
  const out = execFileSync('uv', ['run', '--quiet', '--project', workerProject, 'python', '-I', '-c', script], { input: pdf, maxBuffer: 32 * 1024 * 1024 });
  return out.toString('utf-8').split('\f');
}

/** Whether `needle` appears in `text` once whitespace runs (line wraps) are collapsed. */
export function containsText(text: string, needle: string): boolean {
  const flat = (value: string): string => value.replace(/\s+/gu, ' ').trim();
  return flat(text).includes(flat(needle));
}
