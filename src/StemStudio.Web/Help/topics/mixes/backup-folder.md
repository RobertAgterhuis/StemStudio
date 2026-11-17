---
id: backup-folder
title: "The backups folder"
area: mixes
summary: "Backups are written to a folder on your computer (./backups next to StemStudio); copy archives from there to an external drive."
anchors: ["backup-folder-state", "backup-folder-error"]
keywords: ["backups folder", "folder", "STEMSTUDIO_BACKUP_HOST_DIR", "external drive", "download", "permission", "not writable"]
related: [library-care, backup-contents, backup-restore]
reviewed: "013"
---

StemStudio writes every backup into the **backups folder**. With Docker this is the folder `./backups` next to the
`docker-compose.yml` file, unless `STEMSTUDIO_BACKUP_HOST_DIR` names another folder. The Backups tab shows the folder
and its free space.

- **Keep a copy elsewhere.** A backup on the same disk does not help when that disk fails. Copy the archive files to
  an external drive or another computer, or use **Download** on a backup row.
- **Restore from a file you copied.** Put the archive into the backups folder; it appears in the list.
- **"Not available" or "read-only"**: the folder does not exist or StemStudio may not write to it. Create the folder
  and make it writable. On Linux the API runs as user 1654: `sudo chown 1654 ./backups`. Then reload the page.

Archives are large (often many GB): they are never sent through the browser while they are being written, and a
download can resume when it is interrupted.
