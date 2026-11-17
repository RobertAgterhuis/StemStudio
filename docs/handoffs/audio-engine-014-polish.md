# Hand-off: audio-engine, 014 HO:polish

Lane L1, 2026-11-17. The 014 engine work is complete: T042-T043 (`OnLoopPass`, `audio-engine-014-foundational.md`)
and T080-T081 (score follower, `audio-engine-014-us4.md`). Since HO:us4 the only 014-relevant change is the shared
`FrameSubscription`, used by karaoke and the follower.

Contract text to clarify at integration (approved 2026-11-17): in "Loop passes", "a seek or loop change" means a
seek, or a change of the loop region (start, end or enabled). Re-sending an identical region does not reset the
pass count.

The final suite and coverage numbers (shared with 015) are in `audio-engine-015-polish.md`: 790 tests, 99.80 %
lines, 97.99 % branches. `src/score-follow/**` imports no Blazor interop module (014 T015 rule). No new dependency
and no deviation.
