/**
 * Backend client for the feature 010 routes (`specs/010-lyrics-to-song/contracts/backend-api.openapi.yaml`
 * 1.9.0): lyrics sheets, songs, voice profiles with consent, and voice conversions. Problems are
 * returned raw so the specs can assert the problem type (`consent-required`, `library-audio`, ...).
 *
 * @packageDocumentation
 */
import { readFileSync } from 'node:fs';
import path from 'node:path';
import type { APIRequestContext, APIResponse } from '@playwright/test';
import type { GenerationDto } from './generation-api';
import { okJson } from './http';

/** Contract `LyricsSheet`. */
export interface LyricsSheetDto {
  readonly id: string;
  readonly title: string;
  readonly language: string;
  readonly text: string;
}

/** Contract `SectionProposal`. */
export interface SectionProposalDto {
  readonly text: string;
  readonly sections: readonly { readonly marker: string; readonly number: number | null; readonly lineCount: number }[];
  readonly wordsPerSecondAt120s: number;
}

/** Contract `SongRequest`. */
export interface SongRequestDto {
  readonly lyricsId?: string | null;
  readonly lyricsText?: string | null;
  readonly title?: string | null;
  readonly language: string;
  readonly styleId?: string | null;
  readonly freeCaption?: string | null;
  readonly leadVoice: 'female' | 'male' | 'any';
  readonly delivery?: string | null;
  readonly instrumental?: boolean;
  readonly durationSeconds?: number;
  readonly takes?: number;
}

/** Contract `ConsentStatement`. */
export interface ConsentStatementDto {
  readonly version: string;
  readonly text: string;
  readonly textNl: string;
}

/** Contract `VoiceProfile`. */
export interface VoiceProfileDto {
  readonly id: string;
  readonly displayName: string;
  readonly consent: { readonly statementVersion: string; readonly personName: string; readonly consentedAt: string };
  readonly source: 'recorded' | 'uploaded';
  readonly durationSeconds: number;
  readonly voicedRatio: number;
  readonly sampleUrl: string;
  readonly convertedTakeCount: number;
}

/** Form fields of `createVoiceProfile` besides the sample. */
export interface VoiceProfileForm {
  readonly displayName: string;
  readonly personName: string;
  readonly consentConfirmed: boolean;
  readonly consentStatementVersion: string;
  readonly source?: 'recorded' | 'uploaded';
}

/** Typed wrapper around an `APIRequestContext` whose `baseURL` is the API. */
export class VoiceApi {
  /** @param request Context created with `baseURL` = `E2E_API_URL`. */
  constructor(private readonly request: APIRequestContext) {}

  /** `POST /api/lyrics`; the raw response (201 or a problem). */
  async createLyrics(title: string, language: string, text: string): Promise<APIResponse> {
    return this.request.post('/api/lyrics', { data: { title, language, text } });
  }

  /** {@link createLyrics} asserting success; the sheet. */
  async createLyricsOk(title: string, language: string, text: string): Promise<LyricsSheetDto> {
    return okJson<LyricsSheetDto>(await this.createLyrics(title, language, text));
  }

  /** `PUT /api/lyrics/{id}`; the sheet. */
  async updateLyrics(id: string, title: string, language: string, text: string): Promise<LyricsSheetDto> {
    return okJson<LyricsSheetDto>(await this.request.put(`/api/lyrics/${id}`, { data: { title, language, text } }));
  }

  /** `DELETE /api/lyrics/{id}`; the HTTP status. */
  async deleteLyrics(id: string): Promise<number> {
    return (await this.request.delete(`/api/lyrics/${id}`)).status();
  }

  /** `GET /api/lyrics/{id}`; the raw response. */
  async getLyrics(id: string): Promise<APIResponse> {
    return this.request.get(`/api/lyrics/${id}`);
  }

  /** `POST /api/lyrics/upload` with a local file; the raw response. */
  async uploadLyrics(file: string, title: string, language: string): Promise<APIResponse> {
    const buffer = readFileSync(file);
    const multipart = { file: { name: path.basename(file), mimeType: 'text/plain', buffer }, title, language };
    return this.request.post('/api/lyrics/upload', { multipart });
  }

  /** `POST /api/lyrics/sections`; the proposal. */
  async proposeSections(text: string): Promise<SectionProposalDto> {
    return okJson<SectionProposalDto>(await this.request.post('/api/lyrics/sections', { data: { text } }));
  }

  /** `POST /api/songs`; the raw response (202 or a problem). */
  async createSong(body: SongRequestDto): Promise<APIResponse> {
    return this.request.post('/api/songs', { data: body });
  }

  /** {@link createSong} asserting 202; the queued generation. */
  async createSongOk(body: SongRequestDto): Promise<GenerationDto> {
    return okJson<GenerationDto>(await this.createSong(body));
  }

  /** `GET /api/consent/voice`. */
  async consentStatement(): Promise<ConsentStatementDto> {
    return okJson<ConsentStatementDto>(await this.request.get('/api/consent/voice'));
  }

  /** `POST /api/voice-profiles` (multipart) with the sample bytes; the raw response. */
  async createProfile(sample: { readonly name: string; readonly bytes: Buffer }, form: VoiceProfileForm): Promise<APIResponse> {
    const multipart = {
      sample: { name: sample.name, mimeType: 'application/octet-stream', buffer: sample.bytes },
      displayName: form.displayName,
      personName: form.personName,
      consentConfirmed: String(form.consentConfirmed),
      consentStatementVersion: form.consentStatementVersion,
      source: form.source ?? 'uploaded',
    };
    return this.request.post('/api/voice-profiles', { multipart });
  }

  /** {@link createProfile} with a local file. */
  async createProfileFromFile(file: string, form: VoiceProfileForm): Promise<APIResponse> {
    return this.createProfile({ name: path.basename(file), bytes: readFileSync(file) }, form);
  }

  /** `GET /api/voice-profiles`. */
  async listProfiles(): Promise<VoiceProfileDto[]> {
    return okJson<VoiceProfileDto[]>(await this.request.get('/api/voice-profiles'));
  }

  /** `DELETE /api/voice-profiles/{id}?deleteTakes=`; the HTTP status. */
  async deleteProfile(id: string, deleteTakes: boolean): Promise<number> {
    return (await this.request.delete(`/api/voice-profiles/${id}`, { params: { deleteTakes: String(deleteTakes) } })).status();
  }

  /** `POST /api/generations/{id}/takes/{n}/voice-conversions`; the raw response. */
  async convert(generationId: string, index: number, profileId: string): Promise<APIResponse> {
    const url = `/api/generations/${generationId}/takes/${String(index)}/voice-conversions`;
    return this.request.post(url, { data: { profileId, pitchShift: 0, autoShift: true } });
  }
}
