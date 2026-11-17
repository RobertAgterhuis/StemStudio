import { describe, expect, it, vi } from 'vitest';
import { InputPort, inputConstraints, inputError } from '../../src/recording/input';
import { checkSupport } from '../../src/recording/support';
import { FakeMediaDevices, FakeStream, RecordingContext, fakeEnvironment } from '../mocks/media';
import { MockAudioContext, type MockAnalyserNode } from '../mocks/audio-context';

/** Input support, devices, opening and loss (contract 2.5.0, research R-02, FR-001..FR-003). */
function port(devices = new FakeMediaDevices(), loopback?: () => MediaStream): {
  ctx: RecordingContext;
  devices: FakeMediaDevices;
  input: InputPort;
  lost: string[];
  changed: number[];
} {
  const ctx = new RecordingContext();
  const lost: string[] = [];
  const changed: number[] = [];
  const input = new InputPort({
    context: ctx.asAudioContext(),
    env: fakeEnvironment(devices),
    onLost: (reason) => lost.push(reason),
    onDevicesChanged: () => changed.push(1),
    ...(loopback === undefined ? {} : { loopback }),
  });
  return { ctx, devices, input, lost, changed };
}

describe('checkSupport', () => {
  const ctx = new RecordingContext().asAudioContext();

  it('is supported in a secure context with getUserMedia and AudioWorklet; reports the permission', async () => {
    const devices = new FakeMediaDevices();
    expect(await checkSupport(fakeEnvironment(devices), ctx, false)).toEqual({ supported: true, reasonCode: null, reason: null, permission: 'prompt' });
    devices.granted = true;
    expect((await checkSupport(fakeEnvironment(devices), ctx, false)).permission).toBe('granted');
  });

  it('checks the secure context first, then getUserMedia, then AudioWorklet', async () => {
    const insecure = await checkSupport(fakeEnvironment(null, { secure: false }), ctx, false);
    const noMedia = await checkSupport(fakeEnvironment(null), ctx, false);
    const noWorklet = await checkSupport(fakeEnvironment(), new MockAudioContext().asAudioContext(), false);
    expect([insecure.reasonCode, noMedia.reasonCode, noWorklet.reasonCode]).toEqual(['insecureContext', 'noMediaDevices', 'noAudioWorklet']);
    expect(insecure.reason).toMatch(/Open StemStudio on the computer it runs on/);
    expect([insecure.supported, noMedia.reason !== null, noWorklet.reason !== null]).toEqual([false, true, true]);
  });

  it('reports rateMismatch after the browser refused the device rate', async () => {
    const support = await checkSupport(fakeEnvironment(), ctx, true);
    expect([support.supported, support.reasonCode]).toEqual([false, 'rateMismatch']);
  });

  it('reports the permission as unknown when it cannot be queried', async () => {
    const none = await checkSupport(fakeEnvironment(new FakeMediaDevices(), { permissions: undefined }), ctx, false);
    const throwing = await checkSupport(fakeEnvironment(new FakeMediaDevices(), { permissions: { query: () => Promise.reject(new TypeError('microphone')) } }), ctx, false);
    const odd = await checkSupport(fakeEnvironment(new FakeMediaDevices(), { permissions: { query: () => Promise.resolve({ state: 'weird' }) } }), ctx, false);
    expect([none.permission, throwing.permission, odd.permission]).toEqual(['unknown', 'unknown', 'unknown']);
  });
});

describe('inputConstraints and inputError', () => {
  it('asks for the raw signal (research R-02)', () => {
    expect(inputConstraints('usb')).toEqual({
      audio: {
        deviceId: { exact: 'usb' },
        channelCount: { ideal: 2 },
        echoCancellation: false,
        noiseSuppression: false,
        autoGainControl: false,
        voiceIsolation: false,
        latency: { ideal: 0 },
      },
      video: false,
    });
    expect((inputConstraints(null).audio as Record<string, unknown>).deviceId).toBeUndefined();
  });

  it.each([
    ['NotAllowedError', 'permissionDenied:'],
    ['SecurityError', 'permissionDenied:'],
    ['NotFoundError', 'notFound:'],
    ['OverconstrainedError', 'notFound:'],
    ['NotReadableError', 'busy:'],
    ['AbortError', 'busy:'],
    ['NotSupportedError', 'unsupported:'],
    ['TypeError', 'unsupported:'],
  ])('%s -> %s', (name, code) => {
    expect(inputError(new DOMException('x', name)).message.startsWith(code)).toBe(true);
  });

  it('maps a non-error value to unsupported', () => {
    expect(inputError('boom').message).toMatch(/^unsupported: .*boom/);
  });
});

