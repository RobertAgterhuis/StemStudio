# Hand-off: frontend (L5) / 016 HO:us6 (T113, T114)

Branch `016-round-lane-l5`. Not pushed, not merged. E2E T115 is lane L2's.

## Tasks

| Task | Red | Green | Result |
|---|---|---|---|
| T113/T114 menu | fa46b6ca | cbd86af7 | `PractiseAsMenu`: `practise-as` menu button (`aria-haspopup="menu"`, `aria-expanded`, `data-family`, text "Practise as: Guitar" once chosen), items `practise-as-<family>` (`role="menuitemradio"`, `aria-checked`) in the picker order of data-model 5.3 / spec US6 (vocals, guitar, bass, keys, drums, orchestral); APG keyboard: ArrowDown/Up on the button opens on the first/last item, arrows wrap, Home/End, Escape closes and returns the focus (roving `tabindex`) |
| T113/T114 apply | fa46b6ca | cbd86af7 | choosing a family: `FamilyMixService.MinusOne` (every channel of the family, expanded or not; others unmuted and unsoloed), `stemstudio.views.family` stored, `instrument-view` opens with `data-view` = singing / drum-trainer / tablature (guitar, bass) / chords (keys) / part (orchestral) |
| T113/T114 offer | fa46b6ca | cbd86af7 | `practise-as-offer` chip "Practise as Keys again?" from the stored family (unknown values ignored); offered, applied only on click (AS3) |
| T113/T114 views | fa46b6ca | cbd86af7 | one `instrument-view` region (`aria-label` = view name) hosts every view; keys -> `ChordSheet`; tab/part -> `FamilyView` with `ChannelScoreView` (the 014 score view preferring the family's profile, else "Generate tab"/"Generate part"); part view has `score-part-instrument` (orchestrator decision 2026-11-22), stored at `stemstudio.views.instrument` and reported to the mixer for US7's written key |
| refactor | - | cbd86af7 | `ChannelScoreView` shared by the drum trainer and the family views (drum ids unchanged); `ChannelScoreMaker` takes an optional form adjustment; `Mixer.OpenView` (internal) opens any view |
| help | fa46b6ca | cbd86af7 | topic `instrument-views` (anchor `practise-as`; every family, the preset fix of DELTA 6); catalogue `views.practise-as` (B) |

## Tests and coverage

| Component | Command | Result | Coverage |
|---|---|---|---|
| StemStudio.Web | `dotnet test tests/StemStudio.Web.Tests -c Release -p:CollectCoverage=true` | 3766/3766 | 99.08 % line, 92.59 % branch, 99.6 % method |

Build 0 warnings, format clean, `run_all.py` PASS (0 errors after fixing a parameter-count and a duplication error;
jscpd ran: 453 clones). Open warning: `ChannelScoreView.TryGenerateAsync` 16 lines (T135).
WCAG: menu button pattern with keyboard, names = visible text, the menu and views outside the dock.
Security: the stored family and instrument are parsed against the known lists (unknown values ignored).

## Decisions

1. Catalogue id `views.practise-as` (data-model 6's `practise-as` fails the catalogue id pattern; see HO:us1).
2. The family order is the one of `InstrumentFamilies.All` (spec US6 text), not the ui-test-ids table order.
3. "Practise as drums" applies the minus-one mix and opens the trainer, without the metronome/count-in of Drumless.
4. While the drum, tab or part view is open the mixer's own score panel is not rendered (one engine score follower).
5. "Generate tab"/"Generate part" buttons have no test ids (none in ui-test-ids 1.15.0).

## Deviations

None.

## Open items

Next: T120/T121 (US7 key), then US8 capo. The written key (`mixer-key-written`) uses the instrument the part view reports.
