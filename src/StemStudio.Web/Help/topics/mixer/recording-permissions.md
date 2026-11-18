---
id: recording-permissions
title: "Microphone permission"
area: mixer
summary: "Allow the microphone, and what to do when the browser blocks it."
anchors: ["recording-permissions"]
keywords: ["permission", "microphone", "blocked", "denied", "browser", "localhost"]
related: [recording]
reviewed: "015"
---
StemStudio asks for the microphone only after you click **Allow microphone**. The browser then shows its own prompt.

- **Blocked**: click the lock or site icon in the address bar, allow the microphone for this site, then **Try again**.
- **No input found**: connect the microphone or interface and **Try again**.
- **In use**: another program (a video call, a recording program) holds the input. Close it and **Try again**.
- **Disconnected**: the input was unplugged while the panel was open; a running take is kept up to that moment.
- **Not on this computer**: browsers allow the microphone only on `http://localhost` or `http://127.0.0.1`. Open
  StemStudio on the computer it runs on to record.
- **Browser not supported**: use Chrome or Edge. Firefox may refuse interfaces that do not run at 44.1 kHz.

Closing the panel or the mixer releases the microphone.
