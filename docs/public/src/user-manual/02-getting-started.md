# Getting started

StemStudio runs on your own computer. Nothing is uploaded to a cloud service: the songs you
import, the stems and your mixes stay in local storage. It is made for one person practising
on one machine, so there are no accounts or passwords.

## What you need

- A computer with **Docker** (Docker Desktop on Windows or macOS, Docker Engine with
  Compose v2 on Linux).
- About 5 GB of free disk space for the standard installation, plus room for your songs.
  Each imported song with its stems takes roughly 50 to 150 MB.
- For detail separation (splitting drums into kick, snare and so on): Docker must be
  allowed to use at least **20 GB of memory**. In Docker Desktop this is under
  *Settings → Resources*.
- Optional: an **NVIDIA graphics card** (GPU) for much faster processing. See section 2.3.
- A current version of Chrome, Edge or Firefox. Safari works on a best-effort basis.
- Optional, for play-along recording (chapter 11): a microphone or an audio interface,
  headphones, and Chrome or Edge on the computer that runs StemStudio.

## Start StemStudio with one command

Open a terminal in the StemStudio folder and run:

```
docker compose up --build
```

The first start builds the images and takes several minutes. When the terminal shows that
all three services are healthy, open **http://localhost:8080** in your browser.

- To stop StemStudio press <kbd>Ctrl</kbd>+<kbd>C</kbd> in the terminal, or run
  `docker compose down`. Your songs, stems and mixes are kept.
- To start again later, run `docker compose up` (without `--build` unless you updated
  StemStudio).

### Only this computer can open StemStudio

StemStudio answers only on this computer: **http://localhost:8080** works here, but a
phone, tablet or other computer in your network cannot open it. That is on purpose: your
library, backups and the AI agent (with your Claude key) stay out of reach of other devices,
for example on a shared or public Wi-Fi network.

If you really want to reach StemStudio from another device, set `STEMSTUDIO_BIND_ADDRESS`
before you start it, for example `STEMSTUDIO_BIND_ADDRESS=0.0.0.0 docker compose up`
(or uncomment the line in your `.env` file). Also add the address the other device uses to
`STEMSTUDIO_API_ALLOWED_HOSTS`, for example
`STEMSTUDIO_API_ALLOWED_HOSTS=localhost;127.0.0.1;[::1];api;192.168.1.10`; without it
the API (port 8081) answers that device with *Bad Request*. Only do this on a network you trust: every
device in that network can then reach your songs and backups, and can replace or delete the
Claude key you stored in Settings. Song playback on the other device needs more set-up (see the
technical manual, "Network exposure"); a supported tablet remote is planned. Remove the
settings and start again with `docker compose up -d` to close it. Recording (chapter 11) and
entering the Claude key (chapter 21) still work only on the computer itself.

> **Important:** `docker compose down -v` deletes **all** your tracks, stems, mixes and the
> downloaded models. Only use it when you really want to start from scratch.

The first real separation downloads the separation model (a few hundred MB). The first
detail separation downloads about 1.9 GB of additional models. After that, StemStudio
works without an internet connection for everything except importing from YouTube or
Spotify.

## Faster processing with an NVIDIA GPU

With a supported NVIDIA graphics card, separating a 4-minute song including detail stems
takes a few minutes instead of half an hour (measured: 3.7 minutes on the GPU against
31.5 minutes on the CPU for the reference song).

Requirements:

- An NVIDIA GPU of the Turing generation or newer (compute capability 7.5 or higher).
- A current NVIDIA driver on the host (R580 or newer).
- Windows 11: Docker Desktop with the WSL2 back end. Do **not** install a separate Linux
  NVIDIA driver inside WSL. Linux: the NVIDIA Container Toolkit.

Start StemStudio with the GPU option:

```
docker compose -f docker-compose.yml -f docker-compose.gpu.yml up --build
```

The GPU image is larger (about 11.5 GB) because it contains the NVIDIA CUDA libraries.
StemStudio checks the GPU when it starts. If anything is wrong (no GPU, old driver, the
card is busy with a game or another AI program), it simply uses the CPU and tells you why
on the import screen under **Processing device**. You never lose a job because of the GPU.

The generative features (style transfer and songs from your own lyrics, chapters 18 and 19)
need an NVIDIA GPU with enough free memory and are started with an extra compose option
(see the technical manual, "Generative services").

## Your first visit

When you open StemStudio for the first time, a welcome panel asks how familiar you are with
practice tools:

- **I am new to this**: the Beginner level with guidance and presets.
- **I know my way around**: the Advanced level with the full set of controls.
- **Give me everything**: the Expert level with numeric entry and keyboard-first work.

You can also **Take a one-minute tour** of the import screen, or choose **Not now**. The
level only changes which controls and aids you see; you can switch at any time (chapter 3).

## Trying StemStudio without waiting

For a quick look at the screens you can start StemStudio with a fake separator that
"separates" instantly by copying the song into every stem:

```
STEMSTUDIO_SEPARATOR=fake docker compose up --build
```

The stems then all sound the same, so this mode is only useful for exploring the interface.
The song sections (chapter 7) are then fixed parts of eight bars instead of a real detection.
