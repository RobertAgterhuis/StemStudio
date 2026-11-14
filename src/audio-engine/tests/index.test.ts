import { describe, expect, it } from 'vitest';
import { ENGINE_CONTRACT_VERSION, createEngine, type DotNetObjectReference } from '../src/index';

const dotnet: DotNetObjectReference = {
  invokeMethodAsync: () => Promise.resolve(undefined),
};

describe('audio engine skeleton', () => {
  it('exposes the contract version', () => {
    expect(ENGINE_CONTRACT_VERSION).toBe('1.0.0');
  });

  it('createEngine is a placeholder until the audio-engine agent implements it', () => {
    expect(() => createEngine(dotnet)).toThrow('not implemented');
  });
});
