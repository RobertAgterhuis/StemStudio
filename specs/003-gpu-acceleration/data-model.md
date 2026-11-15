# Data Model: GPU Acceleration

Builds on [001 data-model](../001-stemstudio-mvp/data-model.md) and
[002 data-model](../002-detail-separation/data-model.md). Contracts:
[worker-api 1.2.0](contracts/worker-api.openapi.yaml), [backend-api 1.3.0](contracts/backend-api.openapi.yaml),
[ui-test-ids 1.2.0](contracts/ui-test-ids.md). Design: [ADR-012](../../docs/decisions/ADR-012-gpu-device-policy.md).

## 1. Configuration (worker environment)

| Variable | Values | Default | Meaning |
|---|---|---|---|
| `STEMSTUDIO_DEVICE` | `cpu`, `cuda`, `auto` | `cpu` | `cpu` never touches CUDA. `cuda` and `auto` behave the same since 003: use the GPU when the start-up probe passes, otherwise the CPU with a reason (FR-003). The GPU compose override sets `auto`. |
| `STEMSTUDIO_FAKE_DEVICE` | `cpu`, `gpu`, `gpu-oom` | `cpu` | Only with `STEMSTUDIO_SEPARATOR=fake`: the fake separator and fake detail models report a fake GPU (`Fake GPU`); `gpu-oom` makes the first model step of every job "run out of GPU memory" and fall back to the CPU (E2E of the fallback notice, SC-004). |
| `PYTORCH_CUDA_ALLOC_CONF` | torch syntax | unset | GPU image only, optional (`expandable_segments:True` reduces fragmentation; not required by the measured peaks). |

Build arguments of `docker/worker.Dockerfile` (devops): `TORCH_VARIANT` (`cpu` default, `cu132` for
the GPU image) and `ORT_VARIANT` (`cpu` default, `gpu` replaces `onnxruntime` by
`onnxruntime-gpu==1.32.0` and adds `cuda-toolkit[curand]==13.2.2`). The CPU image is built exactly from `worker/uv.lock` and contains no
NVIDIA package (constitution IV, ADR-011).

## 2. Worker entities

### 2.1 DeviceInfo (start-up probe, `GET /health` -> `deviceInfo`)

| Field | Type | Rule |
|---|---|---|
| `kind` | `cpu` \| `gpu` | `gpu` only when a GPU was requested and every probe step passed |
| `name` | string \| null (<= 200) | NVML/torch device name, e.g. `NVIDIA GeForce RTX 5070 Ti`; null on the CPU |
| `computeCapability` | `"<major>.<minor>"` \| null | e.g. `12.0` |
| `totalMemoryMiB` | int \| null | NVML total |
| `freeMemoryMiB` | int \| null | NVML free at start-up |
| `reason` | string \| null (<= 500) | why the CPU is used although a GPU was requested; null otherwise |

Probe order (first failure wins, its text becomes `reason`; ADR-012 D2):

| # | Check | Reason text when it fails |
|---|---|---|
| 1 | `STEMSTUDIO_DEVICE` is `cpu` | (none; `kind=cpu`, `reason=null`) |
| 2 | torch imports and was built with CUDA (`torch.version.cuda` not None) | `This worker image has no GPU support (CPU build); use the GPU start option.` |
| 3 | `torch.cuda.is_available()` | `No usable NVIDIA GPU: CUDA is not available in this container (is the GPU option enabled and the driver current?).` |
| 4 | `sm_<major><minor>` of device 0 is in `torch.cuda.get_arch_list()` | `The GPU <name> (compute capability <x.y>) is not supported by this build.` |
| 5 | a 256x256 matmul on `cuda` returns a finite result | `The GPU test kernel failed: <first line of the error, <= 200 chars>.` (covers "driver too old" / "no kernel image") |
| 6 | NVML reads total/free memory | `GPU memory cannot be read (NVML unavailable).` |

The probe runs once in the composition root; the result is immutable for the worker's lifetime.
`onnxruntime` CUDA availability is not part of the probe: when the CUDA execution provider cannot
be created, the KARA 2 step runs on the CPU and records a fallback (2.3).

### 2.2 JobDevice (per standard job and per detail job)

Wire: `Job.device`, `DetailJob.device` (worker 1.2.0); `Track.device`, `DetailPhase.device`
(backend 1.3.0). Persisted in the worker job JSON (`jobs/*.json`, `detail-jobs/*.json`) and in
the backend database.

