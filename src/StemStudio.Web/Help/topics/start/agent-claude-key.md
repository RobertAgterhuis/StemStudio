---
id: agent-claude-key
title: "Claude API key"
area: start
summary: "Enter the Claude API key in Settings (stored encrypted on this computer) or set ANTHROPIC_API_KEY; the environment variable wins."
anchors: ["agent-claude-key"]
keywords: ["claude", "anthropic", "api key", "key", "encrypted", "ANTHROPIC_API_KEY", "replace", "delete", "test", "settings"]
related: [agent-backends, agent-panel]
reviewed: "011"
---

Claude needs your own API key from the Anthropic console. StemStudio accepts it from two places:

- **Settings -> Composer agent -> Claude API key.** Paste the key and press **Save key**. The field empties at once and
  the key is stored encrypted on this computer.
- **The `ANTHROPIC_API_KEY` environment variable** of the StemStudio API (set in the shell that starts StemStudio).

When both exist, **the environment variable wins**. The saved key is kept and shown as "not used while
ANTHROPIC_API_KEY is set"; it is used again once the variable is removed.

## What you see

StemStudio never shows the key again. The status only says where the key in use comes from, its **last four
characters** (so you can recognise it) and when a saved key was stored. Saving a key does not select Claude: you
still choose Claude and accept the paid-service notice. Deleting the key does not switch the agent back either; the
agent panel then offers **Switch to local**.

The key can only be entered, replaced, deleted or tested on the computer StemStudio runs on, with StemStudio opened
as `http://localhost:8080`. From another device the controls are disabled.

**Test key** asks Anthropic once whether it accepts the key, without generating text and without cost. Nothing else
contacts Anthropic: not saving, not opening Settings, not starting StemStudio.

## What the encryption protects, and what it does not

- It protects the key when someone gets a copy of the library database, a backup or an export, the logs or a
  screenshot: they contain at most the encrypted key and its last four characters. The encryption keys live in a
  separate place that is never part of the library or a backup.
- It does not protect against someone with full access to this computer (an administrator account, malware running
  as you, a copy of the whole disk), and it cannot hide the key from the running program, which needs it to talk to
  Claude.

## Replace or delete

If the key may have leaked, first revoke it in the Anthropic console, then press **Replace key**, paste the new key
and press **Save new key**. **Delete key** removes the saved key after a confirmation; the environment variable is not
affected. A key change applies to the next command; a command that is already running keeps its key.

## "The stored key cannot be read"

After restoring a backup on another computer, or when the encryption keys were removed, the saved key can no longer
be decrypted. Everything else keeps working. **Enter the key again**: press **Replace key**, paste the key and save.
