import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fileUrl, recorderUnavailableReason, revokeUrl, startRecording } from '../src/recorder';

/** Fake MediaRecorder: records what it was asked, emits one chunk on stop. */
class FakeRecorder extends EventTarget {
  static supported = true;
  static instances: FakeRecorder[] = [];
  state: 'inactive' | 'recording' = 'inactive';
  readonly mimeType: string;
  timeslice: number | undefined;

  constructor(
    readonly stream: MediaStream,
    options?: { mimeType?: string },
  ) {
    super();
    this.mimeType = options?.mimeType ?? 'audio/ogg';
    FakeRecorder.instances.push(this);
  }

  static isTypeSupported(): boolean {
    return FakeRecorder.supported;
  }

  start(timeslice?: number): void {
    this.state = 'recording';
    this.timeslice = timeslice;
  }

  stop(): void {
    this.state = 'inactive';
    const data = new Blob([new Uint8Array([1, 2, 3])], { type: this.mimeType });
    this.dispatchEvent(Object.assign(new Event('dataavailable'), { data }));
    this.dispatchEvent(new Event('stop'));
  }
}

interface FakeTrack {
  stop: ReturnType<typeof vi.fn>;
}

function fakeStream(): { stream: MediaStream; track: FakeTrack } {
  const track = { stop: vi.fn() };
  return { stream: { getTracks: () => [track] } as unknown as MediaStream, track };
}

function installMicrophone(getUserMedia: (constraints: MediaStreamConstraints) => Promise<MediaStream>): void {
  vi.stubGlobal('navigator', { mediaDevices: { getUserMedia: vi.fn(getUserMedia) } });
}

beforeEach(() => {
  FakeRecorder.instances = [];
  FakeRecorder.supported = true;
  vi.stubGlobal('MediaRecorder', FakeRecorder);
  vi.stubGlobal('isSecureContext', true);
  URL.createObjectURL = vi.fn(() => 'blob:preview');
  URL.revokeObjectURL = vi.fn();
});

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

describe('recorderUnavailableReason', () => {
  it('is null when the browser can record', () => {
    installMicrophone(() => Promise.resolve(fakeStream().stream));
    expect(recorderUnavailableReason()).toBeNull();
  });

  it('asks for a secure connection outside https and localhost', () => {
    installMicrophone(() => Promise.resolve(fakeStream().stream));
    vi.stubGlobal('isSecureContext', false);
    expect(recorderUnavailableReason()).toMatch(/secure connection/u);
  });

  it('explains a browser without microphone access', () => {
    vi.stubGlobal('navigator', {});
    expect(recorderUnavailableReason()).toMatch(/cannot record/u);
  });

  it('explains a browser without MediaRecorder', () => {
    installMicrophone(() => Promise.resolve(fakeStream().stream));
    vi.stubGlobal('MediaRecorder', undefined);
    expect(recorderUnavailableReason()).toMatch(/cannot record/u);
  });
});

describe('startRecording', () => {
  it('records dry singing as WebM/Opus and returns the bytes on stop', async () => {
    const { stream, track } = fakeStream();
    installMicrophone(() => Promise.resolve(stream));

    const session = await startRecording(60);
    const recorder = FakeRecorder.instances[0];
    expect(recorder?.state).toBe('recording');
    expect(session.mimeType()).toBe('audio/webm;codecs=opus');
    const constraints = vi.mocked(navigator.mediaDevices.getUserMedia).mock.calls[0]?.[0];
    expect(constraints).toEqual({ audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false } });

    const bytes = await session.stop();
    expect(Array.from(bytes)).toEqual([1, 2, 3]);
    expect(track.stop).toHaveBeenCalled();
    expect(session.previewUrl()).toBe('blob:preview');
  });

  it('uses the browser default format when WebM/Opus is not supported', async () => {
    FakeRecorder.supported = false;
    installMicrophone(() => Promise.resolve(fakeStream().stream));

    const session = await startRecording(60);

    expect(session.mimeType()).toBe('audio/ogg');
  });

  it('stops by itself after the maximum length', async () => {
    vi.useFakeTimers();
    installMicrophone(() => Promise.resolve(fakeStream().stream));
    const session = await startRecording(60);

    vi.advanceTimersByTime(60_000);

    expect(FakeRecorder.instances[0]?.state).toBe('inactive');
    expect(Array.from(await session.stop())).toEqual([1, 2, 3]);
  });

  it('has no preview before it is stopped', async () => {
    installMicrophone(() => Promise.resolve(fakeStream().stream));
    const session = await startRecording(60);

    expect(session.previewUrl()).toBeNull();
  });

  it('dispose stops the microphone and releases the preview', async () => {
    const { stream, track } = fakeStream();
    installMicrophone(() => Promise.resolve(stream));
    const session = await startRecording(60);
    await session.stop();

    session.dispose();

    expect(track.stop).toHaveBeenCalled();
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:preview');
  });

  it('dispose while recording stops the recorder', async () => {
    installMicrophone(() => Promise.resolve(fakeStream().stream));
    const session = await startRecording(60);

    session.dispose();

    expect(FakeRecorder.instances[0]?.state).toBe('inactive');
    expect(URL.revokeObjectURL).not.toHaveBeenCalled();
  });

  it.each([
    ['NotAllowedError', /permission was denied/u],
    ['SecurityError', /permission was denied/u],
    ['NotFoundError', /No microphone/u],
    ['AbortError', /could not be started: busy/u],
  ])('rejects %s with a readable reason', async (name, reason) => {
    installMicrophone(() => Promise.reject(Object.assign(new Error('busy'), { name })));

    await expect(startRecording(60)).rejects.toThrow(reason);
  });

  it('rejects when the browser cannot record at all', async () => {
    vi.stubGlobal('navigator', {});

    await expect(startRecording(60)).rejects.toThrow(/cannot record/u);
  });
});

describe('module exports (010 voice recorder)', () => {
  it.each(['recorderUnavailableReason', 'startRecording', 'fileUrl', 'revokeUrl'])('exports %s from the entry point', async (name) => {
    const entry = (await import('../src/index')) as Record<string, unknown>;
    expect(typeof entry[name]).toBe('function');
  });
});

describe('fileUrl and revokeUrl', () => {
  it('returns a preview URL of the chosen file', () => {
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: [new File(['x'], 'voice.wav')] });

    expect(fileUrl(input)).toBe('blob:preview');
  });

  it('returns null without an input or a file', () => {
    const input = document.createElement('input');
    Object.defineProperty(input, 'files', { value: [] });

    expect(fileUrl(null)).toBeNull();
    expect(fileUrl(input)).toBeNull();
  });

  it('releases a preview URL', () => {
    revokeUrl('blob:preview');

    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:preview');
  });
});
