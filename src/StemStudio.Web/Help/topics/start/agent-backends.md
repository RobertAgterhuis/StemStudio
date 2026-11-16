---
id: agent-backends
title: "Agent backends"
area: start
summary: "The agent runs on Ollama on this computer by default; Claude is optional and paid per use; None turns it off."
route: "/settings"
anchors: ["agent-backends"]
keywords: ["ollama", "claude", "backend", "model", "local", "fallback", "settings"]
related: [agent-panel, agent-approvals, agent-claude-key]
reviewed: "011"
---

- **Ollama** (default, local, free) uses the model `qwen3.5:9b`. While a separation or generation job uses the
  graphics card, the agent switches to the smaller `qwen3.5:4b`; the badge says so.
- **Claude** (optional) is a paid online service. It can be chosen only when a Claude API key is available: entered
  in Settings (stored encrypted on this computer) or set as `ANTHROPIC_API_KEY` in the API's environment; see
  [Claude API key](help:agent-claude-key). StemStudio never shows the key again. Only text is sent: your commands,
  the tool list and short tool results, never audio, files or lyrics.
- **None** turns the agent off. Every other part of StemStudio works as before.

When the agent is not ready the panel says why and how to fix it, for example "Start Ollama" or
`ollama pull qwen3.5:9b`. StemStudio never switches to Claude by itself.
