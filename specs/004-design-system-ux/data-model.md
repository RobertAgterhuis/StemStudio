# Data Model: Design System and UX Foundation

Builds on [003 data-model](../003-gpu-acceleration/data-model.md). No server-side entity, table,
migration or API field is added (contracts/DELTA.md). Everything below lives in the browser
(section 1) or in memory in the Blazor app (sections 2-4). Contracts:
[ui-test-ids 1.3.0](contracts/ui-test-ids.md), [help-content 1.0.0](contracts/help-content.md),
[web-interop 1.0.0](contracts/web-interop.md). Design: ADR-013.

## 1. User preferences (browser local storage)

Single user, one record per browser profile. Key `stemstudio.preferences`, value JSON (UTF-8),
max 16 KB.

### 1.1 Record (schema version 1)

| Field | Type | Default | Rule |
|---|---|---|---|
| `schemaVersion` | int | `1` | a record with another version is discarded and replaced by defaults on the next write (no migration needed for v1) |
| `level` | `"beginner"` \| `"advanced"` \| `"expert"` \| `null` | `null` | `null` = not chosen yet; the effective level is then Beginner |
| `theme` | `"system"` \| `"light"` \| `"dark"` | `"system"` | |
| `density` | `"auto"` \| `"comfortable"` \| `"compact"` | `"auto"` | `auto`: compact at Expert, comfortable otherwise |
| `motion` | `"system"` \| `"reduced"` | `"system"` | `system` follows `prefers-reduced-motion` |
| `help.welcomeDismissed` | bool | `false` | true after a level choice or "Not now" |
| `help.tours` | map `tourId -> { version: int, status: "completed" \| "dismissed", at: ISO 8601 }` | `{}` | <= 50 entries; ids kebab-case |
| `help.hintsDismissed` | string[] (topic ids) | `[]` | <= 200 entries |
| `updatedAt` | ISO 8601 UTC | write time | informational |

Example:

```json
{
  "schemaVersion": 1,
  "level": "advanced",
  "theme": "system",
  "density": "auto",
  "motion": "system",
  "help": {
    "welcomeDismissed": true,
    "tours": { "import-basics": { "version": 1, "status": "completed", "at": "2026-11-15T10:12:00Z" } },
    "hintsDismissed": []
  },
  "updatedAt": "2026-11-15T10:12:00Z"
}
```

### 1.2 Validation and failure behaviour

- Read: parse -> per field validate -> invalid or missing field takes its default (others kept).
  Unparseable JSON, a non-object, an unknown `schemaVersion` or a size > 16 KB -> all defaults.
  Unknown extra fields are dropped on the next write.
- Storage unavailable (exception on access, quota): defaults in memory; `storageAvailable = false`
  is shown once in the preferences panel (`pref-storage-unavailable`); writes are no-ops.
- Write: whole record, after validation, debounced 250 ms in .NET; `updatedAt` set by JS.
- Cross-tab: the `storage` event delivers the new record; the receiving tab validates it and
  applies level/theme/density/motion immediately (FR-020); help progress is merged by taking the
  newer `at` per tour.

### 1.3 State transitions

```text
level:   null --(welcome choice | level switch)--> beginner|advanced|expert --(level switch)--> any
welcome: shown --(choice | "Not now" | tour start)--> dismissed --(pref-reset-help)--> shown
tour t:  not offered --(route+level match, no record or record.version < t.version)--> offered
         offered --(start)--> running --(last step | Done)--> completed(v)
                                     --(close | Escape | navigation)--> dismissed(v)
         offered --(offer dismiss)--> dismissed(v)
```

## 2. Experience model (C#, `src/StemStudio.Web/Experience/`)

| Type | Shape | Rules |
|---|---|---|
| `ExperienceLevel` | enum `Beginner = 0, Advanced = 1, Expert = 2` | ordered; JSON names lower-case |
| `UiFeature` | record `(string Id, ExperienceLevel Min, ExperienceLevel? Max, string TopicId)` | id `^[a-z]+(\.[a-z0-9-]+)+$` |
| `FeatureCatalog` | static list of `UiFeature` (design-spec.md 5) + `IsVisible(id, level)` | unknown id -> `ArgumentException` (caught by tests); visible iff `Min <= level <= (Max ?? Expert)` |
| `ExperienceContext` | cascading value: `Level`, `Density`, `IsVisible(featureId)` | changes raise one re-render of subscribers |

