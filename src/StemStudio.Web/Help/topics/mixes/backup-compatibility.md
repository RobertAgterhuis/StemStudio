---
id: backup-compatibility
title: "Which backups can be restored"
area: mixes
summary: "Backups of this and older StemStudio versions can be restored (older ones are updated); newer ones need a newer StemStudio."
anchors: ["backup-row-status-*"]
keywords: ["compatible", "version", "older backup", "newer", "migration", "not compatible", "check", "verify"]
related: [backup-restore, library-care]
reviewed: "013"
---

Each backup row says whether it can be restored:

- **Ready to restore**: made by this StemStudio version.
- **Made by an older StemStudio**: the database inside is updated to this version during the restore, before anything
  in your library changes.
- **Not compatible**: made by a newer StemStudio, or in a format this version does not know. Update StemStudio first.
- **Not a StemStudio backup**: the file in the backups folder is no StemStudio archive (or its contents list cannot be
  read).

At the Advanced and Expert levels, **Check** reads the whole archive and compares every file with the size and
fingerprint written in it, without restoring anything. A restore always does this check first.
