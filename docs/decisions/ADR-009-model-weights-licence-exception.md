# ADR-009: Model weights licence exception and detail separation models

- **Status**: Accepted
- **Date**: 2026-11-15
- **Decider**: Owner (asked by the CTO), recorded by the CTO

## Context

Research for hierarchical "detail" separation (`docs/research/detail-separation-models.md`) found:

- The Demucs README states that "the license of the weights is not clear" and that `htdemucs_6s`
  "likely" falls under CC BY-NC. StemStudio's first stage therefore never met constitution IV as
  written; `docs/THIRD-PARTY-LICENSES.md` recorded it incorrectly.
- Under a strict OSI rule only lead/backing vocals (UVR MDX-Net KARA 2) and winds (UVR
  17_HP-Wind_Inst) could be split further. Drum parts, strings, brass, synth/keys and
  acoustic/electric guitar are only available from models with NC, unconfirmed or no stated licence
  (DrumSep variants, MVSep Mega 53-stem BS-RoFormer).

## Decision

The owner chose:

1. Keep Demucs and document an exception for model weights in local, personal, non-commercial use
   (constitution 1.1.0, principle IV).
2. For detail separation, allow every available model, including weights without a stated licence,
   subject to the conditions in principle IV (listed with status, URL and SHA-256; downloaded at
   first use, never committed; commercial use and image redistribution not covered; explicitly
   forbidden uses excluded).

Code and libraries stay OSI-only. In particular `python-audio-separator` is not installed as a
package because its hard dependency `diffq` is CC BY-NC (a library, not weights); MIT inference code
is vendored instead where needed.

## Consequences

- Feature 002 (detail separation) may use the best available models per split.
- README and THIRD-PARTY-LICENSES carry a clear "not for commercial use or redistribution" notice.
- If StemStudio is ever distributed or used commercially, every model in the exception list must be
  re-evaluated or replaced.
