"""ACE-Step 1.5's Python API as the engine uses it, and the mapping of its failures.

Imported lazily (``acestep`` image variant only); tests replace ``AceStepApi`` by fakes.
"""

import logging
from dataclasses import dataclass
from typing import Any, Final

from stemstudio_generator.ports import GeneratorError, GpuOutOfMemoryError

LOAD_FAILED_TEXT: Final = "The ACE-Step model could not be loaded; see the generator log."
"""Job error text when the DiT or the language model cannot be initialised."""

_OOM_MARKERS: Final = ("out of memory", "insufficient free vram")
_LOG: Final = logging.getLogger(__name__)


@dataclass(frozen=True, slots=True)
class AceStepApi:
    """The parts of ACE-Step's Python API the engine uses.

    Attributes:
        handler: ``acestep.handler.AceStepHandler``.
        llm: ``acestep.llm_inference.LLMHandler``.
        params: ``acestep.inference.GenerationParams``.
        config: ``acestep.inference.GenerationConfig``.
        generate: ``acestep.inference.generate_music``.
        torch: The ``torch`` module (cache, dtypes).
        causal_lm: ``transformers.AutoModelForCausalLM`` (the bf16 LM loader, 010).
    """

    handler: Any
    llm: Any
    params: Any
    config: Any
    generate: Any
    torch: Any
    causal_lm: Any


def import_acestep() -> AceStepApi:
    """Import ACE-Step, torch and transformers (``acestep`` image variant only)."""
    import torch  # noqa: PLC0415 - lazy: GPU image variant only
    from acestep.handler import AceStepHandler  # noqa: PLC0415
    from acestep.inference import (  # noqa: PLC0415
        GenerationConfig,
        GenerationParams,
        generate_music,
    )
    from acestep.llm_inference import LLMHandler  # noqa: PLC0415
    from transformers import AutoModelForCausalLM  # noqa: PLC0415

    return AceStepApi(
        handler=AceStepHandler, llm=LLMHandler, params=GenerationParams,
        config=GenerationConfig, generate=generate_music, torch=torch,
        causal_lm=AutoModelForCausalLM,
    )  # fmt: skip


def failure(text: str, readable: str) -> GeneratorError:
    """``GpuOutOfMemoryError`` for OOM texts, else ``readable``; the raw text is only logged."""
    _LOG.warning("ACE-Step failure: %s", text)
    lowered = text.lower()
    if any(marker in lowered for marker in _OOM_MARKERS):
        return GpuOutOfMemoryError()
    return GeneratorError(readable)
