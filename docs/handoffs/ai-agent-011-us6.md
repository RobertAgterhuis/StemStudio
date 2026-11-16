# Hand-off: ai-agent / 011 US6 (T075, T076, T077, T077b) + security fixes

Branch `011-lane-agent`. No LLM called; no key anywhere. Builds on `ai-agent-011-us5.md`.

## What was built

Tool providers for 004-010, all matching tool catalogue 1.0.0 (names, kinds, risks, undo kinds, groups, availability),
registered in `AddStemStudioAgent()` (registry cap of 32 per page profile holds with all 57 tools):

- **004 `HelpToolProvider`**: `search_help` (UI; at most 5 topics, summary <= 300), `open_help` (topic id pattern),
  `navigate` (page enum).
- **005 `ArtistToolProvider`**: `get_artist_info` - name, country, genres, members and album only inside `untrusted_text`
  (always taints), Wikipedia summary <= 500 chars omitted for Claude, fixed reason text per status (provider text only in
  `reasonDetail.untrusted_text`); `refresh_artwork`.
- **006 `LyricsToolProvider`**: `get_lyrics_status` (never text; fixed reason per status, the backend's problem text is
  dropped), `fetch_lyrics` (job, phase lyrics, undo-only `remove_lyrics`), `align_lyrics` (heavy, estimate), `show_karaoke`
  (UI, inverse from `{previous}`).
- **007 `ExportToolProvider`**: `export_audio` (raw/mix from the open mixer or `mixId`, format/bit depth/bitrate, range),
  `export_stem_pack`, `list_exports` (names untrusted), `delete_export`. Exports are `job` but **escalate to heavy** when
  the estimated output exceeds 500 MB (`AgentToolDescriptor.HeavyWhen`, evaluated before the policy; size from duration,
  format and file count). Undo: fresh `delete_export` per created asset.
- **008 `ScoreToolProvider`**: `transcribe_stem` (heavy; estimate from `GET /api/scores/options`, suggested profile, piano
  quality), `list_scores`, `delete_score`.
- **009 `StyleToolProvider`**: the 10 tools; `styleId`s only; `create_style` free text through the artist-imitation
  guardrail; user style names untrusted, preset names trusted; `get_generation_status` with fixed reason texts and a safe
  device name; restyle/generation results with `jobRef{phase restyle|generation}`; undo: `discard_generation` (undo-only:
  cancel while active, then delete), `retry_generation` -> `cancel_generation`, `import_take` -> `delete_track`,
  `create_style` -> fresh `delete_style`.
- **010 `SongToolProvider`**: `list_lyrics_sheets` (titles untrusted, never text), `save_lyrics_sheet` (`lyrics-not-from-user`
  guardrail; undo-only fresh `delete_lyrics_sheet`), `generate_song` (at most one of lyricsId/lyricsText and of
  styleId/freeCaption; instrumental without lyrics is valid; imitation guardrail on freeCaption/delivery/title),
  `list_voice_profiles` (display name untrusted; no person name, sample or URL), `convert_take_voice` (heavy; the card names
  the profile and the take). No profile creation, deletion or consent tool.
- The imitation guardrail now also receives the library's artist names (`ListTracksAsync`) for tools with free style text.
- Known API problems become fixed readable messages: `imitation` -> the refusal, `consent-required`/`consent-outdated` ->
  "Renew the consent for this voice on the Voices page", `preset-read-only`, `generation-not-retryable`.

## Security fixes (coordinator findings)

1. **Port free text in trusted fields** (red 5f27171, green e8978a4; API errors red fb33753, green ad29d20): band-info and lyrics
   reasons are fixed messages per status; lyrics problem text is dropped; artist country moved into `untrusted_text`;
   device name/reason, track stage, stem names and generation device names restricted to safe codes/patterns
   (`Tools/TrustedText`); **API error messages** (`AgentActionException` with an HTTP status) now reach the model only as
   `untrusted_text` under a fixed trusted text ("StemStudio could not do this step (HTTP n)." / "The item does not exist
   (HTTP 404)."), the panel keeps the original text; errors the agent raises itself (status 0) stay trusted. Sweep result:
   the remaining trusted strings from ports are enums, numbers, ids, preset style names and help topics (own content).
2. **`HeavyWhen` fail-open** (red daa7d8e, green 8373074): a throwing size check now escalates the step to heavy
   (approval, never covered by a plan grant), the cause is only logged (`RunLog.RiskCheckFailed`); cancellation propagates.
   The card carries reason `risk-heavy` (the contract enum has no `risk-check-failed`; see requests).
3. **Voice consent defense in depth** (red d5b8c58, green 4f2fea90): new descriptor hook `Precondition` runs before the
   policy; `convert_take_voice` looks the profile up via `IVoiceActions` and denies the step (no card, no job) with a fixed
   message when it does not exist or its consent is not current; the estimator and the handler check again. Messages
   carry no personal data; the backend's 409 `consent-*` is still mapped to the failed step "Renew the consent ...".

## Red -> green commits

| Pair | Red | Green |
|---|---|---|
| T075 | 25a308a | dd60ecf |
| T076 | b78f23c | ec993c0 |
| trusted fields | 5f27171 | e8978a4 |
| HeavyWhen fail-closed | daa7d8e | 8373074 |
| API error messages | fb33753 | ad29d20 |
| T077 | 3116598 | 740d4f6 |
| T077b | 779c296 | 06e5e20 |
| voice consent precondition | d5b8c58 | 4f2fea90 |

Plus edge-case tests (branch coverage) and `Runtime/InjectionSourcesTests` (every external-text source of 005-010 taints the
run and never leaks lyrics).

## Tests and coverage

`dotnet test tests/StemStudio.Agent.Tests -p:CollectCoverage=true`: **586 passed**, 0 failed.
`StemStudio.Agent`: **line 98.54 %, branch 93.52 %, method 98.11 %** (gate 90/90 passed).

## Quality checks

Build 0 warnings; `dotnet format --verify-no-changes` clean. `sig_check src/StemStudio.Agent`: 127 files, 478 units,
**0 errors, 52 warnings** (T079b reduces them).

## Dependencies added

None.

## Docs impact

User manual: the agent can now use help, band info, lyrics, exports, sheet music, styles, restyles, songs and own-voice
conversions; exports above about 500 MB, transcriptions, alignments, restyles, songs and voice conversions ask for
confirmation; the agent refuses styles or songs that imitate an artist and lyrics the user did not type.

## Open issues / requests

1. **architect**: add `risk-check-failed` to `AgentDecisionReasons` and the `decisionReasons` enum (agent-api / backend-api
   1.10.x) if the card should say why an export's size could not be checked; until then the card shows `risk-heavy`.
2. **frontend** (repeat from US5, blocking for save/update/export-mix): `mixer_get_state` must include `settings`.
3. **qa**: `us6-restyle` heavy card carries `device: gpu` and `estimatedMinutes`; `us6-imitation`: `generate_song` with an
   artist in `freeCaption` is `denied` before any backend call, the next scripted turn still plays.
4. Eval cases 29-30 and S1-S3 are covered by provider/policy tests here; the scripted eval runs follow with T079a.

## Next owner

ai-agent continues with T079a (eval harness) and T079b (polish).
