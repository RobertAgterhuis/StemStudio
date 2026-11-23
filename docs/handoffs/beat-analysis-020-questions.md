# beat-analysis 020: questions (lane rule 5)

Lane BEAT-ANALYSIS, 2026-11-23. Done so far: T014 (red `f7b6a2cc`) and T015 (green `a231addb`), the
representation (data-model 4.1, 98 % line / 95 % branch). Before T030-T035 I ran the binding measures of
data-model 4.2-4.4 on the V1 synthetic grid (R-04): six family generators in
`worker/tests/insights/synthetic.py`, 30 s, equal RMS, exact injections. The scripts and logs are in
`.claude/scratchpad/020-round/beat/exp/` (not committed). Two of the binding measures do not meet
SC-002 on V1. I need a decision before I build them.

## Q1: B2 bleed (data-model 4.2/4.3) misses SC-002 on V1

How I measured: every ordered family pair i <- j, alpha in {0, 0.03, 0.1, 0.3}, index = max_j s_ij
(as specified). Grid levels: 0 and 0.03 are Low, 0.1 is Medium, 0.3 is High. I took the best T1/T2 found
by search, so these numbers are the best case for each variant.

| Variant | Arrangement | Pooled Spearman | Levels correct | Control bass<-kick |
|---|---|---|---|---|
| 4.2 as written (uncentred g, quiet = L_i <= L_j - 15 dB; an empty quiet set falls back to g) | all six play all the time, on one beat grid | 0.38 | 60 % | g/g_quiet 2.07 (only just capped) |
| same | instruments with rests (independent 2-s sections, 60 % on) | **0.81** | **78 %** | 2.07 |
| same, margin swept over 8/10/12/20 dB | with rests | 0.68-0.76 | 76-82 % | not both targets at any margin |
| **proposed: ratio-floor guard** g_floor_b = 5th percentile of P_i/P_j over j's active cells within 20 dB of j's band peak; g* = min(g, g_floor); co-occurrence when g/g_floor > 2 | with rests | **0.95** | **100 %** | 6.1 (capped) |
| proposed | all play all the time | 0.56 | 69 % | 6.1 |

Why 4.2 fails: g_quiet uses only cells where P_i/P_j <= 10^-1.5, so its value can never exceed 0.0316
(a selection bias). With alpha = 0.3 the quiet set is then empty, and the fallback to g brings back the
false shares of co-rhythmic instruments: drums<-vocals 0.23 and guitar<-bass 0.44 at alpha = 0. The
proposed floor is a test of the same idea ("bleed is a constant-ratio copy"), and it costs no more.

Two side findings:

- The index is an energy share, so it grows with alpha² ("linear in the injected amount" holds in energy,
  not in amplitude). The calibrated thresholds land near T1 about 0.003 and T2 about 0.02-0.06, far below
  the initial guesses of 0.10/0.30.
- No variant reaches SC-002 when all six instruments play all the time on one grid. Instruments that
  play together and sustain (guitar over bass, pad over bass) look like a constant low ratio, which is
  the method's limit without references. G1 has to measure this on real music.

**Decision needed**:

- (a) Keep 4.2 literally and accept that V1 misses SC-002 (about 78 % levels).
- (b) Approve the ratio-floor guard as a refinement of 4.2 (architect to update data-model 4.2/4.3 at
  T108).

For either option, also approve the V1 arrangement "instruments with rests" as the V1 song. The
"playing together" control stays as specified.

## Q2: A1 flicker (R-03, data-model 4.4) does not respond to the V1 musical-noise injection

Ratios stem/mix with the TF-mask injection p = 0, 0.05, 0.15, 0.3 (1-8 kHz):

| A1 variant | vocals | drums | bass | guitar | piano | other |
|---|---|---|---|---|---|---|
| as written, on the 64 mel bands (4.1) | 0.29 -> 0.30 | 0.88 -> 0.89 | 0.13 -> 0.13 | 0.17 -> 0.18 | 0.23 -> 0.24 | 0.03 -> 0.03 |
| as written, on linear STFT bins | 0.40 -> 0.40 | 1.32 -> 1.33 | 2.96 -> 3.23 | 0.42 -> 0.88 | 0.41 -> 0.71 | 0.08 -> 0.10 |
| dip detector (cell > 6 dB below both time neighbours), STFT bins | 1.09 -> 1.07 | 0.84 -> 0.82 | 0.06 -> 0.13 | 0.35 -> 1.11 | 0.15 -> 1.03 | 0.13 -> 0.17 |
| injection at 50 % overlap instead of 75 %, A1 on STFT bins | flat | flat | falls | 0.42 -> 1.18 | 0.41 -> 0.75 | 0.08 -> 0.20 |

