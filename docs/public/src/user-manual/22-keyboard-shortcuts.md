# Keyboard, foot pedals and the command palette

StemStudio can be used completely with the keyboard, and in the mixer also with two foot pedals.
<kbd>Mod</kbd> is <kbd>Ctrl</kbd> on Windows and Linux and <kbd>Cmd</kbd> on a Mac.

Shortcuts do not work while you type in a field, and not in a list, a slider, a menu or a dialog,
or during a tour. <kbd>Space</kbd> and <kbd>Enter</kbd> on a focused button still press that
button. Only <kbd>Mod</kbd>+<kbd>K</kbd> works everywhere. Single-letter shortcuts never fire
together with <kbd>Ctrl</kbd>, <kbd>Alt</kbd> or <kbd>Cmd</kbd>.

## The shortcut overview

Press <kbd>Mod</kbd>+<kbd>/</kbd> on any screen, at every level, or **Keyboard shortcuts** in the
help panel, to see the keys that work right now. The overview shows your own keys when you changed
them, and the keys of your foot pedals. At the Expert level the top bar also has a **Shortcuts**
button and buttons show their key.

## Everywhere

| Keys | Action |
|---|---|
| <kbd>Mod</kbd>+<kbd>K</kbd> | Open the command palette |
| <kbd>?</kbd> or <kbd>F1</kbd> | Help for the current screen |
| <kbd>Mod</kbd>+<kbd>/</kbd> | The shortcut overview |
| <kbd>Esc</kbd> | Close the panel, palette or tour |
| <kbd>G</kbd> then <kbd>I</kbd> | Go to Import |
| <kbd>G</kbd> then <kbd>T</kbd> | Go to Tracks |
| <kbd>G</kbd> then <kbd>M</kbd> | Go to Mixes |

For the <kbd>G</kbd> sequences, press the second key within one second.

## In the mixer

| Keys | Action |
|---|---|
| <kbd>Space</kbd> | Play or pause (not while a button or field has focus) |
| <kbd>Home</kbd> | Stop and return to the start |
| <kbd>←</kbd> / <kbd>→</kbd> | Seek 5 seconds back or forward (with <kbd>Shift</kbd>: 1 second) |
| <kbd>[</kbd> / <kbd>]</kbd> | Set loop point A / B |
| <kbd>L</kbd> | Loop on or off |
| <kbd>Shift</kbd>+<kbd>L</kbd> | Loop the section that is playing (chapter 7) |
| <kbd>N</kbd> / <kbd>Shift</kbd>+<kbd>N</kbd> | Next / previous section |
| <kbd>Backspace</kbd> | Back to the start of the section |
| <kbd>K</kbd> | Click (metronome) on or off |
| <kbd>Shift</kbd>+<kbd>K</kbd> | Count-in on or off |
| <kbd>-</kbd> / <kbd>=</kbd> (or <kbd>+</kbd>) | Practice speed −5 % / +5 % |
| <kbd>,</kbd> / <kbd>.</kbd> | Transpose −1 / +1 semitone |
| <kbd>0</kbd> | Original speed and key |
| <kbd>T</kbd> | Speed trainer on or off (Advanced) |
| <kbd>F</kbd> / <kbd>Shift</kbd>+<kbd>F</kbd> | Score panel on or off / score follow on or off (chapter 8) |
| <kbd>R</kbd> | Record or stop recording (chapter 11) |
| <kbd>C</kbd> | Compare: my take, original, both |
| <kbd>1</kbd> … <kbd>9</kbd> | Select channel 1 to 9 |
| <kbd>M</kbd> / <kbd>S</kbd> | Mute / solo the selected channel |
| <kbd>Mod</kbd>+<kbd>S</kbd> | Save the mix |
| <kbd>Mod</kbd>+<kbd>Shift</kbd>+<kbd>S</kbd> | Save the mix as a new mix |

> **Changed keys:** the count-in moved from <kbd>C</kbd> to <kbd>Shift</kbd>+<kbd>K</kbd> and
> *Original speed and key* from <kbd>R</kbd> to <kbd>0</kbd>. <kbd>R</kbd> and <kbd>C</kbd> are
> now used for recording.

A focused fader also responds to the arrow keys and <kbd>PgUp</kbd>/<kbd>PgDn</kbd>; the
focused speed slider moves 1 % with the arrow keys and 10 % with <kbd>PgUp</kbd>/<kbd>PgDn</kbd>.

## In a setlist

While you play a setlist (chapter 9):

