# Hand-off: audio-engine / foundational

## What was built

T037: hand-written test doubles for the audio engine (no production code yet).

- `MockAudioContext` (settable `currentTime`, `resume`/`close`, `decodeAudioData` with scripted
  durations per byte length and a scripted failure, `createBuffer`), `MockAudioBufferSourceNode`
  (records every `start(when, offset)` and `stop()`, `loop/loopStart/loopEnd`, `end()` fires
  `onended`, double `start()` throws like the browser), `MockGainNode` / `MockStereoPannerNode`
  with `MockAudioParam` (records `setTargetAtTime`, `setValueAtTime`, `linearRampToValueAtTime`,
  `cancelScheduledValues`), `MockAnalyserNode` (scripted `samples` for
  `getFloatTimeDomainData`); every node records its outgoing connections, so tests can assert the
  graph.
- `FakeDotNetRef`: records `invokeMethodAsync(method, ...args)`.
- `FetchStub`: per-URL replies (status, body size, network error), default 404.

## Files changed

- `src/audio-engine/tests/mocks/{audio-context.ts,dotnet-ref.ts,fetch.ts,mocks.test.ts}`
- `specs/001-stemstudio-mvp/tasks.md` (T037 ticked)

## Tests and coverage

`npm test`: 2 files, 10 passed (8 mock self-tests + 2 skeleton tests).
`npm run test:coverage`: statements 100 % (2/2), branches 100 % (0/0), functions 100 % (1/1),
lines 100 % (2/2) — production code is still the skeleton; the mocks live under `tests/` and are
not part of the coverage scope.

Red-to-green: the mocks are test infrastructure; their self-tests were written together with them.

## Quality checks

`npm run lint` clean (complexity <= 5, max-params 4), `npm run typecheck` clean.

## Dependencies added

None.

## Open issues / requests

None.

## Next owner

audio-engine (US2: T082 onwards). The frontend can rely on the contract unchanged.
