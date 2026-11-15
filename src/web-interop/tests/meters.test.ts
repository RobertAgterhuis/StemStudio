import { afterEach, describe, expect, it } from 'vitest';
import { formatPeakDb, startMeterReadouts } from '../src/meters';
import type { Disposable } from '../src/types';
import { must } from './media';

let handle: Disposable | undefined;

afterEach(() => {
  handle?.dispose();
  handle = undefined;
  document.body.innerHTML = '';
});

function flush(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

describe('formatPeakDb', () => {
  it.each([
    ['-12.34', '-12.3'],
    ['0', '0.0'],
    ['-60', '-inf'],
    ['-75', '-inf'],
    ['nonsense', '-inf'],
    [null, '-inf'],
  ])('%s -> %s', (value, text) => {
    expect(formatPeakDb(value)).toBe(text);
  });
});

describe('startMeterReadouts (Expert dB text, ui-test-ids meter-db-<key>)', () => {
  it('mirrors data-peak-db of a meter into its readout', async () => {
    document.body.innerHTML = '<div data-testid="meter-vocals"></div><span data-testid="meter-db-vocals">-inf</span>';
    handle = startMeterReadouts(document.body);
    must(document.querySelector('[data-testid="meter-vocals"]')).setAttribute('data-peak-db', '-6.25');
    await flush();
    expect(must(document.querySelector('[data-testid="meter-db-vocals"]')).textContent).toBe('-6.3');
  });

  it('ignores meters without a readout and other attributes', async () => {
    document.body.innerHTML = '<div data-testid="meter-drums"></div><div id="x"></div>';
    handle = startMeterReadouts(document.body);
    must(document.querySelector('[data-testid="meter-drums"]')).setAttribute('data-peak-db', '-3');
    must(document.getElementById('x')).setAttribute('data-peak-db', '-3');
    await flush();
    expect(document.body.textContent).toBe('');
  });

  it('stops after dispose', async () => {
    document.body.innerHTML = '<div data-testid="meter-bass"></div><span data-testid="meter-db-bass">-inf</span>';
    handle = startMeterReadouts(document.body);
    handle.dispose();
    must(document.querySelector('[data-testid="meter-bass"]')).setAttribute('data-peak-db', '-1');
    await flush();
    expect(must(document.querySelector('[data-testid="meter-db-bass"]')).textContent).toBe('-inf');
  });
});