| Keys | Action |
|---|---|
| <kbd>Mod</kbd>+<kbd>→</kbd> | Next song |
| <kbd>Mod</kbd>+<kbd>←</kbd> | Previous song |

## In the library

| Keys | Action |
|---|---|
| <kbd>/</kbd> | Jump to **Search the library** (library pages; ignored while you type in a field) |
| <kbd>Arrow Down</kbd> | From the search box, move to the first result |
| <kbd>Esc</kbd> | Clear the search and go back to the folder you were in |

The library search is described in chapter 15.

## In the lyrics editor

The assistant keys (chapter 19) work in the lyrics editor:

| Keys | Action |
|---|---|
| <kbd>Ctrl</kbd>+<kbd>Alt</kbd>+<kbd>C</kbd> | Check the lyrics |
| <kbd>F8</kbd> / <kbd>Shift</kbd>+<kbd>F8</kbd> | Next / previous suggestion |
| <kbd>Alt</kbd>+<kbd>Enter</kbd> | Accept the suggestion |
| <kbd>Alt</kbd>+<kbd>Backspace</kbd> | Reject the suggestion |
| <kbd>Alt</kbd>+<kbd>I</kbd> | Mark the suggestion as intentional |
| <kbd>Esc</kbd> | Back to the text |

These keys belong to the lyrics editor and cannot be given to another command.

## Your own keys (Advanced)

Open **Preferences** and choose **Change shortcuts and foot pedals**.

1. Press **Change** next to a command, then press the key you want. <kbd>Esc</kbd> cancels.
2. When the key already does something else, StemStudio tells you which command uses it. Change
   that one first: one key never does two things.
3. **Clear** removes the key of a command. **Reset all shortcuts** brings back every default key.

Some keys can never be used, because the browser or a screen reader needs them: <kbd>Tab</kbd>,
<kbd>Enter</kbd>, <kbd>Esc</kbd>, <kbd>F5</kbd>, <kbd>F6</kbd>, <kbd>F7</kbd>, <kbd>F11</kbd>,
<kbd>F12</kbd>, <kbd>Insert</kbd>, <kbd>Caps Lock</kbd>, the menu key, the browser's
<kbd>Ctrl</kbd> keys for tabs, windows, reload, print, find and zoom, and keys with
<kbd>Alt</kbd>. The keys of the lyrics editor are kept for it too.

Your keys are stored in this browser, like the other preferences. When stored keys clash after an
update, the clashing ones are dropped and StemStudio says so.

## Single-key shortcuts

**Single-key shortcuts** (in Preferences, at every level) switches off every shortcut without
<kbd>Ctrl</kbd> or <kbd>Cmd</kbd>. Then a stray key press, for example from speech input, never
starts or stops the music. <kbd>Mod</kbd>+<kbd>K</kbd>, <kbd>Mod</kbd>+<kbd>S</kbd> and the other
<kbd>Mod</kbd> keys keep working.

## Foot pedals (Advanced)

A foot pedal lets you practise with both hands on your instrument. Most page-turner pedals behave
like a keyboard and send <kbd>PgUp</kbd> (left pedal) and <kbd>PgDn</kbd> (right pedal). Choose
what they do under **Foot pedals** in **Change shortcuts and foot pedals**:

| Preset | Left pedal | Right pedal |
|---|---|---|
| **Play / Back to section start** (default) | Back to the start of the part | Play or pause |
| **Next / previous section** | Previous section | Next section |
| **Next / previous song (setlist)** | Previous song | Next song |
| **Faster / slower** | Speed −5 % | Speed +5 % |
| **Custom** | Give any command a pedal yourself under the commands above | |

- When your pedal sends other keys, press **Learn left pedal** or **Learn right pedal** and press
  the pedal.
- **Pedal mode** lets the pedals work while a slider or button has the keyboard focus. In a text
  field they never do.
- Holding a pedal down does not repeat the command.
- The test line below the setup shows what the last pedal press did.

The Advanced tour **Hands-free practice** shows the section keys, your own keys and pedals, and
pedal mode.

## The command palette

<kbd>Mod</kbd>+<kbd>K</kbd> opens a search box at the top of the screen. Type a few letters
to filter, move with the arrow keys, press <kbd>Enter</kbd> to run and <kbd>Esc</kbd> to
close. The palette contains:

- **Go to**: Import, Tracks, Mixes.
- **Actions** of the current screen, for example *Save mix*, *Save mix as* and *Play or
  pause* in the mixer, or *Open mixer* on a track page.
- **Tracks**: open any track by its title.
- **Mixes**: open any mix directly in the mixer.
- **Help**: open any help topic.
