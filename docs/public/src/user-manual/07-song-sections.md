# Song sections

StemStudio finds the parts of a song for you: intro, verse, pre-chorus, chorus, bridge, solo,
instrumental and outro. They appear as a row of buttons, the **section lane**, above the mixer
panels. With one click you loop a part, and with one key you move to the next one, so you can
practise a song part by part.

## Finding the sections

Sections are found automatically when a song has been separated. For songs that were separated
before this feature, they are found the first time you open the mixer; that takes a few seconds.
While it runs, the lane says so; when it fails, the lane shows the reason and **Try again**.

How it works: StemStudio compares the separated stems over time. It looks at where the sound
changes, which parts repeat and which instrument leads. Repeating parts with vocals become verses
and choruses; a part where one instrument leads without vocals becomes a **solo** for that
instrument, for example *Guitar solo* or *Bass solo*. Everything runs on your own computer,
without a GPU and without the internet.

- Boundaries snap to **bar lines** when the tempo grid is reliable, to **beats** when it is
  uncertain, and to a tenth of a second without a grid. When the grid is uncertain the lane says
  so, because the boundaries may then be a little off.
- The labels are a guess from the music. Songs without repetition, live recordings and medleys
  get fewer or rougher sections. When nothing repeats, the parts are called *Part A*, *Part B*
  and so on instead of guesses. A song has at most 64 sections; every section is at least one
  second long.
- When a track is separated again (**Retry**), its sections are kept.

## The section lane

Each section is a button with its kind, its label and its bars, for example *Chorus 1, bars
17-24*. The button of the part that is playing is marked, and a looped section shows that it is
looped. The highlight does not depend on colour alone: it has an outline and a label. With a
screen reader the lane is a list of buttons.

| Action | How |
|---|---|
| Loop a section | Click its button. The loop goes on; when the playback was somewhere else, it jumps into the section |
| Loop the part that is playing | **Loop this section**, or <kbd>Shift</kbd>+<kbd>L</kbd> |
| Next or previous part | **Next section** (<kbd>N</kbd>) and **Previous section** (<kbd>Shift</kbd>+<kbd>N</kbd>) |
| Back to the start of the part | **Section start** (<kbd>Backspace</kbd>) |

While a section loop is on, **Next section** and **Previous section** move the loop to the
neighbouring part. That is the quickest way to work through a song: loop the intro, play it a few
times, press <kbd>N</kbd>, and so on.

## Sections and the speed trainer

A looped section is also the region of the speed trainer (chapter 5): loop the solo, switch the
trainer on (<kbd>T</kbd>, Advanced) and every pass of the solo gets a little faster. The mix
remembers which section the loop came from when you save it (chapter 6). When you move loop
point A or B by hand, the loop no longer belongs to the section; it stays where you put it.

## Editing the sections (Advanced)

Press **Edit sections** below the section lane.

- **Split at the playhead** cuts the section you are in where the playback is. The cut snaps to
  the nearest bar line (or beat) when the tempo grid allows it.
- **Merge with the next** joins two sections; the first one keeps its name and kind.
- Type a **name**, or change the **kind**. For a solo you can say which instrument plays it.
- **Starts at** moves the boundary with the section before it. Use the arrow keys for small
  steps. Every section stays at least one second long.
- **Delete** gives the time to the section before it. **Add a section** fills the rest of the
  song.

Names such as *Verse 2* are renumbered when you add or remove a verse; names you typed yourself
stay as they are.

**Done** saves all changes at once, **Cancel** throws them away. When another window saved the
sections first, StemStudio says *"The sections were changed in another window. The newer version
is loaded; make your changes again."* Your edits stay until you detect the sections again.

## Detecting again (Expert)

At the Expert level each section shows how sure the detection was, and the lane lists the
warnings of the detection (for example an uncertain tempo grid or no vocals). **Detect again**
runs the detection once more. When you edited the sections, StemStudio asks before it replaces
them.

## Levels

| Level | What you see |
|---|---|
| Beginner | The section lane, one-click loops, next, previous and section start |
| Advanced | Also **Edit sections** |
| Expert | Also the confidence per section, the detection warnings and **Detect again** |

The Beginner tour **Practise a section** shows the lane, looping the part that is playing and the
practice log.
