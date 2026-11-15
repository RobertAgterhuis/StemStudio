"""CPU-image guard (feature 003, FR-008, ADR-011; 006 T011, 008): runs INSIDE a worker image.

Fails (exit 1) when the image holds any NVIDIA/CUDA runtime: a distribution named ``nvidia-*``
(except the pure-Python ``nvidia-ml-py``), ``cuda-*``, ``triton`` or ``onnxruntime-gpu``, or a
torch build with CUDA (``torch.version.cuda`` is not None); when a distribution the lock excludes
by override is present (``matplotlib``, 008 Phase 0); or when a required module does not import
(``torchaudio``, ``stable_whisper`` for 006). Prints one line per finding.

Used by ``scripts/check-cpu-image.{sh,ps1}``, which pipe this file into
``docker run --rm -i --entrypoint python <image> -I -``. Standard library plus torch only.
"""

import importlib
import importlib.metadata
import sys

ALLOWED = frozenset({"nvidia-ml-py"})
FORBIDDEN_NAMES = frozenset({"triton", "onnxruntime-gpu", "matplotlib"})
FORBIDDEN_PREFIXES = ("nvidia-", "cuda-")
REQUIRED_MODULES = ("torchaudio", "stable_whisper")


def normalise(name: str) -> str:
    """Return a distribution name in lower case with ``_`` and ``.`` replaced by ``-``."""
    return name.lower().replace("_", "-").replace(".", "-")


def is_forbidden(name: str) -> bool:
    """Return True when the normalised distribution ``name`` is an NVIDIA/CUDA runtime."""
    if name in ALLOWED:
        return False
    return name in FORBIDDEN_NAMES or name.startswith(FORBIDDEN_PREFIXES)


def forbidden_distributions() -> list[str]:
    """Return the sorted names of installed distributions that are NVIDIA/CUDA runtimes."""
    names = {normalise(dist.metadata["Name"] or "") for dist in importlib.metadata.distributions()}
    return sorted(filter(is_forbidden, names))


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


def findings() -> list[str]:
    """Return one line per NVIDIA/CUDA runtime, excluded distribution or missing module."""
    lines = [f"forbidden distribution: {name}" for name in forbidden_distributions()]
    cuda = torch_cuda_version()
    if cuda is not None:
        lines.append(f"torch is a CUDA build (torch.version.cuda = {cuda})")
    lines.extend(filter(None, map(import_failure, REQUIRED_MODULES)))
    return lines


def main() -> int:
    """Print the findings and return 0 when the image is CPU only, otherwise 1."""
    lines = findings()
    sys.stdout.writelines(f"FAIL {line}\n" for line in lines)
    if lines:
        return 1
    sys.stdout.write(
        "OK   CPU-only image: no NVIDIA/CUDA distributions, CPU torch, no matplotlib; "
        f"{', '.join(REQUIRED_MODULES)} import\n"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
