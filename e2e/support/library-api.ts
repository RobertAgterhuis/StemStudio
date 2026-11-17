/**
 * Backend client for the feature 022 library routes (`specs/022-library-folders/contracts/backend-api.library-folders.openapi.yaml`,
 * full document 1.22.0): folder views, live search and library info. Used for set-up and for
 * asserting server state; the UI is driven through the page objects.
 *
 * @packageDocumentation
 */
import { expect, type APIRequestContext, type APIResponse } from '@playwright/test';
import { okJson } from './http';

/** `FolderLevel`. */
export type FolderLevel = 'artist' | 'album' | 'song';

/** `PlacementSource`. */
export type PlacementSource = 'item' | 'track' | 'match' | 'import' | 'fallback';

/** `FolderSort`. */
export type FolderSort = 'name' | 'year' | 'recent' | 'size' | 'items';

/** `FolderRef`. */
export interface FolderRefDto {
  readonly id: string;
  readonly level: FolderLevel;
  readonly name: string;
}

/** `FolderArt`. */
export interface FolderArtDto {
  readonly kind: 'cover' | 'mosaic' | 'placeholder';
  readonly images: readonly { readonly thumbUrl: string; readonly coverUrl: string }[];
  readonly placeholder: { readonly glyph: string; readonly tone: number };
}

/** `LibraryFolder`. */
export interface LibraryFolderDto {
  readonly id: string;
  readonly level: FolderLevel;
  readonly name: string;
  readonly nameSource: PlacementSource;
  readonly year: number | null;
  readonly counts: { readonly albums?: number; readonly songs?: number; readonly items: number };
  readonly sizeBytes: number;
  readonly latestAt: string;
  readonly art: FolderArtDto;
  readonly trackIds: readonly string[];
}

/** `PlacementLevel`. */
export interface PlacementLevelDto {
  readonly folderId: string;
  readonly name: string;
  readonly source: PlacementSource;
}

/** `Placement` (new required `Asset.placement`, 1.22.0). */
export interface PlacementDto {
  readonly artist: PlacementLevelDto;
  readonly album: PlacementLevelDto;
  readonly song: PlacementLevelDto;
  readonly year: number | null;
  readonly filed: boolean;
}

/** The parts of `Asset` the 022 specs read. */
export interface PlacedAssetDto {
  readonly id: string;
  readonly kind: string;
  readonly name: string;
  readonly status: string;
  readonly provenance: { readonly trackId: string | null; readonly trackAvailable: boolean };
  readonly placement: PlacementDto | null;
}

/** `LibraryFolderView`. */
export interface LibraryFolderViewDto {
  readonly revision: string;
  readonly folder: LibraryFolderDto | null;
  readonly breadcrumb: readonly FolderRefDto[];
  readonly children: readonly LibraryFolderDto[];
  readonly childrenTotal: number;
  readonly items: readonly PlacedAssetDto[];
  readonly totalItems: number;
  readonly totalSizeBytes: number;
  readonly pendingFiling: number;
}

/** `FolderHit`. */
export interface FolderHitDto {
  readonly folder: LibraryFolderDto;
  readonly path: readonly FolderRefDto[];
}

/** `ItemHit`. */
export interface ItemHitDto {
  readonly asset: PlacedAssetDto;
  readonly path: readonly FolderRefDto[];
  readonly matchedInstruments: readonly string[];
}

/** `LibrarySearchResult`. */
export interface LibrarySearchResultDto {
  readonly query: string;
  readonly tokens: readonly string[];
  readonly revision: string;
  readonly artists: readonly FolderHitDto[];
  readonly albums: readonly FolderHitDto[];
  readonly songs: readonly FolderHitDto[];
  readonly items: readonly ItemHitDto[];
  readonly totals: { readonly artists: number; readonly albums: number; readonly songs: number; readonly items: number };
}

/** `LibraryInfoRequest`: `null` = automatic for that level. */
export interface LibraryInfoRequestDto {
  readonly artist: string | null;
  readonly album: string | null;
  readonly song: string | null;
}

/** `LibraryInfoLevel`. */
export interface LibraryInfoLevelDto {
  readonly value: string | null;
  readonly effective: string;
  readonly source: PlacementSource;
  readonly automatic: string;
}

/** `LibraryInfoState`. */
export interface LibraryInfoStateDto {
  readonly artist: LibraryInfoLevelDto;
  readonly album: LibraryInfoLevelDto;
  readonly song: LibraryInfoLevelDto;
  readonly placement: PlacementDto | null;
  readonly movedItems: number;
}

/** Folder ids of one placement, root to song. */
export interface FolderPath {
  readonly artistId: string;
  readonly albumId: string;
  readonly songId: string;
}

