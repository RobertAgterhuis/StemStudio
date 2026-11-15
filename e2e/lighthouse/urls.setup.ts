import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { chromium } from '@playwright/test';
import { webBaseUrl } from '../support/env';
import { expect, test } from '../support/ux-test';

// T064 / SC-003: seeds the shared data and hands the five URLs plus Playwright's Chromium to
// `run-lighthouse.mts` (TEST-STRATEGY.md 10.4).
const outFile = path.resolve(__dirname, '..', '.state', 'lighthouse-urls.json');

test('write the Lighthouse URLs of Import, Tracks, Track, Mixes and Mixer', ({ uxData }) => {
  const track = uxData.detailTrack.id;
  const urls = ['/', '/tracks', `/tracks/${track}`, '/mixes', `/tracks/${track}/mixer`].map((route) => new URL(route, webBaseUrl).href);
  mkdirSync(path.dirname(outFile), { recursive: true });
  writeFileSync(outFile, JSON.stringify({ urls, chromePath: chromium.executablePath() }, null, 2));
  expect(urls).toHaveLength(5);
});