## 3. Preferences state (C#, `src/StemStudio.Web/Preferences/`)

| Type | Shape |
|---|---|
| `UserPreferences` | immutable record mirroring 1.1 (`Level?`, `Theme`, `Density`, `Motion`, `HelpProgress`) with `EffectiveLevel` (null -> Beginner) and `EffectiveDensity` |
| `HelpProgress` | `WelcomeDismissed`, `IReadOnlyDictionary<string, TourRecord> Tours`, `IReadOnlySet<string> HintsDismissed` |
| `TourRecord` | `(int Version, TourStatus Status, DateTimeOffset At)`; `TourStatus { Completed, Dismissed }` |
| `IPreferencesStore` | `Task<PreferencesSnapshot> LoadAsync()`, `Task<bool> SaveAsync(UserPreferences)`; implemented over `IWebInterop` |
| `PreferencesSnapshot` | `(UserPreferences Preferences, bool StorageAvailable)` |
| `PreferencesState` | scoped service: `Current`, `Loaded`, `StorageAvailable`, `SetLevelAsync`, `SetThemeAsync`, `SetDensityAsync`, `SetMotionAsync`, `DismissWelcomeAsync`, `RecordTourAsync`, `DismissHintAsync`, `ResetHelpAsync`, event `Changed`; applies appearance through interop on each change |

## 4. Help model (C#, `src/StemStudio.Web/Help/`)

| Type | Shape | Source |
|---|---|---|
| `HelpTopic` | `Id, Title, Area, Summary, Feature?, Levels, Route?, Anchors, Keywords, Related, Reviewed, Markdown` | `Help/topics/**/*.md` |
| `RenderedTopic` | `Topic`, `Html` (sanitised Markdig output), `Headings` | rendered on first open, cached |
| `TourDefinition` | `Id, Version, Title, Route, Levels, Steps` | `Help/tours/*.json` |
| `TourStep` | `Target, Topic, Title?, Text?` | |
| `IHelpCatalog` | `Topic(id)`, `ScreenTopic(route)`, `TopicForAnchor(helpId)`, `Areas`, `Tours`, `Render(id)` | singleton, built at start-up |
| `IHelpSearch` | `Search(query, max = 10) -> IReadOnlyList<HelpHit>`; `HelpHit(TopicId, Score)` | in-memory index, research.md R-06 scoring |
| `HelpContext` | scoped: `IsOpen`, `CurrentTopicId`, `OpenAsync(topicId?)`, `Close()`, history for Back | resolves focus -> anchor -> topic, else screen topic |

## 5. Mixer additions (C#, `src/StemStudio.Web/Mixer/`)

| Type | Shape | Rules |
|---|---|---|
| `PracticePreset` | record `(string Id, string Label, Func<MixerState, bool> IsAvailable, Action<MixerState> Apply)` | ids as ui-test-ids `<presetId>`; `Apply` uses only existing `MixerState` operations (mute, solo, volume, metronome enabled), so the mix becomes dirty like a manual change (FR-016) |
| `PracticePresets` | static list | `play-along-<stem>`: unsolo all, unmute all, mute `<stem>`, metronome on (drums only), volumes unchanged; `karaoke`: mute vocals; `focus-vocals`: solo vocals; `reset`: unmute and unsolo all, volumes 100 %, pan 0, metronome off |
| `ChannelSelection` | selected channel key or null | keys `1`..`9` map to standard strips in display order |

## 6. Design tokens

Defined in CSS (`wwwroot/css/tokens.css`, design-spec.md 2); no C# model. `StemColor.Var(stem)`
returns `var(--ss-stem-<stem>)` for the six stems (detail stems: the parent) and
`var(--ss-stem-other)` for unknown names; `StemColor.Pattern(detailIndex)` returns the pattern
class (`solid`, `dashed`, `dotted`, `double`).
