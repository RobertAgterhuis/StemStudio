"""GPU out-of-memory classification (009 T014; ADR-018 D4)."""

import pytest

from stemstudio_generator.devices.oom import is_gpu_oom
from stemstudio_generator.ports import GpuOutOfMemoryError


class OutOfMemoryError(RuntimeError):
    """Same class name as ``torch.cuda.OutOfMemoryError``."""


@pytest.mark.parametrize(
    "error",
    [
        OutOfMemoryError("CUDA"),
        RuntimeError("CUDA out of memory. Tried to allocate 2.00 GiB"),
        RuntimeError("Insufficient free VRAM: need 12.0 GB, have 4.1 GB"),
        GpuOutOfMemoryError(),
        MemoryError("Failed to allocate memory for requested buffer"),
    ],
)
def test_gpu_oom_errors_are_recognised(error: BaseException) -> None:
    assert is_gpu_oom(error)


def test_an_oom_in_the_cause_chain_is_recognised() -> None:
    try:
        try:
            raise OutOfMemoryError("inner")
        except OutOfMemoryError as inner:
            raise RuntimeError("generation failed") from inner
    except RuntimeError as outer:
        assert is_gpu_oom(outer)


def test_an_oom_in_the_implicit_context_is_recognised() -> None:
    try:
        try:
            raise RuntimeError("CUDA out of memory")
        except RuntimeError:
            raise ValueError("while handling") from None
    except ValueError as outer:
        assert not is_gpu_oom(outer)  # "from None" suppresses the context on purpose
    outer = ValueError("wrapped")
    outer.__context__ = RuntimeError("CUDA out of memory")
    assert is_gpu_oom(outer)


@pytest.mark.parametrize("error", [ValueError("bad"), RuntimeError("CUDA error: device-side")])
def test_other_errors_are_not_oom(error: BaseException) -> None:
    assert not is_gpu_oom(error)


def test_cyclic_cause_chains_terminate() -> None:
    first, second = RuntimeError("a"), RuntimeError("b")
    first.__cause__, second.__cause__ = second, first

    assert not is_gpu_oom(first)
