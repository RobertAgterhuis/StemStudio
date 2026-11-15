"""CPU-image guard (feature 003, FR-008, ADR-011; 006 T011, 008; 009 T010): runs INSIDE an image.

Profiles (first argument, default ``worker``):

* ``worker`` - the default worker image: fails when it holds any NVIDIA/CUDA runtime (a
  distribution named ``nvidia-*`` except the pure-Python ``nvidia-ml-py``, ``cuda-*``, ``triton``
  or ``onnxruntime-gpu``) or a torch build with CUDA (``torch.version.cuda`` is not None); when a
  distribution the lock excludes by override is present (``matplotlib``, 008 Phase 0); or when a
  required module does not import (``torchaudio``, ``stable_whisper`` for 006).
* ``generator`` - the fake generator image (009/010, ``GENERATOR_VARIANT=fake``): the same
  NVIDIA/CUDA rules, and torch, the ACE-Step packages and any model stack must be absent (base
  install of ``generator/uv.lock`` only); the generator package and its web stack must import.

Prints one line per finding. Used by ``scripts/check-cpu-image.{sh,ps1}``, which pipe this file
into ``docker run --rm -i --entrypoint python <image> -I - [profile]``. Standard library (plus
torch for the worker profile) only.
"""

import importlib
import importlib.metadata
import sys
from dataclasses import dataclass

ALLOWED = frozenset({"nvidia-ml-py"})
FORBIDDEN_NAMES = frozenset({"triton", "onnxruntime-gpu", "matplotlib"})
FORBIDDEN_PREFIXES = ("nvidia-", "cuda-")
MODEL_STACK = frozenset(
    {"torch", "torchaudio", "torchvision", "ace-step", "nano-vllm", "transformers", "diffusers"}
)


@dataclass(frozen=True)
class Profile:
    """What one image kind must not contain and must be able to import."""

    forbidden_names: frozenset[str]
    required_modules: tuple[str, ...]
    torch_expected: bool
    summary: str


PROFILES = {
    "worker": Profile(
        FORBIDDEN_NAMES,
        ("torchaudio", "stable_whisper"),
        torch_expected=True,
        summary="no NVIDIA/CUDA distributions, CPU torch, no matplotlib",
    ),
    "generator": Profile(
        FORBIDDEN_NAMES | MODEL_STACK,
        ("stemstudio_generator.config", "stemstudio_generator.schemas", "fastapi", "uvicorn",
         "soundfile", "pynvml"),
        torch_expected=False,
        summary="no NVIDIA/CUDA distributions, no torch or model stack, no matplotlib",
    ),
}  # fmt: skip


def normalise(name: str) -> str:
    """Return a distribution name in lower case with ``_`` and ``.`` replaced by ``-``."""
    return name.lower().replace("_", "-").replace(".", "-")


def is_forbidden(name: str, profile: Profile) -> bool:
    """Return True when the normalised distribution ``name`` must not be in the image."""
    if name in ALLOWED:
        return False
    return name in profile.forbidden_names or name.startswith(FORBIDDEN_PREFIXES)


def forbidden_distributions(profile: Profile) -> list[str]:
    """Return the sorted names of installed distributions the profile forbids."""
    names = {normalise(dist.metadata["Name"] or "") for dist in importlib.metadata.distributions()}
    return sorted(name for name in names if is_forbidden(name, profile))


def torch_cuda_version() -> str | None:
    """Return ``torch.version.cuda`` (None for a CPU build)."""
    import torch  # noqa: PLC0415 - imported late so the distribution scan reports first

    return torch.version.cuda


def import_failure(module: str) -> str | None:
    """Return why ``module`` does not import, or None when it imports."""
    try:
        importlib.import_module(module)
    except Exception as error:  # any import failure is a finding
        return f"module {module} does not import: {type(error).__name__}: {error}"
    return None


def findings(profile: Profile) -> list[str]:
    """Return one line per forbidden distribution, CUDA torch build or missing module."""
    lines = [f"forbidden distribution: {name}" for name in forbidden_distributions(profile)]
    cuda = torch_cuda_version() if profile.torch_expected else None
    if cuda is not None:
        lines.append(f"torch is a CUDA build (torch.version.cuda = {cuda})")
    lines.extend(filter(None, map(import_failure, profile.required_modules)))
    return lines


def main(argv: list[str]) -> int:
    """Print the findings of the profile named in ``argv``; 0 when the image passes, else 1."""
    name = argv[0] if argv else "worker"
    profile = PROFILES.get(name)
    if profile is None:
        sys.stdout.write(f"FAIL unknown profile {name!r}; use one of {', '.join(PROFILES)}\n")
        return 2
    lines = findings(profile)
    sys.stdout.writelines(f"FAIL {line}\n" for line in lines)
    if lines:
        return 1
    modules = ", ".join(profile.required_modules)
    sys.stdout.write(f"OK   CPU-only {name} image: {profile.summary}; {modules} import\n")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
