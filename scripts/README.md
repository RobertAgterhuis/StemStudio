# scripts/

Developer and CI scripts (owner: devops). Every `.sh` script has a `.ps1` twin; each prints its
usage with `-h` / `Get-Help`.

## Start-up

- `dev.sh` / `dev.ps1`: the non-Docker path (worker, api, web on the host; data under
  `<repo>/data`, git-ignored).
- `docker compose up --build`: the Docker path (see the header of `docker-compose.yml`).

## Backups folder (feature 013, FR-005)

Where backup archives (`stemstudio-backup-<yyyyMMdd-HHmmss>.zip`, often several GB) are written:

| Path | Folder | Set with |
|---|---|---|
| Docker | `./backups` next to `docker-compose.yml`, bind-mounted into the api at `/backups` | `STEMSTUDIO_BACKUP_HOST_DIR` (absolute, or relative to the repository) |
| Non-Docker (`dev.sh`, `dev.ps1`) | `<repo>/data/backups` (created by the script) | `StemStudio__BackupDir` (an existing value wins) |

Create the Docker folder before the first start; otherwise Docker creates it owned by root and
the api (uid 1654 on Linux hosts) cannot write it. The api never changes host permissions.

```bash
mkdir -p backups
sudo chown 1654:1654 backups            # Linux hosts only
scripts/check-backup-folder.sh          # exists, writable for the api user, free space
```

```powershell
$env:STEMSTUDIO_BACKUP_HOST_DIR = 'D:\StemStudio\backups'   # optional: another drive
New-Item -ItemType Directory -Force $env:STEMSTUDIO_BACKUP_HOST_DIR | Out-Null
./scripts/check-backup-folder.ps1
```

`backups/` is ignored by git and by the image build context. The archives are ordinary files: copy
them to another disk or computer to keep a copy elsewhere.

## Checks

| Script | Checks |
|---|---|
| `check-backup-folder.{sh,ps1}` (`backup_folder_checks.py`) | backups folder exists, is writable for the api user, has >= 256 MiB free (013) |
| `check-key-volume.{sh,ps1}` (`key_volume_checks.py`) | Claude key ring volume only in api (011 claude-key) |
| `check-library-mount.{sh,ps1}` (`library_mount_checks.py`) | worker sees the library read-only at `/library` (007) |
| `check-worker-fonts.{sh,ps1}` | Leipzig music font in the worker image (008) |
| `check-soundtouch-served.{sh,ps1}` | the web serves SoundTouch WASM and its notices (012) |
| `check-cpu-image.{sh,ps1}`, `image-licences.{sh,ps1}` | CPU image without CUDA, image licence report |
| `test-all.{sh,ps1}` | every test suite of the repository |

The Python checks are tested in `scripts/tests` (`scripts/pyproject.toml`, >= 90 % line and
branch coverage in CI).
