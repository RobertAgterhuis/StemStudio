---
id: lyric-assist-privacy
title: "Privacy and models of the lyric assistant"
area: mixes
summary: "By default everything runs on this computer; with Claude selected your lyrics text is sent to Anthropic after a notice."
anchors: ["lyric-assist-privacy"]
keywords: ["privacy", "local model", "ollama", "claude", "anthropic", "notice", "tokens", "usage", "fallback", "dictionary", "offline"]
related: [lyric-assist, lyric-assist-limits, agent-backends]
reviewed: "023"
---

The assistant uses the AI backend chosen in **Settings** for the agent:

- **Local (Ollama)**, the default: the model runs on this computer. While a heavy job uses the graphics card the
  model help waits: the pane says the graphics card is busy, and the spelling check still works.
- **Claude** (optional): the first time, a notice asks you to agree that **the text of the sheet is sent to
  Anthropic** for the check, paid per use. Nothing is sent before you agree. Token use is shown (Advanced).
- **Off**: only the dictionary layer runs.

Spelling, your words, intentional marks, syllables and the rhyme finder always run on this computer, also without a
model. When the model is off or not reachable, the pane names what is unavailable and how to fix it.

The layer line (Advanced) names the dictionary and the model; Expert also shows the source of each suggestion and
an assist log with blocks, timings, discarded items and token use.
