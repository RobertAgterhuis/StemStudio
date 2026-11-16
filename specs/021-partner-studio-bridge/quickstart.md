# Quickstart: Practice Partner and Studio Bridge (feature 021)

Validation scenarios for the integration wave. Q1-Q6 run on any machine with the fake stack (CI
equivalent); Q7-Q11 need the reference machine (RTX 5070 Ti, audio interface, a DAW) and are run by
the CTO with the owner's gear.

## Prerequisites

- 009/010 and 012-020 merged; `scripts/test-all.*` green.
- Fake stack: `docker compose -f docker-compose.yml -f docker-compose.generative-fake.yml -f docker-compose.e2e.yml up -d`
  (fake generator, fake catalogue host, fake stems; E2E browser flags for the fake audio input).
- Reference stack: `docker compose -f docker-compose.yml -f docker-compose.gpu.yml -f docker-compose.generative.yml up -d`.

## Q1 Partner on the fake stack (US1, SC-007)

1. Import the fixture track (fake separation), open the mixer.
2. "Practice partner" -> family Bass -> Add a part -> Generate.
3. Expect: job card goes `queued -> generating -> completed`; take 1 shows verdict and
   "offset -37 ms corrected" (fake offset); AI badge.
4. "Add to mixer" -> strip `part-xxxxxxxx` "Bass (AI)" appears, plays in sync (meter moves with the
   stems), save the mix, reload: the strip and its volume come back.
5. Style hint "in the style of <any deny-listed artist>" -> refused with the imitation reason.
Automated: `e2e/tests/partner.spec.ts`.

## Q2 Alternative part and every family (US1, MoSCoW principle)

1. For each family of the acceptance set (drums, bass, guitar, keys, strings, backing vocals):
   generate with the fake generator (mode alternative where a stem exists) and add.
2. Expect: the replaced stem is muted in the open mix; each strip has the right default name.
Automated: parameterised case in `partner.spec.ts` (one per family group).

## Q3 Amp with the fake input (US2)

1. Open the input panel (015) with Chromium's fake device (guitar DI fixture), switch Amp on,
   choose "Guitar crunch" (built-in).
2. Expect: input and output meters move; the page probe (test hook) reports a spectral difference
   between dry and wet (> 3 dB more energy above 2 kHz); `amp-latency` shows a number; bypass
   returns the dry signal.
3. Choose the fake catalogue capture (installed in Q4) as amp: state `running`, no `missing`.
Automated: `e2e/tests/amp.spec.ts`.

## Q4 Profiles and cabinets (US4)

1. Amp profiles page -> install the fake catalogue entry: licence and attribution shown; SHA-256
   verified (tamper the fixture -> `checksum-mismatch`).
2. Upload `e2e/fixtures/amp/linear-test.nam` -> accepted; upload `not-a-model.nam` -> `invalid-nam`;
   upload an IR WAV of 1 s -> accepted; a 3 s IR -> refused.
3. Delete the capture used by a saved rig -> the rig shows the fallback note.

## Q5 Dry + wet take and re-amp (US5)

1. Amp on, record a 5 s take (015 flow).
2. Expect: the take row has the wet badge; dry and wet files have the same length
   (`GET .../takes/{id}` and `.../wet`); playback uses wet; re-amp with another preset plays the
   dry file through the new rig; "Print" replaces the wet file.

## Q6 DAW export on the fake stack (US3, SC-005 automated part, SC-006)

1. Track with a saved mix (bass -6 dB, pan guitar -0.5, mute other, EQ + reverb on vocals), 3
   sections, an arrangement "chorus x2", one part and one wet take -> Export to DAW (defaults +
   arrangement).
2. Expect: library item kind "DAW project" ready; download `*.dawproject`; `unzip -l` shows
   `project.xml`, `metadata.xml`, `audio/01-vocals.wav` ...; the backend test tool
   `dotnet run --project tools/dawproject-check -- <file>` (qa helper, T086) validates against the XSD
   and prints tracks/clips/markers/tempo points; the report lists reverb as not mapped and the
   amp take as "wet + muted dry".
3. Bit identity: `audio/02-drums.wav` equals the 007 raw WAV 24-bit export of `drums` (SHA-256).

## Q7 Partner on the GPU (SC-001, SC-002, SC-003)

1. On 3 reference songs (owner's choice, own imports), generate one take per family of the
   acceptance set for a 60 s range.
2. Record time per job (warm), fit verdicts, and the owner's "usable for practice" rating.
3. Pass: warm 60 s <= 3 min; >= 80 % fit checks pass; >= 4 of 6 parts usable.

## Q8 Amp latency and load (SC-004)

1. Audio interface on the reference machine, 015 calibration done.
2. Load a standard WaveNet capture and a lite capture; open the engine benchmark page
   (`/dev/engine-bench`, development builds only) and read `process()` p50/p99.
3. Pass: standard <= 50 % of the quantum, chain latency <= 2 ms; record the round trip shown.

## Q9 DAW import (SC-005 owner part, S-05)

1. Open the Q6 export (made on the reference stack with real stems) in Bitwig Studio and/or
   Studio One.
2. Check and note: tracks and names, audio aligned to bars (grid lines on the downbeats), markers,
   tempo map, arrangement clips, EQ/compressor devices, mute/solo, AI comment.
3. Record the result in the help topic `daw-compatibility` and in ADR-034 Phase 0 outcome.

## Q10 CPU-only machine (SC-008)

1. Default stack without the generator: partner button disabled with "not installed" reason;
   amp and DAW export work; all existing tests pass.

## Q11 Levels and help (US6)

1. Switch Beginner / Advanced / Expert; the controls match data-model.md 9; every new help marker
   opens its topic (`help.spec.ts` covers the ids automatically).
