# Style transfer and the style library

Feature 009.

Hear a song you imported in another style: your rock song as a bossa nova, a jazz trio or
synthwave. Each result is a **take** that is saved in your library and clearly labelled as
AI-generated.

## What you need

Style transfer runs a large generative model and needs an **NVIDIA GPU** with at least about
8.5 GB of free graphics memory (about 14 GB for the standard quality profile). There is no
CPU version. The administrator starts StemStudio with the generative option (see the
technical manual). Without it, the controls are disabled and show the reason, for example
*"Generative service not installed"* or *"Requires an NVIDIA GPU: …"*. Everything else in
StemStudio keeps working.

The first use downloads the model (about 10 GB); the job then shows *Downloading model
(first use)*.

How long it takes, measured on the reference machine (RTX 5070 Ti, 16 GB): a 30-second range
is restyled in about 16 seconds, including loading the model after it was unloaded while idle.
Longer ranges and two takes take proportionally longer.

## Restyling a song

1. Open a completed track and press **Restyle**.
2. Pick a **style** from the library. Its description and default strength are shown.
3. Choose the **mode**:
   - **Range**: generate a new piece from a part of the song (start and end time).
   - **Restyle section in place**: you get the full song back with only the chosen part
     changed.
4. Tick the **stems** to use as the source. Leave out the vocals for an instrumental result.
5. Set the **strength** in five steps: *Subtle*, *Light*, *Balanced*, *Strong*, *Free*.
   Subtle stays close to the original, Free follows the style most.
6. Choose **1 or 2 takes** and press **Start**.

The range must be between 10 seconds and 8 minutes. Jobs show *queued*, *Waiting for the
GPU*, *Downloading model*, *running* with progress, *completed*, *failed* or *cancelled*.
You can cancel a running job and retry a failed one.

## Generations and takes

The **Generations** screen (and a section on the track page) lists all jobs. For each take
you can **play** it, view its **details** (style, strength, seed, source and range, model,
device, time) and **Import as track**: the take becomes a new track that is separated and
mixed like any other song, with an **AI-generated** badge and a link to its source.

## The style library

The **Styles** screen holds 24 ready-made presets in 13 families, among them *Jazz trio*,
*Big band swing*, *Bossa nova*, *Salsa*, *Reggae*, *Acoustic blues*, *Country*,
*Symphonic orchestra*, *String quartet*, *Solo piano*, *Synthwave*, *House*, *Drum and bass*,
*Chiptune*, *Lo-fi hip hop*, *Funk*, *Classic soul*, *Punk rock*, *Heavy metal* and *Ambient*.

Filter by genre, mood, instrument or kind, and search by name. Presets cannot be changed,
but **Duplicate** creates an editable copy. In your own styles you set a name, genre,
sub-genre, tempo, instrument and mood tags, vocal handling, default strength and a
description. Changing or deleting a style never changes takes made earlier.

## Rules for styles

Styles describe music, not people. Descriptions that ask to imitate an artist are refused,
for example *"in the style of …"*, *"sounds like …"*, *"voice of …"* (and the Dutch *"in de
stijl van"*, *"klinkt als"*, *"stem van"*), as are artist names from your library and a list
of known names. This list can never be complete, so please respect the intent of the rule.

## AI labels and personal use

Every take carries an **AI-generated** badge in the app and labels inside the file
(`AI_GENERATED=true` and the generator name). Restyled versions of copyrighted songs are for
your personal practice only and must not be published.
