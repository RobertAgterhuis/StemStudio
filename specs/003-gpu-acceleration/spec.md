# Feature Specification: GPU Acceleration

**Feature Branch**: `003-gpu-acceleration`

**Created**: 2026-11-15

**Status**: Draft

**Input**: User description: "Use the NVIDIA GPU (RTX 5070 Ti, 16 GB) of the reference machine to
speed up standard separation, detail separation and later generative models, with automatic CPU
fallback. Owner request: right after 002 (docs/ROADMAP.md)."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Faster separation on a GPU machine (Priority: P1)

A musician with an NVIDIA GPU starts StemStudio with the GPU option. Imports are separated into the
standard stems and then into detail stems much faster than on the CPU, without any other change in
how the application is used.

**Why this priority**: Detail separation takes about 26 minutes per 4 minutes of audio on the CPU;
this is the main pain point and the precondition for the generative features in the roadmap.

**Independent Test**: Start the stack with the GPU option, import the reference song, and compare
the standard and detail separation times with the CPU baseline recorded in `docs/STATUS.md`.

**Acceptance Scenarios**:

1. **Given** the stack runs with the GPU option on a machine with a supported NVIDIA GPU, **When** a
   song is imported, **Then** standard and detail separation run on the GPU and the track page shows
   that the GPU is used.
2. **Given** the reference song (4:22), **When** it is processed on the GPU, **Then** standard plus
   detail separation complete at least 5 times faster than the CPU baseline.
3. **Given** a GPU run, **When** the results are compared with a CPU run of the same song, **Then**
   the same parents are split into the same detail stems (outcome per parent identical), and the
   reconstruction check still passes.

---

### User Story 2 - Works everywhere, falls back automatically (Priority: P1)

Without the GPU option, or on a machine without a usable NVIDIA GPU, StemStudio behaves exactly as
before on the CPU. If the GPU option is chosen but the GPU is unavailable or runs out of memory
for a job, the job continues on the CPU instead of failing.

**Why this priority**: StemStudio must keep working on any machine and in CI (no GPU there).

**Independent Test**: Run the default stack (CPU) and the full test suite unchanged; simulate an
unavailable GPU and an out-of-memory error and check that jobs complete on the CPU with a notice.

**Acceptance Scenarios**:

1. **Given** the default start command, **When** the stack runs, **Then** everything works on the
   CPU as in 002 and the health information reports the CPU device.
2. **Given** the GPU option on a machine without a usable GPU, **When** the worker starts, **Then**
   it starts on the CPU and reports why the GPU is not used.
3. **Given** a GPU job that runs out of GPU memory, **When** the error occurs, **Then** that step is
   retried once on the CPU and the job completes; the track page notes the fallback.

---

### User Story 3 - See which device is used (Priority: P3)

The user can see whether processing runs on the GPU or the CPU (application health and track
detail), so slow processing can be explained.

**Why this priority**: Transparency; small.

**Independent Test**: Open the track page during and after processing in both modes and read the
device indication.

**Acceptance Scenarios**:

1. **Given** processing on the GPU, **When** the user views the track page, **Then** the device
   "GPU (<name>)" is shown for the job; on the CPU "CPU" is shown.

### Edge Cases

- The GPU is busy with another program (e.g. a game or a local LLM): free memory is checked before
  a heavy step; if insufficient, the step runs on the CPU.
- The GPU driver is too old for the bundled CUDA runtime: the worker falls back to the CPU and
  reports the reason.
- Two heavy GPU steps never run at the same time (one GPU job at a time).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST offer an optional GPU mode for the worker, enabled by a single
  documented start option, while the default start remains CPU-only.
- **FR-002**: In GPU mode, standard separation and all detail models MUST run on the GPU when it is
  available.
- **FR-003**: The worker MUST detect at start-up whether a usable GPU exists and otherwise run on the
  CPU, reporting the reason in its health information.
- **FR-004**: When a GPU step fails because of insufficient GPU memory, the system MUST retry that
  step once on the CPU; other GPU errors fail the job with a readable message as before.
- **FR-005**: At most one GPU-heavy step MUST run at a time.
- **FR-006**: The device used (GPU name or CPU, and whether a CPU fallback happened) MUST be
  recorded per job and shown on the track page and in the health information.
- **FR-007**: Separation outcomes MUST be equivalent between GPU and CPU: same per-parent outcome
  for the reference song; reconstruction check passes.
- **FR-008**: The CPU image and CI MUST remain free of the NVIDIA runtime libraries (constitution
  1.2.0); the GPU variant lists them as system dependencies in the licence document.
- **FR-009**: The fix of the broken GPU build configuration (wrong CUDA wheel variant) is part of
  this feature.

### Key Entities

- **Device info**: device kind (cpu/gpu), GPU name, total/free memory at start, fallback reason.
- **Job device record**: per standard and detail job: device used, fallback happened (yes/no).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On the reference machine, the reference song's standard plus detail separation
  completes at least 5 times faster in GPU mode than the CPU baseline (CPU: standard ~2.7 min,
  detail ~28.8 min for 4:22).
- **SC-002**: With the default start, 100 % of the existing automated tests pass unchanged.
- **SC-003**: Per-parent detail outcomes of the reference song are identical between GPU and CPU.
- **SC-004**: A simulated out-of-memory error results in a completed job (CPU fallback) in 100 % of
  test runs.

## Assumptions

- Reference machine: NVIDIA GeForce RTX 5070 Ti (16 GB, Blackwell sm_120), driver 610.88, Windows
  11 + Docker Desktop (WSL2). Other NVIDIA GPUs with a current driver should work; AMD/Intel GPUs
  and macOS are out of scope.
- About 3 GB of GPU memory is used by the desktop; usable memory is ~12.5 GB.
- Owner decision 2026-11-15 (ADR-011): NVIDIA CUDA runtime libraries are allowed in the GPU image
  variant only.
- Generative models (009/010) and the agent's local LLM (011) will reuse the GPU policy introduced
  here (one GPU job at a time, memory check, CPU fallback where feasible).
