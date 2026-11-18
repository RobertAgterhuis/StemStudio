# Lyric writing assistant

The lyric assistant helps you write and polish **your own lyrics** in Dutch or English. It
checks spelling, grammar and flow, suggests rhymes and other devices, writes alternatives or
the next lines, looks up rhymes and counts syllables. It never changes your text by itself:
every result is a **suggestion** that you accept or reject.

It works on the lyrics sheets of chapter 18 and does nothing until you press a button. Nothing
is checked while you type.

## Opening the assistant

1. Open **Lyrics** in the navigation.
2. Press the **title** of a sheet. The lyrics editor opens.
3. Press **Assistant**. The assistant pane opens next to the text (it is closed until you
   open it).

The sheet must be saved and its language must be Dutch or English. If the language is
*unknown*, the editor offers a one-click choice with a guess such as *Looks like Dutch*. For
other languages the controls are disabled and say why. Unsaved changes are saved first (with
the normal checks of the editor).

## What the assistant can do

| Button | What you get |
|---|---|
| **Check** | Spelling, grammar and flow (awkward phrasing, stumbling stress, repeated words) for the whole sheet or the lines you selected |
| **Suggest devices** | Optional proposals: end rhyme, internal rhyme, assonance, alliteration, an image or metaphor, a better syllable fit and deliberate poetic licences |
| **Alternatives** | 1 to 5 versions of the line at the cursor (3 by default) |
| **Continue** | 1 to 4 lines after the block at the cursor |
| **Title ideas** (Advanced) | Up to 5 titles |

Beside the buttons is an optional **Instruction** (up to 200 characters, for example *darker*
or *simpler words*) for devices and writing help. An instruction that names an artist or asks
to write like someone is refused; describe the mood or the theme instead.

**Cancel** stops a running assist. Only one assist runs at a time; a second request says *A
check is running.* On a sheet of about 40 lines a full check takes roughly half a minute to a
minute on the reference machine; the first results (spelling) appear at once and the model
results stream in, with a progress bar.

## Suggestions

Each suggestion appears twice: as a **highlight** in the text and as an entry in the list
with its category (spelling, grammar, flow, poetic licence, device, alternative, continuation
or title), a short explanation in the **language of your lyrics** and the proposed text.
Highlights differ by line style (wavy, double, dotted, dashed or solid underline), so you do
not depend on colour. Spelling suggestions come from a dictionary on your computer; grammar,
flow and writing help come from the AI model.

For each suggestion you can:

- **Accept**: only that span changes. The sheet is saved as a new revision and the line gets a
  small marker "accepted suggestion". Accepting one alternative closes the others.
- **Reject**: the suggestion closes and the text stays.
- **Mark as intentional** (corrections): the word, phrase or line is left alone by every later
  check for as long as it is in this sheet. At the Advanced level you also choose the scope
  of the mark: word, phrase or line.
- **Add to my words** (spelling, Advanced): the word, for example a name, counts as correct in
  all your sheets of that language. This also closes the suggestion.

**Accept all (n)** accepts the suggestions of the current filter at once as one undo step.
**Undo** restores the text from before the last accept. It works only while you have not saved
other changes since; otherwise the editor says what would be lost. A suggestion whose original
text is no longer at its place is shown as outdated and is never applied.

Details about the suggestions: when you edit the text after a check, suggestions follow their
line where possible. If you change the language of the sheet, open suggestions of the old
language become outdated. If the sheet changed in another browser tab, the editor reloads it
instead of overwriting it.

## Poetic licence is never an error

Lyrics may break the rules on purpose. Elisions (*'k*, *'t*, *gonna*), inversions for a rhyme,
archaisms, spelling for sound (*zooooo*), slang and a line in the other supported language are
**never reported as errors**. The assistant can also propose such licences itself, under
**Suggest devices**.

**Sensitivity** (Advanced) sets how much **Check** reports:

- **Loose**: only clear mistakes.
- **Balanced** (default): mistakes and improvements.
- **Strict**: also notes the licences it recognised, as information you can keep or replace by
  the standard form.

Sensitivity applies to **Check**; devices and writing help are explicit requests. At the
Advanced level the category chips (**All**, spelling, grammar and so on) filter the list.
**Intentional marks** lists your marks and lets you remove one, or **Mark selection as
intentional** marks the selected text without a suggestion. **My words** lists your word list
and lets you remove a word.

## Syllables, rhythm target and density

**Syllables** (Advanced) shows the syllable count of every line next to the text. It is
counted on your computer after you pause typing and sends nothing anywhere. A line is marked
when it is more than two syllables away from the middle of its section, or from the target.

The **rhythm target** turns tempo into a syllable goal per line: BPM (40 to 220), beats per bar
(2 to 7), bars per line (1 to 8) and syllables per beat (0.5 to 4). Example: 4 beats x 2 bars
x 1.5 syllables is 12 syllables per line. Press **Save target** or **Clear target**. Device
suggestions aim at the target and show the counts as *11 -> 9 syllables*; the counts are
checked before a suggestion is shown, and a suggestion with a wrong rhyme or count is dropped.

**Density** shows the words per second at the duration of the target; above four words per
second a line gets hard to sing.

## Rhyme finder

**Rhymes** opens the rhyme finder: type or select a word and press **Find**. It lists perfect
and near rhymes with their syllable counts within half a second, from data on your computer
only and without the AI model. **Insert** puts a rhyme at the cursor. Dutch rhymes come from
the base forms of words, so some inflected forms are missing.

## Keyboard

The assistant is fully usable with the keyboard. The keys work in the lyrics editor:

