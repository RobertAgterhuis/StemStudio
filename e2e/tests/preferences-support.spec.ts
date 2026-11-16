import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { tourVersions } from '../support/preferences';
import { expect, test } from '../support/test';

// Stack-free self-test (012 integration): the tours recorded as completed in the storage states
// (support/preferences.ts) carry the content versions of the shipped tour files, otherwise a bumped
// tour (012 added a practice step to mixer-basics and mixer-advanced) is offered again on every
// page and changes the screenshots and keyboard flows. Runs in `npm test` without a stack.

const toursDir = path.resolve(__dirname, '..', '..', 'src', 'StemStudio.Web', 'Help', 'tours');

/** `id -> version` of every tour file shipped with the Web app. */
function shippedTourVersions(): Record<string, number> {
  const entries = readdirSync(toursDir)
    .filter((file) => file.endsWith('.json'))
    .map((file) => JSON.parse(readFileSync(path.join(toursDir, file), 'utf8')) as { id: string; version: number })
    .map((tour) => [tour.id, tour.version] as const);
  return Object.fromEntries(entries);
}

test.describe('stored tour versions', { tag: '@self-test' }, () => {
  test('equal the versions of the shipped tour files', () => {
    expect({ ...tourVersions }).toEqual(shippedTourVersions());
  });
});
