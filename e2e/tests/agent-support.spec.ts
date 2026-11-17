import { scriptProblems } from '../support/agent-script-check';
import {
  agentScripts,
  contextLyricsId,
  contextMixId,
  contextTrackId,
  finalText,
  readAgentScript,
  scriptFilesOnDisk,
  scriptedTools,
  specTagLists,
  tagProblems,
} from '../support/agent-scripts';
import { expect, test } from '../support/test';

// Stack-free self-test of the feature 011 agent E2E support (TEST-STRATEGY 17.1, 17.4): the
// committed scripts are valid against agent-script.schema.json 1.0.0 and the tag helpers follow
// the convention of scripts/agent-e2e.{sh,ps1}. Runs in `npm test` without a stack.

test.describe('agent scripts', { tag: '@self-test' }, () => {
  test('the folder holds exactly the listed scripts', () => {
    expect(scriptFilesOnDisk()).toEqual([...agentScripts].sort());
  });

  test('every script is valid, uses only known placeholders and ends with an answer', () => {
    for (const name of agentScripts) {
      expect(scriptProblems(readAgentScript(name)), name).toEqual([]);
      expect(finalText(name).length, name).toBeGreaterThan(0);
    }
  });

  test('the US1 script plans, searches, opens the mixer and mutes the drums', () => {
    expect(scriptedTools('us1-command')).toEqual(['set_plan', 'search_library', 'open_mixer', 'mixer_set_channel']);
  });

  test('the 023 apply script lists the suggestions before it applies them', () => {
    expect(scriptedTools('us4-lyric-apply')).toEqual(['list_lyric_suggestions', 'apply_lyric_suggestions']);
  });
});

test.describe('agent script check', { tag: '@self-test' }, () => {
  test('accepts the schema example and the context placeholders', () => {
    const calls = [{ name: 'open_mixer', arguments: { trackId: contextTrackId, mixId: contextMixId } }];
    expect(scriptProblems({ version: 1, turns: [{ calls }, { text: 'Done.', usage: { inputTokens: 1 } }] })).toEqual([]);
  });

  test('accepts the 023 lyrics placeholders and refuses a result placeholder with an unknown category', () => {
    const ids = '{{result.list_lyric_suggestions.ids:spelling}}';
    const calls = [{ name: 'apply_lyric_suggestions', arguments: { lyricsId: contextLyricsId, suggestionIds: ids } }];
    expect(scriptProblems({ version: 1, turns: [{ calls }, { text: 'Done.' }] })).toEqual([]);
    const bad = [{ calls: [{ name: 'apply_lyric_suggestions', arguments: { suggestionIds: '{{result.list_lyric_suggestions.ids:colour}}' } }] }];
    expect(scriptProblems({ version: 1, turns: bad })).toEqual(['turns[0].calls[0].arguments.suggestionIds uses the unknown placeholder {{result.list_lyric_suggestions.ids:colour}}']);
  });

  test('refuses what the schema refuses', () => {
    expect(scriptProblems([])).toEqual(['the script is not a JSON object']);
    expect(scriptProblems({ version: 2, turns: [] })).toEqual(['version must be 1', 'turns must hold 1-50 turns']);
    expect(scriptProblems({ version: 1, turns: [{ say: 'x' }] })).toEqual(['turns[0] has the unknown property say']);
    expect(scriptProblems({ version: 1, turns: [{ calls: [{ name: '' }] }] })).toEqual(['turns[0].calls[0].name must be a string of 1-100']);
    expect(scriptProblems({ version: 1, turns: [{ usage: { inputTokens: -1 } }] })).toEqual(['turns[0].usage values must be integers >= 0']);
  });

  test('refuses unknown or embedded placeholders', () => {
    const turns = [{ calls: [{ name: 'get_track', arguments: { trackId: '{{last.trackId}}', ids: [`x ${contextTrackId}`] } }] }];
    expect(scriptProblems({ version: 1, turns })).toEqual([
      'turns[0].calls[0].arguments.trackId uses the unknown placeholder {{last.trackId}}',
      `turns[0].calls[0].arguments.ids[0] uses the unknown placeholder x ${contextTrackId}`,
    ]);
  });
});

test.describe('agent tags', { tag: '@self-test' }, () => {
  test('every agent test carries the agent tag and at most one script tag; the unavailable test has none', () => {
    const lists = specTagLists();
    expect(lists.length).toBeGreaterThan(0);
    expect(lists.flatMap(tagProblems)).toEqual([]);
  });

  test('every script is used by a test', () => {
    const used = new Set(specTagLists().flat());
    expect(agentScripts.filter((name) => !used.has(`@${name}`))).toEqual([]);
  });

  test('the tag check finds the convention breaks', () => {
    expect(tagProblems(['@agent', '@us1-command'])).toEqual([]);
    expect(tagProblems(['@us1-command'])).toEqual(['@us1-command: missing @agent']);
    expect(tagProblems(['@agent', '@agent-unavailable', '@us1-command'])).toEqual(['@agent @agent-unavailable @us1-command: @agent-unavailable with a script tag']);
  });
});
