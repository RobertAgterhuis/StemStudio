---
id: lyrics-format
title: "Writing lyrics for a song"
area: mixes
summary: "Up to 4,096 characters of your own lyrics, with optional markers such as [verse] and [chorus]; .lrc times are dropped."
anchors: ["lyrics-format"]
keywords: ["lyrics format", "markers", "verse", "chorus", "lrc", "txt", "characters", "own lyrics"]
related: [lyrics-sections, song-options]
reviewed: "010"
---

Lyrics for a song are plain text of at most **4,096 characters**. Empty lines separate the parts.

- **Section markers** on their own line tell the model what a part is: `[intro]`, `[verse]`, `[pre-chorus]`,
  `[chorus]`, `[bridge]`, `[outro]` and `[instrumental]`; numbered ones such as `[verse 2]` work too. They are not sung.
- **Files**: `.txt` and `.md` are read as they are (UTF-8, at most 64 KB). In an `.lrc` file the time stamps are
  dropped, so only the words remain.
- **Length**: about 2 to 3 words per second of song sound natural. The page warns above 4 words per second.

Use lyrics you wrote yourself or may use. They stay on this computer.
