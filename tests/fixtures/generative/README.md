# Generative fixtures (features 009 and 010)

Owner: qa (009:T011, 010:T009). Hand-maintained tables, checked by
`tests/fixtures/tests/test_generative_fixtures.py`; read by the E2E specs through
`e2e/support/generative.ts`. Rules and case ids: `docs/testing/TEST-STRATEGY.md` sections 15-16.

| Path | What | Used by |
|---|---|---|
| `ethics-cases.json` | style texts the artist-imitation guard must refuse (`spec` cases quote the specs, `qa-proposal` cases are qa's expectation), genre names it must accept, phrases no preset may contain, the library-artist case (artist tag of the 005 fixture `cover_embedded_10s.mp3`, not a real artist), the FLAC tags every take carries, the consent statement `consent.v1` (equal to the 010 backend contract example) | backend `StyleTextGuard` tests (009:T024) may reuse the cases; E2E `styles.spec.ts`, `song.spec.ts`, `voice.spec.ts`, `support/generative-flow.ts` |
| `fake-generator.json` | what the E2E specs expect from the fake generator and fake singer per `STEMSTUDIO_FAKE_GENERATOR_MODE` (terminal states, error codes, readable message patterns, take lengths, vocal labels, file tags) | qa proposal for generative 009:T020/T021 and 010:T010/T011; E2E `generative.spec.ts`, `generation-status.spec.ts` |

Related fixtures elsewhere:

- `tests/fixtures/voice-manifest.json` + `generate_voice_fixtures.py`: the synthetic own-voice
  samples (`own-voice-20s.wav`, too short/long variants, noise, a conversion target), generated
  into the git-ignored `tests/fixtures/audio/`.
- `tests/fixtures/lyrics/sample-en.txt`, `sample-nl.txt`, `sample.lrc`, `own-lyrics-expected.json`:
  self-written lyrics for the song specs.

## Licence and provenance

Every text in this folder and in the lyrics samples is written by qa for the test suite and
dedicated to the public domain (CC0 1.0). No real artist, song or person is named or imitated;
the guard cases use invented descriptions ("a famous crooner", "a well-known singer"). The voice
samples are synthetic (formant synthesiser `tests/fixtures/sung_voice.py`): no recording of a
real person is used, so the consent rules of 010 are tested without anybody's voice.
