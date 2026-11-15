import { expect, test } from '@playwright/test';
import { landingErrors, maxClockDriftMs, maxLandingErrorMs, type KaraokeSample } from '../support/karaoke-probe';
import { fakeAlignedLines, parseFakeLyricsMode, readFakeLyrics } from '../support/lyrics';
import { clefs, crossNoteheads, expectedFakeBars, fifths, hasPickup, isMidi, isPdf, measureCount, noteTypes, staves, timeSignature, voices } from '../support/musicxml';

// Features 006/008: self-test of the stack-free helpers the karaoke, lyrics and sheet music specs
// rely on (landing maths, fake lyrics table, MusicXML readers). Needs no stack; green before the
// UI exists.
const fake = readFakeLyrics();
const sample = (lineIndex: number, wordIndex: number, referenceMs: number, positionMs = referenceMs): KaraokeSample => ({ state: 'singing', lineIndex, wordIndex, positionMs, referenceMs, at: 0 });
const score = `<?xml version="1.0"?><score-partwise><part id="P1">
<measure number="0" implicit="yes"><attributes><key><fifths>1</fifths></key><time><beats>4</beats><beat-type>4</beat-type></time>
<staves>2</staves><clef number="1"><sign>G</sign><line>2</line></clef><clef number="2"><sign>F</sign><line>4</line><clef-octave-change>-1</clef-octave-change></clef></attributes>
<note><type>quarter</type><voice>1</voice><notehead>x</notehead></note></measure>
<measure number="1"><note><type>eighth</type><voice>2</voice></note></measure></part></score-partwise>`;

test.describe('karaoke landing maths', () => {
  test('a word change is measured against its start and the offset', () => {
    const lines = fake.lines;
    const landings = landingErrors([sample(0, 0, 4010), sample(0, 0, 4030), sample(0, 1, 5240)], lines, 500);
    expect(landings).toEqual([
      { word: 'Fake', errorMs: -490 },
      { word: 'line', errorMs: -20 },
    ]);
    expect(maxLandingErrorMs(landings)).toBe(490);
  });

  test('countdown samples and unknown indices are not landings', () => {
    const countdown = { ...sample(0, 0, 0), state: 'countdown' };
    expect(landingErrors([countdown, sample(9, 0, 1), sample(0, -1, 1)], fake.lines, 0)).toEqual([]);
    expect(maxLandingErrorMs([])).toBe(0);
  });

  test('clock drift compares the engine position with the reference', () => {
    expect(maxClockDriftMs([sample(0, 0, 1000, 990), sample(0, 1, 2000, 1960)], 0)).toBe(40);
  });
});

test.describe('fake lyrics table', () => {
  test('modes parse with word as the default and refuse unknown values', () => {
    expect(parseFakeLyricsMode(undefined)).toBe('word');
    expect(parseFakeLyricsMode('')).toBe('word');
    expect(parseFakeLyricsMode('plain')).toBe('plain');
    expect(() => parseFakeLyricsMode('karaoke')).toThrow('is not one of');
  });

  test('fake alignment spaces words evenly and keeps the line breaks', () => {
    const lines = fakeAlignedLines('one two\n\nthree', fake.fakeAlignment);
    expect(lines.map((l) => l.words.map((w) => w.startMs))).toEqual([[2000, 2500], [], [3000]]);
    expect(lines[2]?.endMs).toBe(3500);
  });
});

test.describe('MusicXML, PDF and MIDI readers', () => {
  test('read bars, key, time, staves, clefs, types, voices and noteheads', () => {
    expect([measureCount(score), hasPickup(score), fifths(score), timeSignature(score), staves(score)]).toEqual([2, true, 1, '4/4', 2]);
    expect(clefs(score)).toEqual([
      { sign: 'G', line: 2, octaveChange: 0 },
      { sign: 'F', line: 4, octaveChange: -1 },
    ]);
    expect([...noteTypes(score)]).toEqual(['quarter', 'eighth']);
    expect([...voices(score)]).toEqual([1, 2]);
    expect(crossNoteheads(score)).toBe(1);
  });

  test('a document without key or time gives null and one staff', () => {
    expect([fifths('<part></part>'), timeSignature('<part></part>'), staves(''), measureCount('')]).toEqual([null, null, 1, 0]);
  });

  test('the fake bar count follows the grid with and without a pickup', () => {
    const beats = Array.from({ length: 34 }, (_, i) => 0.5 + 0.6 * i);
    expect(expectedFakeBars({ beats, downbeats: beats.filter((_, i) => i % 4 === 0), beatsPerBar: 4 })).toBe(9);
    expect(expectedFakeBars({ beats, downbeats: beats.filter((_, i) => i % 4 === 1), beatsPerBar: 4 })).toBe(10);
  });

  test('file signatures', () => {
    expect([isPdf(Buffer.from('%PDF-1.7')), isPdf(Buffer.from('MThd')), isMidi(Buffer.from('MThd\0\0\0\x06'))]).toEqual([true, false, true]);
  });
});

