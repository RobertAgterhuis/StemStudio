# ADR-011: GPU vendor runtime exception

- **Status**: Accepted
- **Date**: 2026-11-15
- **Decider**: Owner (asked by the CTO), recorded by the CTO

## Context

Feature 003 (GPU acceleration) needs CUDA-enabled PyTorch (and onnxruntime-gpu) on the reference
machine's NVIDIA RTX 5070 Ti (Blackwell, sm_120). These wheels ship NVIDIA's CUDA runtime
libraries (cuDNN, cuBLAS, ...), which are free but proprietary, so constitution IV (OSI-only)
would forbid them. Research: `docs/research/roadmap-003-011.md`.

## Decision

The owner allows the NVIDIA CUDA runtime libraries as a system dependency, treated like the GPU
driver, only in the optional GPU image variant (constitution 1.2.0, principle IV). The default
CPU image remains fully open source and every feature keeps working on the CPU.

## Consequences

- Two worker image variants: CPU (default, CI) and GPU (opt-in via the compose override).
- `docs/THIRD-PARTY-LICENSES.md` lists the CUDA runtime under a separate "system dependencies
  (GPU variant only)" heading.
- CI keeps testing the CPU path; the GPU path is validated on the reference machine.
