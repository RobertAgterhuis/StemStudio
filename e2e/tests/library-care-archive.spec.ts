import { readFileSync } from 'node:fs';
import path from 'node:path';
import {
  archiveProblems,
  fixtureArchive,
  fixtureArchiveDir,
  isLink,
  openArchive,
  readCentralDirectory,
  sha256,
  validateManifest,
} from '../support/backup-archive';
import { expect, test } from '../support/test';

// Feature 013 stack-free self-test (`@self-test`, no docker, no browser page): the archive reader
// and manifest validator of support/backup-archive.ts against the committed fixture archives of
// qa T012 (tests/fixtures/backup), so the checks the 013 specs apply to the stack's archives are
// themselves proven to accept the valid archives and to see each planted defect.

interface CatalogueItem {
  readonly file: string;
  readonly sha256: string;
}

function catalogue(name: string): readonly CatalogueItem[] {
  const file = path.join(fixtureArchiveDir, '..', name);
  return (JSON.parse(readFileSync(file, 'utf8')) as { archives: CatalogueItem[] }).archives;
}

test.describe('backup archive reader (013 self-test)', { tag: ['@library-care', '@self-test'] }, () => {
  test('the committed archives match their catalogues', () => {
    for (const item of [...catalogue('hostile-manifest.json'), ...catalogue('schema-manifest.json')]) {
      expect(sha256(fixtureArchive(item.file)), item.file).toBe(item.sha256);
    }
  });

  test('valid archives at the current, 008 and 010 schemas have no problems', () => {
    for (const name of ['valid-base.zip', 'schema-008.zip', 'schema-010.zip']) {
      expect(archiveProblems(fixtureArchive(name)), name).toEqual([]);
    }
    const { manifest } = openArchive(fixtureArchive('valid-base.zip'));
    expect(manifest.voiceProfiles[0]?.consent.statementVersion).toBe('consent.v1');
    expect(manifest.contents.excluded).toEqual([{ category: 'recordings', reason: 'empty' }]);
  });

  test('the manifest validator refuses a schema violation and a wrong format', () => {
    expect(validateManifest(openArchive(fixtureArchive('h12a-manifest-schema.zip')).manifest)).toContainEqual(expect.stringContaining('hashAlgorithm'));
    expect(validateManifest(openArchive(fixtureArchive('h12b-manifest-format.zip')).manifest)).toContainEqual(expect.stringContaining('format'));
    expect(validateManifest({ format: 'stemstudio-backup' }).length).toBeGreaterThan(5);
  });

  test('links, duplicates, unlisted and missing entries are seen', () => {
    expect(readCentralDirectory(fixtureArchive('h05-symlink.zip')).some(isLink)).toBe(true);
    expect(archiveProblems(fixtureArchive('h05-symlink.zip'))).toContain('link entry');
    expect(archiveProblems(fixtureArchive('h06-duplicate-name.zip'))).toContain('duplicate entry names');
    expect(archiveProblems(fixtureArchive('h07-unlisted-entry.zip'))).toContainEqual(expect.stringMatching(/^unlisted entry files\/tracks\/.+\/artwork\/user\/cover\.jpg$/));
    expect(archiveProblems(fixtureArchive('h08-missing-entry.zip'))).toContainEqual(expect.stringMatching(/^missing entry .+piano\.flac$/));
  });

  test('raw names are read without normalisation', () => {
    const names = (file: string): string[] => readCentralDirectory(fixtureArchive(file)).map((record) => record.name);
    expect(names('h03-backslash.zip').some((name) => name.includes('\\'))).toBe(true);
    expect(names('h04a-control-character.zip').some((name) => name.includes('\u0001'))).toBe(true);
    expect(names('h01-traversal.zip')).toContain('files/../../etc/passwd');
  });

  test('a zip bomb is bounded and a flipped byte changes the hash', () => {
    expect(archiveProblems(fixtureArchive('h09-zip-bomb.zip'))).toContainEqual(expect.stringMatching(/^size or hash differs: .+other\.flac$/));
    expect(archiveProblems(fixtureArchive('h10-hash-mismatch.zip'))).toContainEqual(expect.stringMatching(/^size or hash differs: .+drums\.flac$/));
  });

  test('Zip64 sizes and a lying entry count are read as declared', () => {
    const huge = readCentralDirectory(fixtureArchive('h16-disk-space.zip')).find((record) => record.name.startsWith('files/uploads/'));
    expect(huge?.size).toBe(2 ** 50);
    expect(readCentralDirectory(fixtureArchive('h17a-manifest-too-large.zip')).at(-1)?.size).toBe(256 * 1024 * 1024 + 1);
    expect(() => readCentralDirectory(fixtureArchive('h17b-entry-count.zip'))).toThrow(/2000001 entries/);
    expect(() => readCentralDirectory(fixtureArchive('not-a-zip.zip'))).toThrow(/not a ZIP/);
  });
});
