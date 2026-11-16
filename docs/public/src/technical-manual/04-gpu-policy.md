# GPU policy

The GPU is an accelerator, never a requirement for the core product. The policy is fixed in
ADR-011 (vendor runtime) and ADR-012 (device policy); generative services extend it in
ADR-018.

## Image variants (D1)

One Dockerfile builds a CPU and a GPU worker image. CI builds and tests only the CPU image
and fails when it contains NVIDIA or CUDA wheels (`scripts/check-cpu-image.*`, CI job
`cpu-image-guard`). The proprietary CUDA runtime is allowed only in the optional GPU image
(ADR-011); the default image stays fully open source.

## Start-up probe (D2)

The worker probes the GPU once at start-up and never fails to start because of it. With
`STEMSTUDIO_DEVICE=cpu` CUDA is not touched. The first failing check becomes the reason in
`/health` (`deviceInfo.reason`) and on the import screen:

1. torch built with CUDA, else *"This worker image has no GPU support (CPU build); use the GPU
   start option."*
2. CUDA available, else *"No usable NVIDIA GPU: CUDA is not available in this container …"*
3. Architecture supported by the build, else *"The GPU … is not supported by this build."*
4. A 256×256 matrix-multiply test kernel, else *"The GPU test kernel failed: …"*
5. NVML readable, else *"GPU memory cannot be read (NVML unavailable)."*

## Scheduling and memory (D3 to D6)

- **One GPU step at a time** (GPU lease).
- **Free-memory check** before every step: NVML free memory must cover the step's need plus
  a 1,024 MiB margin (htdemucs_6s 1,700 MiB, DrumSep 4,400, KARA 2 3,600, Mega 53 10,600).
  Otherwise the step runs on the CPU with the reason shown to the user.
- **Out of memory**: the step is repeated once on the CPU. Other GPU errors fail the job.
- **Model residency**: GPU models are released when the worker is idle; the worker reclaims
  its own cached models before deciding on a memory fallback.
- **Numerics**: float32 only.

`GET /api/device` exposes the worker's device information (cached up to 10 s; unreachable
after 2 s). Measured on an RTX 5070 Ti: standard plus detail separation of a 4:22 song in
3.7 minutes against 31.5 minutes on the CPU.

## Cross-container lease

From feature 009 several containers share one GPU. A `flock` lease on
`/gpu-lease/gpu.lock` in the shared `gpu-lease` volume serialises the GPU worker and the
generator. The generator selects a memory profile from NVML (standard, else low-memory, else
wait up to 600 s), caps its memory fraction at free memory minus 256 MiB, retries once on
out-of-memory and unloads models when idle. The worker waits up to 900 s for the lease and
then falls back to the CPU with *"GPU busy with another StemStudio job"*. Generative jobs
have **no CPU fallback**; without a usable GPU the API answers `409 gpuRequired`.

Lyrics alignment (006) and the accurate piano transcription (008) use the worker's device
runner with the same policy. The agent (011) asks Ollama to release its model
(`keep_alive: 0`, 2 s timeout; the job starts anyway) before submitting GPU-heavy jobs, from
the UI or from the agent, and agent turns use the fallback model `qwen3.5:4b` on the CPU
(`num_gpu` 0) while such a job runs. Ollama runs outside StemStudio's GPU lease.
