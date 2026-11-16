"""Per-process GPU memory cap over torch (009 T014; ADR-012 D4, ADR-018 D4)."""

import sys
from types import SimpleNamespace
from typing import Any

import pytest

from stemstudio_generator.devices.memory import NoMemoryCap, TorchMemoryCap, load_torch_cap


def _torch() -> Any:
    calls: list[tuple[float, int]] = []
    cuda = SimpleNamespace(
        set_per_process_memory_fraction=lambda fraction, device: calls.append((fraction, device)),
    )
    return SimpleNamespace(cuda=cuda, calls=calls)


def test_torch_cap_sets_and_resets_the_fraction_of_gpu_0() -> None:
    torch = _torch()
    cap = TorchMemoryCap(torch)

    cap.limit(0.75)
    cap.reset()

    assert torch.calls == [(0.75, 0), (1.0, 0)]


def test_no_memory_cap_does_nothing() -> None:
    cap = NoMemoryCap()
    cap.limit(0.5)
    cap.reset()


def test_load_torch_cap_imports_torch(monkeypatch: pytest.MonkeyPatch) -> None:
    torch = _torch()
    monkeypatch.setitem(sys.modules, "torch", torch)

    load_torch_cap().limit(0.5)

    assert torch.calls == [(0.5, 0)]