/** The three folder ids of a placement. */
export function folderPath(placement: PlacementDto): FolderPath {
  return { artistId: placement.artist.folderId, albumId: placement.album.folderId, songId: placement.song.folderId };
}

/** "Artist > Album > Song" of a placement (the confirmation and path text). */
export function pathText(placement: PlacementDto): string {
  return `${placement.artist.name} > ${placement.album.name} > ${placement.song.name}`;
}

const pollIntervals = [250, 500, 1000];

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class LibraryApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `GET /api/library/folders` (root: artists). */
  async root(sort: FolderSort = 'name'): Promise<LibraryFolderViewDto> {
    return okJson<LibraryFolderViewDto>(await this.request.get('/api/library/folders', { params: { sort } }));
  }

  /** Raw `GET /api/library/folders/{folderId}` (404 `folder-not-found` for a gone folder). */
  async folderResponse(folderId: string, sort: FolderSort = 'name'): Promise<APIResponse> {
    return this.request.get(`/api/library/folders/${folderId}`, { params: { sort } });
  }

  /** `GET /api/library/folders/{folderId}`. */
  async folder(folderId: string, sort: FolderSort = 'name'): Promise<LibraryFolderViewDto> {
    return okJson<LibraryFolderViewDto>(await this.folderResponse(folderId, sort));
  }

  /** `GET /api/library/search`. */
  async search(q: string, options: { readonly within?: string; readonly limit?: number } = {}): Promise<LibrarySearchResultDto> {
    const params: Record<string, string | number> = { q, limit: options.limit ?? 200 };
    if (options.within !== undefined) {
      params.within = options.within;
    }
    return okJson<LibrarySearchResultDto>(await this.request.get('/api/library/search', { params }));
  }

  /** `GET /api/assets/{id}` with its placement. */
  async asset(assetId: string): Promise<PlacedAssetDto> {
    return okJson<PlacedAssetDto>(await this.request.get(`/api/assets/${assetId}`));
  }

  /** Polls until the item is filed (`placement.filed`); returns its placement. */
  async waitForPlacement(assetId: string, timeoutMs = 30_000): Promise<PlacementDto> {
    await expect.poll(async () => (await this.asset(assetId)).placement?.filed ?? false, { timeout: timeoutMs, intervals: pollIntervals }).toBe(true);
    const placement = (await this.asset(assetId)).placement;
    if (placement === null) {
      throw new Error(`asset ${assetId} has no placement`);
    }
    return placement;
  }

  /** `GET /api/tracks/{id}/library-info`. */
  async trackInfo(trackId: string): Promise<LibraryInfoStateDto> {
    return okJson<LibraryInfoStateDto>(await this.request.get(`/api/tracks/${trackId}/library-info`));
  }

  /** Raw `PUT /api/tracks/{id}/library-info` (400 for invalid names). */
  async setTrackInfoResponse(trackId: string, body: LibraryInfoRequestDto): Promise<APIResponse> {
    return this.request.put(`/api/tracks/${trackId}/library-info`, { data: body });
  }

  /** `PUT /api/tracks/{id}/library-info`. */
  async setTrackInfo(trackId: string, body: LibraryInfoRequestDto): Promise<LibraryInfoStateDto> {
    return okJson<LibraryInfoStateDto>(await this.setTrackInfoResponse(trackId, body));
  }

  /** `DELETE /api/tracks/{id}/library-info` (all automatic). */
  async resetTrackInfo(trackId: string): Promise<LibraryInfoStateDto> {
    return okJson<LibraryInfoStateDto>(await this.request.delete(`/api/tracks/${trackId}/library-info`));
  }

  /** `PUT /api/assets/{id}/library-info`. */
  async setItemInfo(assetId: string, body: LibraryInfoRequestDto): Promise<LibraryInfoStateDto> {
    return okJson<LibraryInfoStateDto>(await this.request.put(`/api/assets/${assetId}/library-info`, { data: body }));
  }

  /** `DELETE /api/assets/{id}/library-info`. */
  async resetItemInfo(assetId: string): Promise<LibraryInfoStateDto> {
    return okJson<LibraryInfoStateDto>(await this.request.delete(`/api/assets/${assetId}/library-info`));
  }

  /** Polls the root until `pendingFiling` is 0; returns the elapsed milliseconds. */
  async waitUntilFiled(timeoutMs: number): Promise<number> {
    const started = Date.now();
    await expect.poll(async () => (await this.root()).pendingFiling, { timeout: timeoutMs, intervals: pollIntervals }).toBe(0);
    return Date.now() - started;
  }
}
