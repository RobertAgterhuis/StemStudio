---
id: agent-panel
title: "Composer agent"
area: start
summary: "Type a command in your own words; the agent plans the steps and runs them with StemStudio's own actions."
anchors: ["agent-panel"]
keywords: ["agent", "assistant", "chat", "command", "plan", "ai"]
related: [agent-approvals, agent-backends]
reviewed: "011"
---

Open the agent with **Agent** at the top right. Type a command in Dutch or English, such as "Zet de zang 3 dB
zachter" or "Import this link and split it further", and press **Enter** (Shift+Enter starts a new line).

- The agent sends the page you are on, the open track and the open mix with every command.
- For more than one action it first shows a **plan** and ticks the steps off while they run.
- Long jobs show their progress; the run continues when you close the panel.
- **Cancel** stops the run. Jobs it already started keep running unless you cancel them yourself.

From the Advanced level every step is listed with its tool and status; the Expert level adds the full tool log.
The agent can only use StemStudio's own actions. Steps that delete, overwrite or start a long job ask you first
(see [confirmations](help:agent-approvals)).
