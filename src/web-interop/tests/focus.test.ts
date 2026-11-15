import { afterEach, describe, expect, it } from 'vitest';
import { captureFocus, focusedHelpId } from '../src/focus';
import { must } from './media';

afterEach(() => {
  document.body.innerHTML = '';
});

describe('focusedHelpId', () => {
  it('returns the marker of the focused element or its closest ancestor', () => {
    document.body.innerHTML = '<div data-help-id="mixer-pan"><input id="pan" /></div><button id="plain">x</button>';
    must(document.getElementById('pan')).focus();
    expect(focusedHelpId()).toBe('mixer-pan');
  });

  it('returns null without a marker', () => {
    document.body.innerHTML = '<button id="plain">x</button>';
    must(document.getElementById('plain')).focus();
    expect(focusedHelpId()).toBeNull();
  });

  it('returns null when nothing is focused', () => {
    (document.activeElement as HTMLElement | null)?.blur();
    expect(focusedHelpId()).toBeNull();
  });
});

describe('captureFocus', () => {
  it('restores focus to the captured element on dispose (once)', () => {
    document.body.innerHTML = '<button id="a">a</button><button id="b">b</button>';
    must(document.getElementById('a')).focus();
    const handle = captureFocus();
    must(document.getElementById('b')).focus();
    handle.dispose();
    expect(document.activeElement?.id).toBe('a');
    must(document.getElementById('b')).focus();
    handle.dispose();
    expect(document.activeElement?.id).toBe('b');
  });

  it('does nothing when the element is gone', () => {
    document.body.innerHTML = '<button id="a">a</button><button id="b">b</button>';
    must(document.getElementById('a')).focus();
    const handle = captureFocus();
    must(document.getElementById('b')).focus();
    must(document.getElementById('a')).remove();
    handle.dispose();
    expect(document.activeElement?.id).toBe('b');
  });
});
