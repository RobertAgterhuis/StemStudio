import { expect, test } from '@playwright/test';
import { fakeMicrophoneLaunchOptions, microphonePermissions } from '../support/fake-microphone';
import { buildFlacWithComments, readVorbisComments } from '../support/flac-tags';
import {
  lyricWords, parseFakeGeneratorMode, placeholderLyrics, present, readEthicsCases, readFakeGenerator, readOwnLyrics, readOwnLyricsExpected,
  readVoiceManifest, uniqueName,
} from '../support/generative';

// Stack-free self-test of the feature 009/010 helpers and fixture tables (green without docker).
const expected = readOwnLyricsExpected();

/** Reference LRC reading of the fixture rules: drop `[mm:ss.xx]` stamps, keep the empty-stamp break. */
function stripStamps(lrc: string): string {
  return lrc.split('\n').map((line) => line.replace(/^(\[\d{2}:\d{2}(\.\d{1,3})?\])+/u, '').trimEnd()).join('\n').trim();
}

test.describe('FLAC tag reader', { tag: '@self-test' }, () => {
  test('reads the Vorbis comments with upper-case keys and values that contain "="', () => {
    const tags = readVorbisComments(buildFlacWithComments(['AI_GENERATED=true', 'generator=Fake 1.0', 'COMMENT=a=b', 'ai_voice=synthetic']));
    expect(tags).toEqual({ AI_GENERATED: ['true'], GENERATOR: ['Fake 1.0'], COMMENT: ['a=b'], AI_VOICE: ['synthetic'] });
  });

  test('a FLAC file without a comment block has no tags; other files are refused', () => {
    const streamInfoOnly = Buffer.concat([Buffer.from('fLaC'), Buffer.from([0x80, 0, 0, 34]), Buffer.alloc(34)]);
    expect(readVorbisComments(streamInfoOnly)).toEqual({});
    expect(() => readVorbisComments(Buffer.from('RIFF0000WAVE'))).toThrow('not a FLAC file');
  });
});

test.describe('fake generator modes', { tag: '@self-test' }, () => {
  test('unset means ready; unknown modes are refused with the allowed values', () => {
    expect([parseFakeGeneratorMode(undefined), parseFakeGeneratorMode(''), parseFakeGeneratorMode('oom-twice')]).toEqual(['ready', 'ready', 'oom-twice']);
    expect(() => parseFakeGeneratorMode('melt')).toThrow(/ready, no-gpu, oom, oom-twice, busy, slow, unvoiced-sample/u);
  });

  test('the expectation table has a readable pattern per failure mode', () => {
    const modes = readFakeGenerator().modes;
    expect('GPU busy: needs 13.6 GB, 4.2 GB free').toMatch(new RegExp(modes.busy.restyle.messagePattern, 'u'));
    expect([modes['oom-twice'].restyle.errorCode, modes.busy.restyle.errorCode, modes['no-gpu'].createProblem]).toEqual(['outOfMemory', 'gpuBusy', 'gpu-required']);
  });
});

test.describe('generative fixtures', { tag: '@self-test' }, () => {
  test('the voice manifest covers every sample check outcome and is CC0', () => {
    const manifest = readVoiceManifest();
    const checks = Object.values(manifest.fixtures).map((facts) => facts.expectedCheck);
    expect(new Set(checks)).toEqual(new Set(['ok', 'too-short', 'too-long', 'unvoiced']));
    expect([manifest.licence, manifest.fixtures['own-voice-20s.wav'].voicedShare >= manifest.limits.minVoicedRatio]).toEqual(['CC0-1.0', true]);
  });

  test('the own lyrics match their expected facts', () => {
    const english = readOwnLyrics('sample-en.txt');
    expect(lyricWords(english)).toBe(expected['sample-en.txt'].words);
    expect(english.split(/\n\s*\n/u)).toHaveLength(expected['sample-en.txt'].proposedMarkers.length);
    expect(stripStamps(readOwnLyrics('sample.lrc'))).toBe(expected['sample.lrc'].normalisedText);
    expect(lyricWords(readOwnLyrics('sample-nl.txt'))).toBeGreaterThan(20);
  });

  test('the ethics table quotes the spec phrases and the consent statement version', () => {
    const ethics = readEthicsCases();
    const spec = ethics.imitationRefused.filter((c) => c.source === 'spec').map((c) => c.text.toLowerCase());
    for (const phrase of ['in the style of', 'sounds like', 'voice of']) expect(spec.some((text) => text.includes(phrase)), phrase).toBe(true);
    expect([ethics.consent.statementVersion, ethics.consent.outdatedVersion]).toEqual(['consent.v1', 'consent.v0']);
    expect(ethics.consent.textEn).toMatch(/^This is my own voice\./u);
  });
});

test.describe('helpers', { tag: '@self-test' }, () => {
  test('placeholder lyrics have the exact length; names fit the 60-character limit', () => {
    expect(placeholderLyrics(4097)).toHaveLength(4097);
    expect(uniqueName('A rather long prefix for a style name', 'firefox', 'run-0123456789').length).toBeLessThanOrEqual(60);
    expect(() => present<string>(undefined, 'the thing')).toThrow('the thing is missing');
  });

  test('the fake microphone uses the file in Chromium and the built-in fake stream in Firefox', () => {
    expect(fakeMicrophoneLaunchOptions('chromium', '/x/own.wav').args).toContain('--use-file-for-fake-audio-capture=/x/own.wav');
    expect(fakeMicrophoneLaunchOptions('firefox', '/x/own.wav').firefoxUserPrefs).toMatchObject({ 'media.navigator.streams.fake': true });
    expect([microphonePermissions('chromium'), microphonePermissions('firefox')]).toEqual([['microphone'], []]);
  });
});
