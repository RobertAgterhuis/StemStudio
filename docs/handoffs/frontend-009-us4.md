# Hand-off: frontend / 009 US4 (import a take as a track)

Tasks **009:T065 -> T066** (ticked). Commits: red 63085f5 -> green a253baa.

## What was built

- `take-import-<generationId>-<index>`: "Import as track" button (busy while importing) that calls
  `POST /api/generations/{id}/takes/{n}/import` and then **opens the new track page** (`/tracks/<trackId>`); once
  `importedTrackId` is set the same id is a link "Open the imported track". Failures go to `generation-action-error`.
- Track page: `TrackOrigin` under the title: `track-origin-badge` ("AI-generated", marker `generation-ai-label`) for
  tracks with `origin.aiGenerated`, and `track-origin-source-link` ("Open the source song", `tracks/<sourceTrackId>`)
  while `origin.sourceTrackId` is set.
- Track list: `track-ai-badge-<trackId>` ("AI") next to the title of generated tracks.
- Library: items with `provenance.origin = generation` show an "AI-generated" badge next to the name (no new test id).
- `AiBadge.TestId` is optional now.

## Tests and coverage

```text
dotnet test tests/StemStudio.Web.Tests -p:CollectCoverage=true
Passed! - Failed: 0, Passed: 1331, Total: 1331
| StemStudio.Web | 99.06% line | 93.49% branch |
```

## Quality checks

Build 0 warnings; format clean; `sig_check`: 0 errors.

## Dependencies added

None.

## Open issues / requests

- qa (`generative.spec.ts` "import a take"): the click navigates to the imported track; the spec's next steps poll the
  API and navigate themselves, which is compatible.

## Next owner

frontend continues with 009 US5 (T072/T073).
