# Hand-off: frontend / 010 US3 (sing it with my own voice)

Tasks **010:T050 -> T051** (ticked). Commits: red af1c94e -> green 0c3d6d7.

## What was built

- **Recorder (web-interop, Vitest)** `src/web-interop/src/recorder.ts`, exported from `web-interop.js`:
  `recorderUnavailableReason()` (secure context, MediaRecorder, getUserMedia), `startRecording(maxSeconds)` ->
  session `{ stop(): Uint8Array, mimeType(), previewUrl(), dispose() }` (WebM/Opus when supported, echo cancellation /
  noise suppression / auto gain off, stops by itself at the limit, microphone released on stop), `fileUrl(input)`,
  `revokeUrl(url)`; readable errors for denied permission / no microphone.
- **.NET port** `Interop/IVoiceRecorder` + `VoiceRecorderInterop` (registered in `Program.cs`): the recording is read
  with an `IJSStreamReference` (up to 20 MiB, no SignalR message limit); JS errors become `VoiceRecorderException`
  with the first line of the message.
- **Typed client**: `ListVoiceProfilesAsync`, `GetVoiceConsentAsync`, `CreateVoiceProfileAsync(VoiceProfileUpload)`
  (multipart `sample`, `displayName`, `personName`, `consentConfirmed`, `consentStatementVersion`, `source`),
  `DeleteVoiceProfileAsync(id, deleteTakes)`, `CreateVoiceConversionAsync`.
- **`/voices`** (`Components/Voices/VoiceProfilesPage`, banner with `data-voice-state` and the singer's reason
  `generation-voice-reason`): `voice-row-<id>` (name, source, length), `voice-play-<id>` (`<audio>` of the sample),
  `voice-consent-<id>` ("Statement consent.v1, agreed by <name> on 14 Nov 2026"), `voice-delete-<id>` -> confirm with
  the checkbox `voice-delete-takes-<id>` ("Also delete the N takes converted to this voice", unticked by default) and
  `voice-delete-confirm`; `voices-empty`, `voices-error`, `voices-action-error`.
- **New voice profile** (`voice-new` -> `NewVoicePanel`):
  - `VoiceRecorder`: `voice-record-start`, `voice-record-stop`, `voice-record-timer` (`m:ss`, `role="timer"`, on the
    `TimeProvider`, polled about once per second), auto-stop at 60 s, `voice-record-status`,
    `voice-record-unavailable` (reason + "You can upload a recording instead."; recording disabled, upload still works).
  - `voice-upload` (accept `.wav,.flac,.mp3,.webm,audio/*`; type and 20 MiB checked before upload), `voice-preview`
    (`<audio controls>` of the recording or the chosen file).
  - `ConsentPanel`: `voice-consent-text` (`data-version`, both texts verbatim in `<p lang="en">` / `<p lang="nl">`,
    element id `voice-consent-statement`), `voice-consent-checkbox` (`aria-describedby` = the statement),
    `voice-person-name` (80), `voice-display-name` (60).
  - `voice-save` enabled only with a sample, the loaded statement, the ticked checkbox and both names; disabled with
    `aria-describedby="generation-voice-reason"` while the singer is **not installed** (sample checks run on the CPU,
    so `unavailable` still allows saving). `voice-error` shows the API reason verbatim (too short/long, unvoiced,
    library audio, name taken, consent); a missing statement blocks the profile.
- **Use my voice**: `take-use-my-voice-<gen>-<n>` only on song takes with `vocal: synthetic`; `UseMyVoiceDialog`
  (`data-help-id="voice-conversion"`): `take-voice-profile-select` (native select; a link to `/voices` with the same id
  when there is no profile), Advanced (`voice.pitch`): `take-voice-pitch-shift` (-12..12), `take-voice-auto-shift`
  (checked); `take-voice-convert-start` (disabled with `take-voice-unavailable` = the singer's reason when not ready),
  `take-voice-convert-cancel`, `take-voice-error`; the dialog closes after start and the conversion appears at the top
  (state "Preparing the stems" while `waitingForStems`).
- Conversion takes: `take-vocal-badge-*` "AI voice: <profile>", `take-vocal-play-<gen>-<n>` (`<audio>` of
  `vocalAudioUrl`), details with the profile and the pitch.
- Help topics `voice-profiles` (screen topic of `/voices`), `voice-consent`, `voice-conversion`; nav `nav-voices`.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1429, Total: 1429
| StemStudio.Web | 99.11% line | 93.58% branch |

src/web-interop: npx vitest run --coverage
Tests 254 passed; All files 99.69 % statements, 97.95 % branches, 100 % functions, 99.68 % lines (recorder.ts 100 %)
eslint . clean; tsc --noEmit clean; npm run build ok
```

## Quality checks

Build 0 warnings; format clean; `run_all.py`: 1670 files, 0 errors.

## Dependencies added

None (MediaRecorder is a browser API; no npm or NuGet package).

## Deviations

- plan.md/T051 name `wwwroot/js/voice-recorder.js`; the recorder is a module of `src/web-interop` (TypeScript, Vitest,
  ESLint) bundled into `wwwroot/js/web-interop.js` as instructed for this wave. The web-interop contract 1.0.0
  (004) is unchanged; the recorder functions are additive exports (architect may document them).
- Extra ids beyond ui-test-ids 1.9.0: `generation-voice-reason`, `voice-record-status`, `voice-cancel`,
  `voices-empty`, `voices-error`, `voices-action-error`, `take-voice-unavailable`, `take-voice-error`.

## Open issues / requests

- qa (`voice-recording.spec.ts`): with Chromium's fake microphone the recorder produces WebM/Opus; the timer text is
  `m:ss` and Stop shows `voice-preview` at once.
- qa: Firefox may record Ogg/Opus where WebM is not offered; the file is still sent as `recording.webm` with the
  browser's MIME type (the backend decodes with ffmpeg).

## Next owner

frontend continues with 010 US4 (T055).