Why: the mel bands average the dropped bins away. The cells 30-60 dB below the peak are the noise floor
and leakage, not the masked harmonics. Overlap-add smears the dropped cells. The clean ratio also varies
a lot by family (0.03 for the pad, 1.3-3 for drums and bass), so "about 1 on clean stems" does not hold.

A2 dropouts works: with gating r = 0, 0.5 and 2 the ratio goes 0 -> about 26 -> about 110 for vocals,
bass, guitar, piano and other. One exception: the clean drums already reach about 110 at r = 0, from
natural decays of more than 30 dB within 0.3 s. Dividing by the mix does not remove these, because the
other instruments fill the mix's gaps.

**Decision needed** (my recommendation is (c), then (a)):

- (a) Build A1 as specified, report its V1 result honestly (it fails), and gate SC-002 for artefacts on
  A2 only until a better A1 exists.
- (b) Allow a timeboxed redesign of A1 (about 1 h; candidate: a temporal-modulation statistic per STFT
  bin), as a contract refinement.
- (c) Change the V1 musical-noise injection (for example tile-wise masks over 2-4 frames, closer to real
  mask errors) and re-test before choosing.
- In every case: for A2, may the drums be judged relative to their own onset envelope? Without that, a
  clean drums stem rates artefacts High (instrument-neutral: every family counts equally).

## Not blocked meanwhile

The fake analyzer (T036/T037, data-model 2.6), the reliability table (T038), the representation, the G1
script skeleton (T039) and the T103 timing harness do not depend on these answers.

## Answers (orchestrator, 2026-11-23)

- Q1 -> (b): use the ratio floor. Validation song with rests approved. Keep the dense song as a control
  that asserts no false bleed above medium. Thresholds come from V1.
- Q2 -> first (c), then (b) with a 1 h timebox; if it still fails, flicker is not shipped. Dropouts are
  judged against each stem's own envelope, for every family.

Outcome: (c) had no effect, and (b) also failed on family neutrality (details in
`beat-analysis-020-us1.md`), so flicker is not shipped. Dropouts now judge every stem against its own
envelope and pass V1 for every family.

## Q3: the dense control cannot meet both targets, and one refinement needs confirming

1. **Dense control vs V1.** On the dense song (every family plays all the time on one grid), sustained
   instruments that play together read as a constant-ratio copy. Guitar shows bass at 0.080 (seed 7),
   0.086 (seed 11) and 0.096 (seed 23); with seed 23 bass shows 0.126. At T2 = 0.053, which is best for
   V1 (87-93 % on seeds 7/11/23), the dense song has 1-2 false High stems out of 6. A T2 that clears the
   dense song on every seed (about 0.13) drops V1 to about 75 %, below SC-002. Adding `{guitar, bass}`
   as a fifth co-occurrence pair fixes seed 7 (84 %, no false High) but not seed 23.
   The test `test_dense_song_has_no_false_bleed_above_medium` is a **strict xfail** until you decide.
   Options:
   - (a) keep it as a documented limit of the method (G1 decides on real music);
   - (b) add `{guitar, bass}` to the pairs (data-model 4.3), still not robust;
   - (c) prefer the dense control and accept that V1 misses SC-002.
   My recommendation: (a).
2. **Gain cap `g* <= 1` (`thresholds.MAX_GAIN`), please confirm.** Without it, a quiet phantom stem made
   the stem it was copied from look bled. For example, a piano stem that is 0.06 x the guitar made the
   guitar read "much piano" with index 1.0. A stem cannot hold more of a source than the source's own
   stem has. With the cap, V1 improved from 86 % to 93 % (rho 0.91 -> 0.94). It is one constant, and
   reverting it is trivial.
