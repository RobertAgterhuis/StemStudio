# Quickstart / validation: Own Lyrics to Song (feature 010)

Prerequisite: the 009 validation (specs/009-style-transfer/quickstart.md Q1-Q8) passed. V1-V2 run
anywhere (CI covers them); V3-V8 need the reference machine.

## V1 Default stack unchanged

`docker compose up -d --build --wait`: `/songs/new`, `/lyrics` and `/voices` open; lyrics can be
saved and edited; "Generate" and voice-profile creation are disabled with the "not installed"
reason (voice-profile creation needs the generator for the sample check). Full test suite green
(SC-007).

## V2 Fake generator stack

As 009 Q2, then `npx playwright test --grep @generative` incl. `song.spec.ts`, `voice.spec.ts`
(Chromium fake microphone with `tests/fixtures/voice/own-voice-20s.wav`). Repeat with
`STEMSTUDIO_FAKE_GENERATOR_MODE=unvoiced-sample` (profile refused with "unvoiced").

## V3 Song from lyrics (SC-001)

GPU generative stack (009 Q3). New song from `tests/fixtures/lyrics/sample-en.txt`, style "Pop rock",
male lead, 60 s, 1 take. Record cold (incl. LM load) and warm times and VRAM
(`nvidia-smi --query-gpu=memory.used --format=csv,noheader -lms 500`). Targets: <= 90 s warm,
<= 4 min cold (excluding the first download). Repeat with 3 minutes (<= 4 min warm) and with the
Dutch sample (`sample-nl.txt`, language `nl`).

## V4 Labels

Take details show lyrics, style, voice options; FLAC tags `AI_GENERATED=true`, `AI_VOICE=synthetic`
(`ffprobe -show_format take-1.flac`); instrumental take: no `AI_VOICE` tag, `data-vocal="none"`.

## V5 Voice profile with consent (SC-004)

Owner records 20-30 s of own singing in `/voices` (microphone), ticks consent, types the name, saves.
Check: the profile shows the statement version `consent.v1`, the name and time. Try: save without
the checkbox (refused), upload a stem downloaded from StemStudio (refused `libraryAudio`), upload
10 s of noise (refused `unvoiced`).

## V6 Use my voice (SC-002)

On the 60 s take: "Use my voice" with the profile. Expected: import + GPU separation (worker)
-> conversion. Record separation and conversion times (target conversion <= 2 min for 60 s) and VRAM.
The new take is labelled "AI voice: <name>"; `/vocal` plays the converted vocal alone.

## V7 Deletion (SC-006)

Delete the profile with "also delete converted takes". Then:

```powershell
docker compose exec api sh -c "find /data -path '*voices*' -o -name '*vocal*'"
docker compose exec generator sh -c "find /data -type f"
```

Neither shows files of the profile or its conversions.

## V8 Listening check (SC-003)

Owner: 5 songs from own lyrics (3 English, 2 Dutch); for each, are >= 80 % of the lines intelligible
and in order? Target >= 4 of 5. Record in `docs/STATUS.md`.
