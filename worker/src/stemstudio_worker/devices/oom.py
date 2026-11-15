"""Recognising GPU out-of-memory errors (ADR-012 D5).

The check walks the exception's cause chain, because the detail models map framework errors to
``MemoryError``/``DetailSeparationError`` (002 ``demix.inference_error``) with ``raise ... from``.
"""

from collections.abc import Iterator
from typing import Final

_MARKERS: Final = ("out of memory", "failed to allocate memory")
_TORCH_OOM: Final = "OutOfMemoryError"


def is_gpu_out_of_memory(exc: BaseException) -> bool:
    """Whether ``exc`` (or one of its causes) is a GPU out-of-memory error.

    Matches ``torch.cuda.OutOfMemoryError`` and the texts "out of memory" / "Failed to allocate
    memory" (CUDA and onnxruntime).
    """
    return any(_is_oom(link) for link in _chain(exc))


def _is_oom(exc: BaseException) -> bool:
    if type(exc).__name__ == _TORCH_OOM:
        return True
    text = str(exc).lower()
    return any(marker in text for marker in _MARKERS)


def _chain(exc: BaseException) -> Iterator[BaseException]:
    seen: set[int] = set()
    link: BaseException | None = exc
    while link is not None and id(link) not in seen:
        seen.add(id(link))
        yield link
        link = link.__cause__ or link.__context__