describe('InputPort', () => {
  it('lists audio inputs only; labels stay empty before permission', async () => {
    const { input, devices } = port();
    expect(await input.listDevices()).toEqual([
      { deviceId: 'usb', label: '', groupId: 'g-usb' },
      { deviceId: 'mic', label: '', groupId: 'g-mic' },
    ]);
    devices.granted = true;
    expect((await input.listDevices()).map((d) => d.label)).toEqual(['USB Interface', 'Laptop Mic']);
  });

  it('opens a stereo device: info, estimate, output-latency snapshot', async () => {
    const { input, ctx, devices } = port();
    ctx.baseLatency = 0.005;
    ctx.outputLatency = 0.02;
    const info = await input.open({ deviceId: 'usb', mode: 'stereo' });
    expect(info).toEqual({
      deviceId: 'usb',
      label: 'USB Interface',
      channelCount: 2,
      mode: 'stereo',
      deviceSampleRate: 48000,
      estimatedLatencySamples: Math.round((0.005 + 0.02 + 0.01) * 44100),
      outputLatencySamples: Math.round((0.005 + 0.02) * 44100),
    });
    expect(devices.requests[0]).toEqual(inputConstraints('usb'));
    expect([input.isOpen, input.channels]).toEqual([true, 2]);
  });

  it('routes mono-1 / mono-2 through a splitter output and stereo directly', async () => {
    const mono2 = port();
    await mono2.input.open({ deviceId: 'usb', mode: 'mono-2' });
    const [splitter] = mono2.ctx.splitters;
    expect(mono2.ctx.mediaSources[0]?.connections).toContain(splitter);
    expect(splitter?.routes.map((r) => [r.node, r.output])).toEqual([[mono2.input.node, 1]]);
    expect(mono2.input.channels).toBe(1);

    const stereo = port();
    await stereo.input.open({ deviceId: 'usb', mode: 'stereo' });
    expect(stereo.ctx.splitters).toHaveLength(0);
    expect(stereo.ctx.mediaSources[0]?.connections).toContain(stereo.input.node);
  });

  it('feeds the input meter analyser from the selected channels', async () => {
    const { input } = port();
    await input.open({ deviceId: 'mic', mode: 'mono-1' });
    const analyser = input.analyser as unknown as MockAnalyserNode;
    expect((input.node as unknown as { connections: unknown[] }).connections).toContain(analyser);
  });

  it('refuses mono-2 and stereo on a one-channel device (modeUnavailable) and releases it', async () => {
    const { input, devices } = port();
    await expect(input.open({ deviceId: 'mic', mode: 'stereo' })).rejects.toThrow(/^modeUnavailable: /);
    expect(devices.track?.stopped).toBe(true);
    await expect(input.open({ deviceId: 'mic', mode: 'mono-2' })).rejects.toThrow(/^modeUnavailable: /);
    expect(input.isOpen).toBe(false);
  });

  it('maps getUserMedia failures', async () => {
    const devices = new FakeMediaDevices();
    devices.failWith = 'NotReadableError';
    await expect(port(devices).input.open({ deviceId: null, mode: 'mono-1' })).rejects.toThrow(/^busy: /);
  });

  it('a rate mismatch (NotSupportedError in createMediaStreamSource) is unsupported and remembered', async () => {
    const { input, ctx, devices } = port();
    ctx.sourceError = 'NotSupportedError';
    await expect(input.open({ deviceId: 'usb', mode: 'stereo' })).rejects.toThrow(/^unsupported: .*44\.1 kHz/);
    expect([input.rateMismatch, devices.track?.stopped]).toEqual([true, true]);
  });

  it('rejects without mediaDevices', async () => {
    const ctx = new RecordingContext();
    const input = new InputPort({ context: ctx.asAudioContext(), env: fakeEnvironment(null), onLost: vi.fn(), onDevicesChanged: vi.fn() });
    await expect(input.open({ deviceId: null, mode: 'mono-1' })).rejects.toThrow(/^unsupported: /);
    await expect(input.listDevices()).resolves.toEqual([]);
    input.dispose();
  });

  it('replaces an open input: the old tracks stop and its nodes disconnect', async () => {
    const { input, devices, ctx } = port();
    await input.open({ deviceId: 'usb', mode: 'stereo' });
    const first = devices.track;
    await input.open({ deviceId: 'mic', mode: 'mono-1' });
    expect([first?.stopped, ctx.mediaSources[0]?.disconnected, input.info?.deviceId]).toEqual([true, true, 'mic']);
  });

  it('close stops every track and disconnects; a second close is a no-op', async () => {
    const { input, devices } = port();
    await input.open({ deviceId: 'usb', mode: 'stereo' });
    input.close();
    input.close();
    expect([devices.track?.stopped, input.isOpen, input.node, input.analyser, input.info]).toEqual([true, false, null, undefined, null]);
  });

  it('a track that ends reports OnInputLost once and closes the input', async () => {
    const { input, devices, lost } = port();
    await input.open({ deviceId: 'usb', mode: 'stereo' });
    devices.track?.end();
    devices.track?.end();
    expect([lost, input.isOpen]).toEqual([['ended'], false]);
  });

  it('forwards devicechange until disposed', async () => {
    const { input, devices, changed } = port();
    devices.dispatchEvent(new Event('devicechange'));
    input.dispose();
    devices.dispatchEvent(new Event('devicechange'));
    expect(changed).toHaveLength(1);
    await Promise.resolve();
  });

  it('rejects a stream without audio tracks (notFound) and other source errors (unsupported)', async () => {
    const empty = port(new FakeMediaDevices(), () => new FakeStream([]) as unknown as MediaStream);
    await expect(empty.input.open({ deviceId: null, mode: 'mono-1' })).rejects.toThrow(/^notFound: /);
    const odd = port();
    odd.ctx.sourceError = 'InvalidStateError';
    await expect(odd.input.open({ deviceId: 'usb', mode: 'stereo' })).rejects.toThrow(/^unsupported: /);
    expect(odd.input.rateMismatch).toBe(false);
  });

  it('uses the loopback stream instead of a microphone (E2E, research R-13)', async () => {
    const ctx = new RecordingContext();
    const destination = ctx.createMediaStreamDestination();
    const { input, devices } = port(new FakeMediaDevices(), () => destination.stream as unknown as MediaStream);
    const info = await input.open({ deviceId: null, mode: 'stereo' });
    expect([devices.requests.length, info.label, info.channelCount, info.deviceId]).toEqual([0, 'loopback', 2, 'loopback']);
  });
});
