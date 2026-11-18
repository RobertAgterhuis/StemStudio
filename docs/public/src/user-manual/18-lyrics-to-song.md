# Songs from your own lyrics

Feature 010.

Write your own lyrics and let StemStudio generate a complete song with music and a sung
voice. Optionally, the voice can be converted into **your own voice**, under strict consent
rules.

Like style transfer, this needs an NVIDIA GPU and the generative option (chapter 17).

## Writing or uploading lyrics

Open **New song**. Type your lyrics, or upload a `.txt`, `.lrc` (timestamps are removed) or
`.md` file of up to 64 KB. Lyrics may be up to 4,096 characters.

Mark the parts of the song with section markers on their own line:
`[intro]`, `[verse]`, `[pre-chorus]`, `[chorus]`, `[bridge]`, `[outro]` and
`[instrumental]`; numbered markers such as `[verse 2]` are allowed. **Add sections** proposes
markers for you: blocks separated by blank lines become verses and a repeated block becomes
the chorus. Saved lyrics are kept on the **Lyrics** screen. Press the title of a saved sheet
there to open it in the lyrics editor, where the **Assistant** checks spelling, grammar and
flow and helps you write (chapter 19).

## Song options

| Option | Values |
|---|---|
| Title and language | More than 50 languages, including English and Dutch |
| Style | A style from the library, or your own short description (same rules as chapter 17) |
| Lead voice | Female, male or any; plus a delivery mood |
| Instrumental | Generates music without vocals |
| Tempo and key | 40 to 220 BPM; any key |
| Duration | 10 seconds to 8 minutes (default 2 minutes) |
| Takes | 1 or 2 |

On the reference machine (RTX 5070 Ti) a 45-second song with vocals takes about 50 seconds
from a cold start; converting a 10-second vocal into your voice takes about 25 seconds.

A warning appears when the lyrics are very dense (more than four words per second); you can
still generate. Choose the language that matches your lyrics for the best pronunciation.
You are responsible for the lyrics you enter.

## Your own voice: consent first

You can create a **voice profile** on the **Voices** screen and use it to convert the
generated vocal into your voice. Only **your own voice** may be used. There is no way to use
a track, a stem or someone else's recording.

1. Press **New voice**.
2. **Record** 10 to 60 seconds of dry singing (20 to 30 seconds works best; recording stops
   automatically at 60 seconds) or **upload** a recording (WAV, FLAC or MP3, up to 20 MB).
3. Read the consent statement, tick the box and type your name:

   > "This is my own voice. I agree that StemStudio uses this recording on this computer to
   > convert generated vocals into my voice. I can delete it at any time."

4. Give the profile a display name and press **Save**.

StemStudio refuses a sample that is too short, too long, mostly silent, or an exact copy of
audio already in your library (a stem, take or export). The profile stores the consent
statement, your name, the date and a fingerprint of the sample. Voice data never leaves your
computer.

**Deleting** a profile removes the sample and everything derived from it. Converted takes
are kept by default and labelled *(profile deleted)*, unless you tick the option to delete
them too.

## Use my voice

On a song take that has vocals, press **Use my voice**, choose your profile and start. The
take is separated first (status *waiting for stems*), then the vocal is converted while
melody, timing and words stay the same, and mixed back. The result is a new take.

## AI labels

Every result is labelled **AI-generated** and, when it has vocals, **AI voice** or **AI
voice converted to** *profile name*. The same labels are written into the audio file.
Generated songs are for personal use.

When lines of your lyrics came from the lyric assistant, the take details also say **Lyrics: n
lines AI-assisted** (chapter 19).
