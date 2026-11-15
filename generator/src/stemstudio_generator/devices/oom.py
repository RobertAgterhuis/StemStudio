"""Recognising GPU out-of-memory errors (ADR-018 D4).

The check walks the cause and context chain, because ACE-Step and the adapters wrap framework
errors. It matches ``torch.cuda.OutOfMemoryError`` by class name (no torch import), the CUDA
text "out of memory", ACE-Step's own "Insufficient free VRAM" guard, allocator failures and the
port's ``GpuOutOfMemoryError`` (raised by the fake engine).
"""

from collections.abc import Iterator
from typing import Final

from stemstudio_generator.ports import GpuOutOfMemoryError

_MARKERS: Final = ("out of memory", "insufficient free vram", "failed to allocate memory")
_TORCH_OOM: Final = "OutOfMemoryError"


def is_gpu_oom(error: BaseException) -> bool:
    """Whether ``error`` or one of its causes is a GPU out-of-memory error."""
    return any(_is_oom(link) for link in _chain(error))


def _is_oom(error: BaseException) -> bool:
    if isinstance(error, GpuOutOfMemoryError) or type(error).__name__ == _TORCH_OOM:
        return True
    text = str(error).lower()
    return any(marker in text for marker in _MARKERS)


def _chain(error: BaseException) -> Iterator[BaseException]:
    seen: set[int] = set()
    link: BaseException | None = error
    while link is not None and id(link) not in seen:
        seen.add(id(link))
        yield link
        link = link.__cause__ or (None if link.__suppress_context__ else link.__context__)
