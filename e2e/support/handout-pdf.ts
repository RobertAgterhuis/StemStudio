/**
 * Facts of a handout PDF for the 020 E2E (SC-005, FR-012, FR-013): page count, bookmarks, document
 * title and language, the text of every page. Read with pypdf from the worker's locked environment
 * (`uv run --project worker`, as `practice-flow-pdf.ts`), so no new npm dependency; no shell, the
 * bytes go in on stdin.
 *
 * @packageDocumentation
 */
import { execFileSync } from 'node:child_process';
import path from 'node:path';

/** What the specs read from one PDF. */
export interface HandoutPdf {
  readonly pages: number;
  readonly outline: readonly string[];
  readonly title: string | null;
  readonly lang: string | null;
  readonly texts: readonly string[];
}

const workerProject = path.resolve(__dirname, '..', '..', 'worker');

const script = [
  'import io, json, sys',
  'from pypdf import PdfReader',
  'reader = PdfReader(io.BytesIO(sys.stdin.buffer.read()))',
  'def walk(items):',
  '    for item in items:',
  '        yield from walk(item) if isinstance(item, list) else [item.title]',
  'meta = reader.metadata or {}',
  "lang = reader.trailer['/Root'].get('/Lang')",
  'json.dump({"pages": len(reader.pages), "outline": list(walk(reader.outline)),',
  '           "title": meta.get("/Title"), "lang": str(lang) if lang is not None else None,',
  '           "texts": [page.extract_text() or "" for page in reader.pages]}, sys.stdout)',
].join('\n');

/** Reads `pdf` with pypdf. */
export function readHandoutPdf(pdf: Buffer): HandoutPdf {
  const out = execFileSync('uv', ['run', '--quiet', '--project', workerProject, 'python', '-I', '-c', script], { input: pdf, maxBuffer: 64 * 1024 * 1024 });
  return JSON.parse(out.toString('utf-8')) as HandoutPdf;
}

/** `text` with whitespace runs (line wraps) collapsed to one space. */
export function flat(text: string): string {
  return text.replace(/\s+/gu, ' ').trim();
}
