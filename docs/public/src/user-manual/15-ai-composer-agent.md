# The AI composer agent

Tell StemStudio what you want in plain English or Dutch, and the agent carries it out with
the same actions you would use yourself: import, separate, mix, export, transcribe, restyle
and generate. You stay in control: anything heavy or destructive waits for your approval.
The agent is optional; every other part of StemStudio works the same without it.

## Before you start

The agent uses a language model that runs on your own computer with **Ollama**. Ollama is a
separate, free program that the administrator installs on the computer that runs StemStudio,
together with two models (see the technical manual, chapter 3):

- `qwen3.5:9b`, the default model (about 6.6 GB);
- `qwen3.5:4b`, a smaller model that is used while the graphics card is busy with a
  separation or generation job.

StemStudio never downloads models itself. If Ollama is not running or a model is missing, the
panel says so and tells you the fix, for example *"Model qwen3.5:9b is not pulled."* with
the command `ollama pull qwen3.5:9b` (chapter 19).

## Opening the agent

Press **Agent** in the header, available on every page, to open the panel. Type a command (up
to 4,000 characters) and press **Enter** or **Send**; **Shift+Enter** starts a new line and
**Escape** closes the panel. The agent answers in the language of your command and knows
which track or mix is open. Examples:

- *"Import this YouTube link and split it further."*
- *"Mute the drums, loop bars 17 to 24 and turn on the click."*
- *"Sla de wijzigingen op in deze mix."*
- *"Make sheet music of the bass of Hero of the Day."*

For a task with several steps, the agent first shows a numbered **plan** and then works
through it. From Advanced level each step is listed with its status: planned, awaiting
approval, running, waiting for job, done, failed, skipped or undone; a job step shows its
progress. When a command is ambiguous (for example two tracks match) the agent asks which one
you mean instead of guessing. Questions outside StemStudio are politely declined.

Your conversations are kept on this computer. When you reload the page or come back later,
the panel opens the latest conversation again. From Advanced level you can rename and delete
conversations; **New** starts an empty one. Only one command runs at a time in StemStudio;
**Cancel** stops a running command; jobs it already started keep running and can be
cancelled on their own page.

## Approvals

An **approval card** appears before every heavy action (a long job on the GPU or CPU, such as
splitting further, transcribing or generating) and every destructive action (deleting or
overwriting something). It says exactly what will happen, with the arguments, whether it can
be undone and, for heavy jobs, the estimated time and device. A delete card names the track
or mix.

- **Approve** runs the step; **Reject** skips it and the steps that depend on it.
- **Approve all heavy steps of this plan** approves the heavy, non-destructive steps of the
  plan at once.
  Destructive steps are always asked one by one.
- A pending card is still there when you come back after closing the browser; it expires
  after 24 hours.

Extra safety: once the agent has read text that does not come from you, such as track titles
from YouTube or Spotify, lyrics or band information, it also asks before changing anything
else in that command. The card then says the step was proposed after reading external
content. This is why, for example, *"export the bass of Hero of the Day"* asks for approval:
the agent first searched the library and read the track's title. Links and lyrics must come
from your own messages; a link that only appears in a track title is never imported.

If the size of a job cannot be checked beforehand, the card says *"The size could not be
checked, so this is treated as a long job"* and asks you like any heavy step.

## Undo

Press **Undo** to reverse the agent's last command: mixer changes return to their previous
values, a newly saved mix is deleted again, an overwritten mix gets its previous version back
and a running job is cancelled. Press it again for the command before. Undo is disabled when
nothing of the conversation is left to undo. Imports, exports and generated results are not
undone; ask the agent to delete them, which asks for confirmation.

## Library and lyrics

The agent can browse a library folder, search the library (chapter 10), open a folder for you
and change **library info**. Folder and item names count as text from outside: after the agent
has read them, changing library info asks for your approval.

