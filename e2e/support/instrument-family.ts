/**
 * The family rule of feature 016 (data-model 5.3) for the "Practise as", Drumless and preset specs: which mixer channels
 * belong to an instrument family, and which view "Practise as" opens for it (ui-test-ids 1.15.0 `instrument-view`
 * `data-view`).
 *
 * @packageDocumentation
 */
import type { ChannelKey } from '../pages/mixer-page';
import type { DetailStemName, ParentStemName, StemName, TrackDto } from './api';
import type { InstrumentFamily } from './instrument-views';

/** The six families in the order of the "Practise as" menu. */
export const families: readonly InstrumentFamily[] = ['vocals', 'drums', 'bass', 'guitar', 'keys', 'orchestral'];

/** `data-view` of the view "Practise as" opens per family (spec US6 scenario 1). */
export const familyView: Readonly<Record<InstrumentFamily, string>> = {
  vocals: 'singing',
  drums: 'drum-trainer',
  bass: 'tablature',
  guitar: 'tablature',
  keys: 'chords',
  orchestral: 'part',
};

/** Family of a standard stem (`other` counts as orchestral while it is not split). */
const stemFamily: Readonly<Record<StemName, InstrumentFamily>> = {
  vocals: 'vocals',
  drums: 'drums',
  bass: 'bass',
  guitar: 'guitar',
  piano: 'keys',
  other: 'orchestral',
};

/** Family of a detail stem by its name; `rest` belongs to the family of its parent. */
const detailFamily: Readonly<Record<Exclude<DetailStemName, 'rest'>, InstrumentFamily>> = {
  lead: 'vocals',
  backing: 'vocals',
  kick: 'drums',
  snare: 'drums',
  toms: 'drums',
  hihat: 'drums',
  cymbals: 'drums',
  percussion: 'drums',
  'acoustic-guitar': 'guitar',
  'electric-guitar': 'guitar',
  keys: 'keys',
  synth: 'keys',
  strings: 'orchestral',
  brass: 'orchestral',
  woodwinds: 'orchestral',
};

/** One mixer channel with its family. */
export interface FamilyChannel {
  readonly key: ChannelKey;
  readonly family: InstrumentFamily;
  /** The parent whose group must be expanded to see the strip, or null for a standard strip. */
  readonly parent: ParentStemName | null;
}

/** Family of a detail channel (data-model 5.3: by detail name, `rest` by its parent). */
export function familyOfDetail(parent: ParentStemName, detail: DetailStemName): InstrumentFamily {
  return detail === 'rest' ? stemFamily[parent] : detailFamily[detail];
}

/** A stem that can hold detail stems (`bass` never does). */
function asParent(stem: StemName): ParentStemName | null {
  return stem === 'bass' ? null : stem;
}

function detailChannels(parent: ParentStemName, stems: readonly { readonly name: DetailStemName }[]): FamilyChannel[] {
  return stems.map((s) => ({ key: `${parent}-${s.name}`, family: familyOfDetail(parent, s.name), parent }));
}

/** Every standard and detail channel of a track with its family. */
export function trackChannels(track: TrackDto): FamilyChannel[] {
  const standard = track.stems.map((s): FamilyChannel => ({ key: s.name, family: stemFamily[s.name], parent: null }));
  const details = splitParentsOf(track).flatMap((parent) =>
    detailChannels(parent, track.detail.parents.find((p) => p.parent === parent)?.stems ?? []),
  );
  return [...standard, ...details];
}

/**
 * The channels whose expected mute state after "Practise as `family`" is unambiguous: every channel of the family is
 * muted, every other one is unmuted. The parent `other` and its `rest` are left out for every family except orchestral
 * once `other` is split (data-model 5.3 counts `other` as orchestral only "while it is not split").
 */
export function expectedMutes(track: TrackDto, family: InstrumentFamily): { readonly muted: ChannelKey[]; readonly open: ChannelKey[] } {
  const split = new Set(splitParentsOf(track));
  const channels = trackChannels(track).filter((c) => !split.has('other') || !isOtherRest(c) || family === 'orchestral');
  return {
    muted: channels.filter((c) => c.family === family).map((c) => c.key),
    open: channels.filter((c) => c.family !== family).map((c) => c.key),
  };
}

function isOtherRest(channel: FamilyChannel): boolean {
  return channel.key === 'other' || channel.key === 'other-rest';
}

/** The parents with detail strips (they get an `expand-<parent>` button). */
export function splitParentsOf(track: TrackDto): ParentStemName[] {
  return track.detail.parents.filter((p) => p.stems.length > 0).flatMap((p) => asParent(p.parent) ?? []);
}
