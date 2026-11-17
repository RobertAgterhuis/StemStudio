---
id: backup-restore
title: "Restoring a backup"
area: mixes
summary: "Restore a backup into an empty library, replace your library (the current one is kept aside and can come back), or add what is missing."
anchors: ["restore-dialog", "previous-library"]
keywords: ["restore", "replace", "merge", "undo restore", "previous library", "discard", "empty library", "active jobs"]
related: [backup-compatibility, backup-folder, backup-voice-data, library-care]
reviewed: "013"
---

Press **Restore** on a backup row. StemStudio first checks the **whole archive** (every file against its size and
fingerprint, the database, the layout); nothing in your library changes before that check has passed. Then it switches
to the restored library in one step.

- **Empty library**: the backup is simply restored.
- **Replace my library**: your current library is **moved aside, not deleted**. Type `REPLACE` to confirm. After the
  restore the Backups tab shows the **previous library** with **Undo restore** (switches back) and **Discard** (deletes
  it for good; type `DISCARD`). Only one previous library is kept: undo or discard it before the next replace.
- **Add what is missing** (Advanced and Expert): items of the backup that your library does not have are added; items
  you already have are kept as they are. The report lists what was skipped or renamed.

A restore needs a quiet library: when an import, a split, an export, sheet music, a lyrics alignment, a generation or
an agent run is still busy, the dialog lists them with links; wait for them or cancel them first. The library volume
also needs free space for the unpacked backup.

After a restore on another computer, the first **Split further** or **Align lyrics** of a track can take a moment
longer: the processing worker first rebuilds its copy of the track's stems.