For your own lyrics sheets (chapter 14) it can start a check or writing help, list the
suggestions (line numbers, categories and states), look up rhymes for a word you name, count syllables and open
the lyrics editor. Its answers carry counts and ids, never the lyric text. **Applying** suggestions
always shows an approval card, whatever the plan allows. The card lists each change as before
and after, read from the stored suggestions and not from the agent's own words, and it expires
after 24 hours like every card. In the Expert assist log such a check shows **Started by**
*agent*. The agent cannot confirm that a text is your own work, and it cannot write other text
into a sheet.

## What the agent does not do

- It never creates or deletes a voice profile and never gives consent. It can convert a take
  to **your own** voice only with a profile you already made with consent (chapter 13), and
  only after you approve the card.
- It refuses requests to imitate an artist; styles describe genres and sounds.
- It has no access to files, the shell, the internet at large or the settings.

## Local AI or Claude

In **Settings** (in the left rail) you choose the language model behind the agent:

- **Ollama (on this computer, free)**, the default. From Advanced level you pick the model and
  the fallback model; models that are not pulled or cannot call tools are marked and refused.
- **Claude (online, paid per use)**: Anthropic's model. It can only be selected when a Claude
  API key is available (see "Your Claude API key" below); otherwise it shows *"not
  configured"*. When you select it you must confirm the notice *"paid per use, text only is sent
  to Anthropic"*. The badge then shows the model, and from Advanced level each command shows its
  token usage. Expert level adds the Claude model and effort options.
- **None (agent off)**.

StemStudio never switches between Ollama and Claude by itself; it may switch silently from
the default local model to the smaller local model while a GPU job runs (the badge shows
which). If Claude fails (key rejected, no connection, rate limit), the panel says why and
offers **Switch to local**. **Test connection** checks the chosen backend within ten seconds.

## Your Claude API key

You can enter the key yourself, on every level, under **Settings -> Composer agent -> Claude API
key**:

1. Open StemStudio on the computer it runs on, as `http://localhost:8080`. From another device
   the field is disabled with the note *"The Claude key can only be entered on the computer
   StemStudio runs on"*.
2. Paste the key from the Anthropic console and press **Save key**. The field empties at once;
   the status shows only where the key comes from and its last four characters
   (*"ends with ...WXYZ"*). The key is stored encrypted on this computer.
3. Saving does **not** select Claude: choose it as the backend and confirm the notice.

- **Test key** checks the key with Anthropic once, only when you press it (free; no text is
  generated). When both a saved key and the environment key exist you choose which one to test.
- **Replace key** opens an empty field for a new key; **Delete key** asks for confirmation.
  Neither changes the selected backend. After a leak, revoke the key in the Anthropic console
  first, then replace it here.
- If the administrator has set `ANTHROPIC_API_KEY`, that key **wins**; a saved key is then shown as
  *"not used while ANTHROPIC_API_KEY is set"*.
- *"The saved key cannot be read on this installation"* means the encryption keys were lost (for
  example after a restore on another computer). Press **Replace key** and paste the key again.
- A key change during a running command applies to the next command; the panel says *"Key
  changed - applies to the next command"*.
- What the encryption protects: a copy of the database, backups and exports, logs and
  screenshots. What it does not protect: someone with full access to this computer, or malware
  running as you.

## Levels and limits

Beginners see the chat, the plan and approvals; Advanced adds the backend badge, the step
list with job details, conversation actions, Claude usage and the model pickers; Expert adds
the full tool log with the exact arguments and results. One command may use up to 12 model
turns and 20 actions.

Known limits:

- The local model is good, not perfect: in the project's evaluation it handled most test
  commands correctly, but not every time. Check the plan and the approval cards.
- If StemStudio restarts while a command runs, the command is marked as interrupted. Jobs it
  started keep running. There is no *Continue* button yet: send the command again.
- After a library search every further change or job in the same command asks for approval
  (see *Extra safety* above), even when the request looks harmless.