| Field | Type | Rule |
|---|---|---|
| `kind` | `cpu` \| `gpu` | `gpu` once any model step of the job ran on the GPU |
| `name` | string \| null | GPU name of the first GPU step; null when no step ran on the GPU |
| `cpuFallback` | bool | true once any model step ran on the CPU on a GPU worker |
| `fallbackReason` | string \| null (<= 500) | reason of the latest fallback; null iff `cpuFallback` is false |

Fold rule (`ports.merge_device(record, use)`, implemented and tested in Phase 0): start from
`null`; per model step `use = DeviceUse(kind, torch_device, name, fallback_reason)`:
`kind = gpu if record.kind == gpu or use.kind == gpu else cpu`; `name = record.name or use.name`;
`cpuFallback = record.cpuFallback or use.fallback_reason is not null`;
`fallbackReason = use.fallback_reason or record.fallbackReason`.

Lifecycle:

- Standard job: `null` while queued/downloading; set to the planned device when the separation
  step starts (`DeviceUse` of the start), replaced by the folded record when it ends (fallback);
  unchanged during analysis; `null` for results from the cache (`by-source`/`by-audio` hit).
- Detail job: `null` while queued and during the plan unit; folded after every parent step; kept on
  completion; `null` when the whole result came from the detail cache. A job on a CPU worker gets
  `{kind: cpu, cpuFallback: false}` after its first model step (the device is shown as "CPU").
- Jobs stored before 1.2.0 load with `device = null` (field optional).

### 2.3 DeviceUse and fallback reasons (per model step, not on the wire)

| Situation | `kind` | `fallback_reason` (exact text pattern) |
|---|---|---|
| CPU worker | cpu | null |
| GPU worker, step ran on the GPU | gpu | null |
| GPU worker, NVML free < `need.memory_mib` | cpu | `Not enough free GPU memory for <label> (needs <n> MiB, <m> MiB free); ran on the CPU.` |
| GPU worker, out of GPU memory during the step, retried on the CPU | cpu | `The GPU ran out of memory during <label>; the step was repeated on the CPU.` |
| GPU worker, onnxruntime CUDA provider unavailable (KARA 2) | cpu | `ONNX Runtime has no usable CUDA provider; <label> ran on the CPU.` |

Numbers are formatted without thousands separators. Other GPU errors are not retried; they fail
the job with the existing readable texts (002 data-model 4.2) (FR-004).

### 2.4 GpuNeed (per model step; constants owned by stem-separation)

`GpuNeed(label, memory_mib)`: the free GPU memory a step needs = measured torch allocated peak of
the step on the reference song (NVML peak for onnxruntime) rounded up to 100 MiB + 1,024 MiB
margin (research.md G-04): htdemucs_6s 1,700, MDX23C DrumSep 4,400, KARA 2 3,600, Mega 53-stem
10,600 MiB. Values for the
current models: research.md G-04. The need is checked against NVML free memory right before the
step, while holding the GPU lease; resident weights of other models count as used.

## 3. Backend entities

### 3.1 Track (additions)

| Field | Type | Persistence | Rule |
|---|---|---|---|
| `Device` | `JobDevice?` (domain value object) | JSON column `Tracks.Device` (nullable, migration `AddJobDevice`) | set from the worker job's `device` on every sync while not terminal and on completion; never changed after completion; reset to null by retry |
| `DetailPhase.Device` | `JobDevice?` | inside the existing `DetailPhase` JSON column (no schema change) | set from the worker detail job's `device` on every sync; reset to null when the phase is (re)started |

Domain value object `JobDevice(Kind, Name, CpuFallback, FallbackReason)` validates: `Name`
<= 200, `FallbackReason` <= 500, `FallbackReason` non-null iff `CpuFallback`, `Name` null when
`Kind` is CPU. Invalid worker data is logged and ignored (device stays as it was); it never fails
a track.

### 3.2 ProcessingDevice (`GET /api/device`)

Not persisted. Mapped from worker `GET /health` -> `deviceInfo` (`kind`, `name`,
`totalMemoryMiB`, `reason`); `reachable=false` with null fields when the worker does not answer
within 2 s or answers with an error. May be cached for up to 10 s (`TimeProvider`).

## 4. UI (frontend)

| Place | Shows | Test ids (1.2.0) |
|---|---|---|
| Import page `/` | worker device: "GPU (<name>)", "CPU" or "Unknown"; the reason when the GPU was requested but is not used | `worker-device`, `worker-device-reason` |
| Track detail, standard section | `track.device.DisplayText()` next to the model; fallback notice with the reason | `track-device`, `track-device-fallback` |
| Track detail, detail section | `track.detail.device` likewise | `detail-device`, `detail-device-fallback` |

`JobDeviceDto.DisplayText()` (Contracts) is the single source of the texts `GPU (<name>)` / `CPU`.
