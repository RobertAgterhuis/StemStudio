# Contract: StemStudio -> DAWproject 1.0 mapping (v1.0.0, feature 021)

Binding for the backend (`DawProjectBuilder`, `DawExportService`) and for qa's golden files.
Rationale: ADR-034, research R-12..R-15. Schema: DAWproject 1.0 `Project.xsd` / `MetaData.xsd`
(bitwig/dawproject, MIT), vendored in `src/StemStudio.Domain/DawProject/Schemas/` (Phase 0 T005).

## 1. Package

| Entry | Content |
|---|---|
| `project.xml` | UTF-8, no BOM, `<Project version="1.0">` |
| `metadata.xml` | `<MetaData>` with `Title`, `Artist`, `Comment` ("Exported from StemStudio <version>; contains AI-generated parts: <names>" when any), `Software`, `Website` omitted |
| `audio/<nn>-<slug>.wav` (or `.flac`) | one file per exported channel; `nn` 01..80 = track order (data-model 4.4); slug = lower-case ASCII of the channel name, `[a-z0-9-]`, <= 40 |

ZIP: entries in the order above, XML `Deflate` optimal, audio `Stored` (no compression), UTF-8 entry
names, no directory entries, no extra fields. Deterministic: fixed entry timestamps
(1980-01-01T00:00:00), so the same recipe gives the same bytes (golden tests).

## 2. Project structure

```xml
<Project version="1.0">
  <Application name="StemStudio" version="<app version>"/>
  <Transport>
    <Tempo unit="bpm" value="<first bar bpm>" min="20" max="300" id="tempo"/>
    <TimeSignature numerator="<n>" denominator="<4|8>" id="timesig"/>
  </Transport>
  <Structure>
    <Track contentType="audio" loaded="true" id="t01" name="<name>" color="#RRGGBB" comment="...">
      <Channel audioChannels="2" role="regular" solo="<bool>" destination="master" id="c01">
        <Devices> ... built-in Equalizer / Compressor ... </Devices>
        <Mute value="<bool>" id="c01-mute"/>
        <Pan unit="normalized" value="<(pan+1)/2>" min="0" max="1" id="c01-pan"/>
        <Volume unit="linear" value="<volume>" min="0" max="2" id="c01-vol"/>
      </Channel>
    </Track>
    ...
    <Channel audioChannels="2" role="master" id="master">
      <Volume unit="linear" value="<master volume>" min="0" max="2" id="master-vol"/>
    </Channel>
  </Structure>
  <Arrangement id="arr">
    <Lanes timeUnit="seconds" id="lanes">
      <Lanes track="t01" id="lanes-t01">
        <Clips id="clips-t01">
          <Clip time="<s>" duration="<s>" playStart="<s>" contentTimeUnit="seconds"
                fadeTimeUnit="seconds" fadeInTime="<s>" fadeOutTime="<s>">
            <Audio channels="2" sampleRate="44100" duration="<file seconds>" id="a01">
              <File path="audio/01-vocals.wav"/>
            </Audio>
          </Clip>
        </Clips>
      </Lanes>
    </Lanes>
    <Markers id="markers"><Marker time="<s>" name="<label>" color="#RRGGBB"/></Markers>
    <TempoAutomation unit="bpm" id="tempo-auto">
      <Target parameter="tempo"/>
      <RealPoint time="<bar start s>" value="<bpm>" interpolation="hold"/>
    </TempoAutomation>
    <TimeSignatureAutomation id="ts-auto">
      <Target parameter="timesig"/>
      <TimeSignaturePoint time="<s>" numerator="<n>" denominator="<d>"/>
    </TimeSignatureAutomation>
  </Arrangement>
</Project>
```

Element and attribute spelling, order and required attributes follow the XSD; the builder's unit
tests validate every golden file against it. Times are seconds with 6 decimals (`InvariantCulture`,
`R` round-trip for values that need it). Ids are unique (`xs:ID`), referenced by `xs:IDREF`.

## 3. Value mapping

| StemStudio | DAWproject | Rule |
|---|---|---|
| Channel volume (linear gain 0-2, 001 mix) | `Channel/Volume unit="linear"` | same value |
| Channel pan -1..1 | `Channel/Pan unit="normalized"` 0..1 | `(pan + 1) / 2`; centre 0.5 |
| Mute | `Channel/Mute value` | as stored in the mix (the audible-level gate is not re-evaluated) |
| Solo | `Channel/@solo` | as stored |
| Detail stems of an expanded parent (002 gate rule) | own tracks | the parent's own track is exported only when listed; the export report notes that the DAW does not apply StemStudio's group gate |
| Master volume | master `Channel role="master"` `Volume` | |
| 017 EQ band (low shelf / peak / high shelf / high-pass / low-pass) | `Equalizer/Band type=lowShelf|bell|highShelf|highPass|lowPass` | `Freq unit="hertz"`, `Gain unit="decibel"`, `Q unit="linear"`, `Enabled`; device `deviceName="StemStudio EQ" deviceRole="audioFX"` |
| 017 compressor | `Compressor` | `Threshold unit="decibel"`, `Ratio unit="linear"`, `Attack`/`Release unit="seconds"`, `OutputGain unit="decibel"` (make-up), `AutoMakeup false` |
| 017 practice EQ preset | `Equalizer` bands when it is an EQ curve; else report | |
| 017 reverb | - | report: "DAWproject has no built-in reverb" |
| Amp chain (021) | - | report; the wet take is the track's audio, the dry DI is a second track with `Mute value="true"` |
| Parts (021) | own track, `comment="AI-generated (ACE-Step 1.5 base)"` | listed in `aiGeneratedTracks` |
| Takes (015) | own track(s) | wet + muted dry when a wet file exists; otherwise the dry take |
| Sections (014) | `Markers/Marker` | at section start; name = label; colour = 014 colour |
| Beat grid (001) | `TempoAutomation` + `TimeSignatureAutomation` | data-model/research R-14; `tempoMode=constant` -> no `TempoAutomation`, `Transport/Tempo` = median |
| Arrangement (019) | `Clip`s per segment and channel | `time` = arranged start, `duration` = segment length, `playStart` = source start, fades as 019 |
| Metronome, loop, karaoke, count-in, speed/transpose (012) | - | report: "Practice settings are not exported" (speed and transpose: audio is exported at original speed and pitch) |
| Mix name, track title/artist | `metadata.xml` Title/Artist, Track names | |

Colours: 004 stem palette for stems (detail stems use their parent's colour), `#9B7BD4` for parts,
`#5FA8A0` for takes.

## 4. Validation and failure

1. Build `project.xml` and `metadata.xml` in memory.
2. Validate each with `XmlSchemaSet` (both XSDs, `XmlSchemaValidationFlags.ReportValidationWarnings`);
   any error or warning -> export `failed` with "DAW project failed validation: <first message>"
   (the full list goes to the log, never the audio paths outside the data directory).
3. Check every `File/@path` exists in the ZIP plan and every `Audio/@duration` matches the rendered
   file within 1 frame.
4. Only then write the ZIP to `assets/<id>/` and mark the asset `ready`.
