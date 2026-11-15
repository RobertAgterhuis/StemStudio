# The AI composer agent

<span class="planned">In development</span> Feature 011. This chapter describes the
specified behaviour.

Tell StemStudio what you want in plain English or Dutch, and the agent carries it out with
the same actions you would use yourself: import, separate, mix, export, transcribe, restyle
and generate. You stay in control: anything heavy or destructive waits for your approval.

## Opening the agent

Press the **agent** button, available on every page, to open the panel. Type a command (up
to 4,000 characters) and press **Send**. The agent answers in the language of your command
and knows which track or mix is open. Examples:

- *"Import this YouTube link and open the mixer when it is ready."*
- *"Mute the drums, loop from 1:20 to 1:45 and turn on the click."*
- *"Save this as 'Drums out, slow' and export it as MP3."*
- *"Maak bladmuziek van de baspartij."*

For a task with several steps, the agent first shows a numbered **plan** and then works
through it. Each step shows its status: planned, awaiting approval, running, waiting for
job, done, failed, skipped or undone. When a command is ambiguous (for example two tracks
match) the agent asks which one you mean instead of guessing. Questions outside StemStudio
are politely declined.

## Approvals

An **approval card** appears before every heavy action (a long job on the GPU or CPU) and
every destructive action (deleting something). It shows exactly what will happen, with the
arguments, whether it can be undone and, for heavy jobs, the estimated time and device.

- **Approve** runs the step; **Reject** skips it and the steps that depend on it.
- **Approve plan** approves all heavy, non-destructive steps of this plan at once.
  Destructive steps are always asked one by one.
- A pending card is still there when you come back after closing the browser.

Extra safety: when the agent has read text from outside (for example lyrics or band
information), or wants to use a link or lyrics that you did not type yourself, it also asks
before changing anything. The card then says the step was proposed after reading external
content.

## Undo

Press **Undo** or type *"undo"* (*"maak ongedaan"*) to reverse the last command: mixer
changes, a newly saved mix, an overwritten mix (the previous version comes back) and a
running job (it is cancelled). Undoing an import, export or generated result deletes it and
therefore asks for confirmation.

## What the agent does not do

- It cannot clone voices; voice profiles stay in the screens of chapter 12.
- It refuses requests to imitate an artist.
- It has no access to files, the shell, the internet at large or settings.

## Local AI or Claude

In **Settings** you choose the language model behind the agent:

- **Ollama** (default): a local model on your own computer, free and offline. Ollama is
  installed separately; StemStudio does not download models for you. If the model is
  missing you see *"Model not pulled: run `ollama pull qwen3.5:9b`"*.
- **Claude** (optional): Anthropic's model, paid per use. It can only be selected when the
  administrator has set the API key in the environment of the StemStudio API. When you select
  it you must confirm the notice *"paid per use, text only is sent to Anthropic"*. The badge
  then shows the model and the token usage.
- **None**: the agent is switched off.

StemStudio never switches between Ollama and Claude by itself. If Claude fails, you see
*"Claude not available: …"* with a button to switch to the local model. **Test connection**
checks the chosen backend within ten seconds.

## Levels and limits

Beginners see the chat, the plan and approvals; Advanced adds the backend badge, job details
and Claude usage; Expert adds the full tool log and model selection. One command may use up
to 12 model turns and 20 actions, and only one command runs at a time in StemStudio.
