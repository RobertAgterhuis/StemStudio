/**
 * In-app voice recording for voice profiles (feature 010, FR-005, FR-015): MediaRecorder in WebM/Opus where the
 * browser supports it, the browser default format otherwise. The recording stays in the page until .NET reads it and
 * sends it to the local StemStudio API; nothing is sent anywhere else. Echo cancellation, noise suppression and
 * automatic gain are off, so the sample is the dry singing the voice conversion needs.
 *
 * @packageDocumentation
 */

/** Preferred recording format. */
const preferredType = 'audio/webm;codecs=opus';

/** Message when the browser has no microphone recording at all. */
const unsupported = 'This browser cannot record from a microphone.';

/** One running or finished recording, returned to .NET as a JS object reference. */
export interface RecordingSession {
  /** Stops the recording (if it is still running) and resolves with the recorded bytes. */
  stop(): Promise<Uint8Array>;
  /** The MIME type of the recording, e.g. `audio/webm;codecs=opus`. */
  mimeType(): string;
  /** An object URL of the finished recording for an `<audio>` preview, or null before it is stopped. */
  previewUrl(): string | null;
  /** Stops the recorder and the microphone and releases the preview URL. */
  dispose(): void;
}

/**
 * Tells whether this page can record from a microphone.
 *
 * @returns Null when recording is possible, else the readable reason (upload stays possible).
 */
export function recorderUnavailableReason(): string | null {
  if (!globalThis.isSecureContext) {
    return 'Recording needs a secure connection (https or localhost).';
  }
  return canRecord() ? null : unsupported;
}

/**
 * Asks for the microphone and starts recording; the recording stops by itself after `maxSeconds`.
 *
 * @param maxSeconds - Longest recording in seconds (60 for voice samples).
 * @returns The session.
 * @throws Error with a readable message when the microphone cannot be used (permission denied, none found).
 */
export async function startRecording(maxSeconds: number): Promise<RecordingSession> {
  if (!canRecord()) {
    throw new Error(unsupported);
  }
  const stream = await openMicrophone();
  const options = MediaRecorder.isTypeSupported(preferredType) ? { mimeType: preferredType } : {};
  return createSession(new MediaRecorder(stream, options), stream, maxSeconds);
}

/**
 * Creates a preview URL of the file chosen in a file input.
 *
 * @param input - The input element, or null.
 * @returns An object URL, or null without a file.
 */
export function fileUrl(input: HTMLInputElement | null): string | null {
  const file = input?.files?.[0];
  return file ? URL.createObjectURL(file) : null;
}

/**
 * Releases a preview URL created by {@link fileUrl}.
 *
 * @param url - The object URL.
 */
export function revokeUrl(url: string): void {
  URL.revokeObjectURL(url);
}

function canRecord(): boolean {
  const devices = (navigator as Partial<Navigator>).mediaDevices;
  return typeof MediaRecorder !== 'undefined' && typeof devices?.getUserMedia === 'function';
}

async function openMicrophone(): Promise<MediaStream> {
  try {
    return await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false } });
  } catch (error) {
    throw new Error(microphoneProblem(error as Error), { cause: error });
  }
}

function microphoneProblem(error: Error): string {
  if (error.name === 'NotAllowedError' || error.name === 'SecurityError') {
    return 'Microphone permission was denied. Allow the microphone for this page to record.';
  }
  return error.name === 'NotFoundError'
    ? 'No microphone was found. Connect one to record.'
    : `The microphone could not be started: ${error.message}`;
}

function createSession(recorder: MediaRecorder, stream: MediaStream, maxSeconds: number): RecordingSession {
  let url: string | null = null;
  const stopped = collect(recorder, stream);
  const limit = setTimeout(() => {
    stopRecorder(recorder);
  }, maxSeconds * 1000);
  recorder.start(1000);
  return {
    async stop() {
      clearTimeout(limit);
      stopRecorder(recorder);
      const blob = await stopped;
      url ??= URL.createObjectURL(blob);
      return new Uint8Array(await blob.arrayBuffer());
    },
    mimeType: () => recorder.mimeType,
    previewUrl: () => url,
    dispose() {
      clearTimeout(limit);
      stopRecorder(recorder);
      stopTracks(stream);
      if (url !== null) {
        URL.revokeObjectURL(url);
      }
    },
  };
}

/** Collects the recorded chunks; resolves with the whole recording once the recorder stops (the microphone is released). */
function collect(recorder: MediaRecorder, stream: MediaStream): Promise<Blob> {
  const chunks: Blob[] = [];
  recorder.addEventListener('dataavailable', (event) => {
    chunks.push(event.data);
  });
  return new Promise<Blob>((resolve) => {
    recorder.addEventListener('stop', () => {
      stopTracks(stream);
      resolve(new Blob(chunks, { type: recorder.mimeType }));
    });
  });
}

function stopTracks(stream: MediaStream): void {
  for (const track of stream.getTracks()) {
    track.stop();
  }
}

function stopRecorder(recorder: MediaRecorder): void {
  if (recorder.state !== 'inactive') {
    recorder.stop();
  }
}
