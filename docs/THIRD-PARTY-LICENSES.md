# Third-party licences

StemStudio uses only open-source components with OSI-approved licences (constitution IV). This
file is CTO-owned and consolidated from the agents' hand-off notes. Exact versions are pinned in
`Directory.Packages.props`, `worker/uv.lock`, `src/audio-engine/package-lock.json`,
`e2e/package-lock.json`, `tools/quality/` and `dotnet-tools.json`. CI produces a machine-generated
licence report (pip-licenses, nuget-license, license-checker) as a build artifact.

## Runtime: .NET (API + Web)

| Component | Version | Licence | Source |
|---|---|---|---|
| .NET 10 runtime / ASP.NET Core | 10.0 | MIT | https://github.com/dotnet/aspnetcore |
| Microsoft.EntityFrameworkCore.Sqlite (+ Design) | 10.0.15 | MIT | https://github.com/dotnet/efcore |
| SQLite (bundled via SQLitePCLRaw) | 3.x | Public domain | https://sqlite.org/copyright.html |
| Microsoft.AspNetCore.OpenApi | 10.0.15 | MIT | https://github.com/dotnet/aspnetcore |
| Microsoft.Extensions.Http.Resilience | 10.11.0 | MIT | https://github.com/dotnet/extensions |
| Radzen.Blazor | 13.1.0 | MIT | https://github.com/radzenhq/radzen-blazor |

## Runtime: Python worker

| Component | Version | Licence | Source |
|---|---|---|---|
| FastAPI | 0.163.0 | MIT | https://github.com/fastapi/fastapi |
| uvicorn[standard] | 0.23.2 | BSD-3-Clause | https://github.com/encode/uvicorn |
| python-multipart | 0.0.33 | Apache-2.0 | https://github.com/Kludex/python-multipart |
| pydantic | 2.14.3 | MIT | https://github.com/pydantic/pydantic |
| Demucs (code) | 4.1.0 | MIT | https://github.com/adefossez/demucs |
| Demucs pretrained weights (`htdemucs_6s`, `htdemucs`) | - | Released with the MIT-licensed Demucs repository | https://github.com/adefossez/demucs |
| PyTorch (CPU build) | 2.15.0+cpu | BSD-3-Clause (bundled parts Apache-2.0/MIT/Zlib/BSL-1.0) | https://github.com/pytorch/pytorch |
| librosa | 1.0.1 | ISC | https://github.com/librosa/librosa |
| soundfile (+ libsndfile) | 0.14.0 | BSD-3-Clause (libsndfile LGPL-2.1+) | https://github.com/bastibe/python-soundfile |
| NumPy | 2.5.4 | BSD-3-Clause | https://github.com/numpy/numpy |
| yt-dlp | 2026.10.26 | Unlicense | https://github.com/yt-dlp/yt-dlp |
| spotDL | 4.6.0 | MIT | https://github.com/spotDL/spotify-downloader |
| FFmpeg / ffprobe (Debian package, separate executable) | distro | LGPL-2.1+ / GPL-2.0+ (Debian build) | https://ffmpeg.org/legal.html |
| Deno (JS runtime required by yt-dlp) | 2.9.8 | MIT | https://github.com/denoland/deno |

### Copyleft transitive dependencies (reviewed and accepted)

All are OSI-approved. They are used unmodified, as separate libraries or executables, in a tool
that runs locally for a single user; StemStudio is not redistributed as a closed binary.

| Component | Licence | Pulled in by |
|---|---|---|
| lameenc 1.9.0 | LGPL-3.0+ | demucs |
| soxr 1.1.0 | LGPL-2.1+ | librosa |
| mutagen 1.48.1 | GPL-2.0+ | spotdl |
| unidecode 1.4.0 | GPL-2.0+ | spotdl |
| text-unidecode 1.3 | Artistic-1.0 / GPL | spotdl |
| pykakasi 2.3.0 | GPL-3.0+ | spotdl |
| spotapi 1.2.8 | GPL-3.0 | spotdl (unofficial Spotify web client, metadata only) |
| certifi, pathspec | MPL-2.0 | requests stack / lizard |

**Distribution note:** anyone who redistributes the worker image must comply with the GPL/LGPL
terms of these components (offer source, keep notices).

## Frontend audio engine (build-time only; the shipped bundle contains only StemStudio code)

| Component | Version | Licence |
|---|---|---|
| TypeScript | 6.0.3 | Apache-2.0 |
| Vite | 8.3.8 | MIT |
| Vitest + @vitest/coverage-v8 | 5.1.0 | MIT |
| ESLint, @eslint/js | 10.15.0 / 10.0.1 | MIT |
| typescript-eslint | 8.72.2 | MIT |
| eslint-plugin-jsdoc | 65.4.2 | BSD-3-Clause |
| jsdom | 31.0.1 | MIT |
| globals, @types/node | 17.14.0 / 22.20.5 | MIT |

## Test and quality tooling (not shipped)

| Component | Version | Licence |
|---|---|---|
| xunit.v3, xunit.runner.visualstudio | 4.0.3 / 4.0.1 | Apache-2.0 |
| Microsoft.NET.Test.Sdk | 18.12.0 | MIT |
| coverlet.collector / coverlet.msbuild | 10.1.0 | MIT |
| bUnit | 2.15.2 | MIT |
| Microsoft.AspNetCore.Mvc.Testing, TimeProvider.Testing | 10.0.15 / 10.11.0 | MIT |
| NSubstitute (+ Analyzers) | 6.2.0 / 1.0.17 | BSD-3-Clause / MIT |
| Shouldly | 4.3.0 | BSD-3-Clause |
| YamlDotNet | 18.1.0 | MIT |
| dotnet-ef (local tool) | 10.0.15 | MIT |
| dotnet-sonarscanner (local tool, CI only, not shipped) | 12.0.0 | LGPL-3.0 |
| pytest, pytest-cov, pytest-timeout | 9.2.0 / 7.1.0 / 2.4.0 | MIT |
| httpx, jsonschema, PyYAML | 0.28.1 / 4.26.0 / 6.0.3 | BSD-3-Clause / MIT / MIT |
| ruff, mypy (+ types-* stubs) | 0.17.5 / 2.4.1 | MIT (stubs Apache-2.0) |
| lizard | pinned in tools/quality | MIT |
| jscpd (via npx) | pinned in tools/quality | MIT |
| @playwright/test (+ browsers) | 1.65.1 | Apache-2.0 |
| eslint-plugin-playwright | 2.12.2 | MIT |
| @redocly/cli (via npx) | 2.78.0 | MIT |

## Rejected for licence reasons

| Component | Reason |
|---|---|
| FluentAssertions 8.x | Commercial licence (Xceed) since v8 |
| JsonSchema.Net 9.x | OSMF EULA, not OSI-approved |

## Container base images

| Image | Licence notes |
|---|---|
| `python:3.12-slim` (Debian) | Debian packages under their respective free licences |
| `mcr.microsoft.com/dotnet/aspnet:10.0` / `sdk:10.0` | MIT (.NET), Debian packages |
