/**
 * Detail stem groups (contract 2.0.0, FR-012..FR-014): validation, lazy loading on the first
 * expansion and the expand/collapse swap, which is a pure gate change at one context time.
 *
 * @packageDocumentation
 */
import { assertUnique, type ChannelBuffers, type NamedSource } from './stem-loader';
import type { ChannelId, DetailGroup, ParentStemName, StemSource } from './types';

/** Delay between a `setExpanded` call and the context time of the swap (s). */
export const SWAP_DELAY_SECONDS = 0.01;

/** Parents that may carry a group (`bass` is never split). */
const SPLITTABLE: ReadonlySet<string> = new Set<ParentStemName>(['vocals', 'drums', 'guitar', 'piano', 'other']);

/** What the groups need from the rest of the engine. */
export interface GroupHost {
  /** Fetches and decodes files (rejects naming the file; rejects after engine disposal). */
  load(sources: readonly NamedSource<ChannelId>[]): Promise<ChannelBuffers>;
  /** Context time of the next swap: `currentTime + SWAP_DELAY_SECONDS`, never before the shared `when`. */
  swapTime(): number;
  /** Adds decoded detail buffers to the transport (started at `at` when playing). */
  attach(buffers: ChannelBuffers, at: number): void;
  /** Expands or collapses `parent` in the mixer at context time `at`. */
  swap(parent: ParentStemName, expanded: boolean, at: number): void;
}

/**
 * @param group - A detail group.
 * @returns Its files keyed by channel id (`<parent>.<detail>`).
 */
export function sourcesOf(group: DetailGroup): NamedSource<ChannelId>[] {
  return group.stems.map((stem) => ({ name: `${group.parent}.${stem.name}`, url: stem.url }));
}

/**
 * @param groups - Groups of a track.
 * @returns The channel ids of all detail stems, group by group.
 */
export function groupChannels(groups: readonly DetailGroup[]): ChannelId[] {
  return groups.flatMap((group) => sourcesOf(group).map((source) => source.name));
}

/**
 * Checks the groups of a load request before anything is fetched.
 *
 * @param stems - Standard stems of the request.
 * @param groups - Detail groups of the request.
 * @throws Error for a parent that is not a splittable stem of the track, a duplicate group or a
 *   duplicate detail stem.
 */
export function validateGroups(stems: readonly StemSource[], groups: readonly DetailGroup[]): void {
  const loaded = new Set<string>(stems.map((stem) => stem.name));
  const seen = new Set<string>();
  for (const group of groups) {
    assertKnownParent(group.parent, loaded);
    if (seen.has(group.parent)) {
      throw new Error(`Duplicate group "${group.parent}"`);
    }
    seen.add(group.parent);
    assertUnique(sourcesOf(group));
  }
}

function assertKnownParent(parent: string, loaded: ReadonlySet<string>): void {
  if (!SPLITTABLE.has(parent) || !loaded.has(parent)) {
    throw new Error(`Unknown group parent "${parent}"`);
  }
}

/** Groups of the loaded track with their load and expansion state. */
export class StemGroups {
  private groups = new Map<ParentStemName, DetailGroup>();
  private readonly loads = new Map<ParentStemName, Promise<void>>();
  private readonly loaded = new Set<ParentStemName>();
  private readonly wanted = new Set<ParentStemName>();
  private generation = 0;

  /** @param host - Loader, transport and mixer hooks. */
  public constructor(private readonly host: GroupHost) {}

  /** @param groups - Validated groups of a new track; forgets every loaded or pending group. */
  public reset(groups: readonly DetailGroup[]): void {
    this.generation += 1;
    this.groups = new Map(groups.map((group) => [group.parent, group]));
    this.loads.clear();
    this.loaded.clear();
    this.wanted.clear();
  }

  /** Forgets every group and loaded or pending group load (engine disposal). */
  public dispose(): void {
    this.reset([]);
  }

  /**
   * Expands (first time: after fetching and decoding the group) or collapses a parent.
   *
   * @param parent - Parent stem.
   * @param expanded - New state.
   * @throws Error for a parent without a group, a failed load (the parent stays collapsed) or a
   *   track replaced while the group was loading.
   */
  public async setExpanded(parent: ParentStemName, expanded: boolean): Promise<void> {
    const group = this.find(parent);
    if (expanded) {
      this.wanted.add(parent);
    } else {
      this.wanted.delete(parent);
    }
    if (expanded && !this.loaded.has(parent)) {
      await this.ensureLoaded(group);
      return;
    }
    this.host.swap(parent, expanded, this.host.swapTime());
  }

  private find(parent: ParentStemName): DetailGroup {
    const group = this.groups.get(parent);
    if (group === undefined) {
      throw new Error(`No detail group for "${parent}"`);
    }
    return group;
  }

  private ensureLoaded(group: DetailGroup): Promise<void> {
    const pending = this.loads.get(group.parent) ?? this.startLoad(group);
    this.loads.set(group.parent, pending);
    return pending;
  }

  private startLoad(group: DetailGroup): Promise<void> {
    const generation = this.generation;
    return this.load(group, generation).catch((error: unknown) => {
      this.forget(group.parent, generation);
      throw error;
    });
  }

  private async load(group: DetailGroup, generation: number): Promise<void> {
    const buffers = await this.host.load(sourcesOf(group));
    if (generation !== this.generation) {
      throw new Error(`Track changed while loading the detail stems of "${group.parent}"`);
    }
    this.loaded.add(group.parent);
    const at = this.host.swapTime();
    this.host.attach(buffers, at);
    if (this.wanted.has(group.parent)) {
      this.host.swap(group.parent, true, at);
    }
  }

  private forget(parent: ParentStemName, generation: number): void {
    if (generation === this.generation) {
      this.loads.delete(parent);
      this.wanted.delete(parent);
    }
  }
}