| Keys | Action |
|---|---|
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>C</kbd> | Check |
| <kbd>F8</kbd> / <kbd>Shift</kbd>+<kbd>F8</kbd> | Next / previous suggestion |
| <kbd>Alt</kbd>+<kbd>Enter</kbd> | Accept the suggestion |
| <kbd>Alt</kbd>+<kbd>Backspace</kbd> | Reject the suggestion |
| <kbd>Alt</kbd>+<kbd>I</kbd> | Mark as intentional |
| <kbd>Esc</kbd> | Back to the text |

The list of suggestions is one tab stop; move through it with the arrow keys. If your keyboard
layout uses <kbd>Ctrl</kbd>+<kbd>Alt</kbd> as <kbd>AltGr</kbd>, press the **Check** button
instead. A polite announcement tells a screen reader when a check has finished, with the
counts, and reads the focused suggestion: line, category, original, proposal and explanation.

## Your own lyrics only

The assistant works on your own lyrics sheets, never on lyrics downloaded for imported songs
(chapter 13). A sheet that largely matches the lyrics of a track in your library is refused
with *This text matches the lyrics of <track>. The assistant works on your own lyrics only.*

If the text really is yours (a song of your own band that you also imported), choose **This is
my own text**. The dialog shows the statement *This text is my own work ... I confirm that
StemStudio may treat it as my own lyrics although it matches the lyrics of an imported
track*, the matching track, a field for **Your name** and a tick box; **Confirm** is enabled
when both are filled. The sheet then shows *Own work confirmed by <name> on <date> (matches
<track>)* with **Revoke**. The confirmation covers only that track: a match with another track
is refused again. It never switches off the two safeguards below, and the composer agent
cannot confirm or revoke it.

- **No copies.** A proposal that repeats 7 or more words in a row, or a whole line, of lyrics
  in your library is withheld; the pane says how many (*n suggestions withheld, too close to
  existing lyrics*). Lyrics that are not in your library cannot be recognised, so always
  check your own text.
- **No imitation.** Instructions that name an artist or ask for a style "like" someone are
  refused, also with an own-work confirmation.

## Local model or Claude

The assistant uses the AI backend you chose in **Settings** for the agent (chapter 20):

- **Ollama (on this computer)**, the default. Nothing leaves your computer.
- **Claude (online, paid per use)**: the first time you use a model action, a notice *Your
  lyrics go to Anthropic* explains that the text of the sheet is sent to Anthropic. Nothing is
  sent before you press **I agree**; **Cancel** or <kbd>Esc</kbd> sends nothing. Each assist
  has a limit of 20,000 input and 4,000 output tokens by default, and at the Advanced level the
  pane shows the usage of the last assist. StemStudio never switches to Claude by itself.
- **None**: only the dictionary layer runs.

The status line (Advanced) names the layers, for example *Spelling: OpenTaal dictionary -
grammar and flow: qwen3.5:9b, local*.

**When the model is not available** (switched off, Ollama not running, or the graphics card is
busy with another job) the pane says which part is unavailable and how to fix it, for example
*Grammar, flow and writing help need the AI model: it is switched off in Settings.* The
buttons for devices, alternatives, continuation and titles are disabled with the reason, and
**Check** still does the **spelling check** from the dictionary, counts syllables and the
rhyme finder keeps working. The assistant does not use the smaller processor model of the
agent: it was too slow and unreliable for lyrics. If a heavy GPU job starts while an assist
runs, the assist stops with a readable message.

The local model is weaker in Dutch than in English; the pane then shows a note that the quality
is limited. Always read a suggestion before you accept it.

## Revisions, the log and the take details

Every save, accept and undo creates a **revision**; the last 50 per sheet are kept. At Expert
level **Revisions** lists them with their cause (*created*, *your save*, *accepted
suggestions*, *undo*) and a **Restore** button, which restores an earlier text as a new
revision. The Expert **Assist log** lists every assist: **Started by** (*editor*, or *agent*
when the composer agent started it), kind, status, blocks done and skipped, the model, how many
suggestions were discarded (invalid place, invalid answer, failed check, filtered, withheld)
and the tokens used. Expert also shows the source of each suggestion (dictionary, rules or
model).

When you generate a song from a sheet (chapter 18), the take details show **Lyrics: n lines
AI-assisted**, counted from the lines that came from accepted suggestions and were not edited
afterwards.

## Asking the agent

In the agent panel (chapter 20) you can write *"Check my lyrics Zomeravond on spelling only"*.
The agent starts the check and tells you the counts and where to find them; it never shows or
edits lyric text itself. Applying suggestions through the agent always shows an **approval
card** that lists every change as before and after, taken from the stored suggestions.
**Reject** applies nothing. The agent cannot write other text into a sheet.

## Levels

| Part | Beginner | Advanced | Expert |
|---|---|---|---|
| Check, Suggest devices, Alternatives, Continue, Rhymes, accept, reject, mark as intentional, Undo, Accept all | yes | yes | yes |
| Number of alternatives, **Title ideas**, category chips | no | yes | yes |
| Sensitivity, intentional marks, **My words** | no | yes | yes |
| **Syllables**, rhythm target, density | no | yes | yes |
| Status line with layers and model, Claude usage | no | yes | yes |
| Source of each suggestion, **Assist log**, **Revisions** | no | no | yes |

## Limits

- Dutch and English only, saved sheets of up to 4,096 characters.
- The model works in blocks of up to 8 lines, so a long sheet takes longer.
- The assistant can be wrong: a suggestion may be a mistake, miss a mistake or mislabel a
  deliberate choice. You decide.
- It cannot recognise lyrics that are not in your library.
