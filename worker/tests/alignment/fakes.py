"""A scriptable stand-in for stable-ts + Whisper (no model, no download); self-written lyrics."""

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import numpy as np
import numpy.typing as npt

TEXT = "Fake line one\nLa la la"
WORDS = ("Fake", "line", "one", "La", "la", "la")


@dataclass(frozen=True)
class Word:
    """One stable-ts word (``word`` keeps the leading space stable-ts adds)."""

    word: str
    start: float
    end: float
    probability: float = 0.9


@dataclass
class Result:
    """A stable-ts ``WhisperResult`` subset."""

    words: list[Word]
    language: str | None = "en"

    def all_words(self) -> list[Word]:
        return self.words


def spread(
    texts: tuple[str, ...] = WORDS, start: float = 1.0, step: float = 0.4, gap: float = 0.0
) -> Result:
    """Words of ``step`` seconds from ``start`` (``gap`` after each), with stable-ts spacing."""
    words = []
    for index, text in enumerate(texts):
        begin = start + index * (step + gap)
        words.append(Word(f" {text}", begin, begin + step))
    return Result(words)


def collapsed(texts: tuple[str, ...] = WORDS) -> Result:
    """Every word squeezed to zero length at 12.5 s (the P0-2 collapse)."""
    return Result([Word(f" {text}", 12.5, 12.5) for text in texts])


@dataclass
class FakeModel:
    """Returns the scripted ``results`` in order and records every call."""

    device: str
    results: list[Result | None]
    calls: list[tuple[str, dict[str, Any]]] = field(default_factory=list)
    ticks: tuple[tuple[int, int], ...] = ((50, 100), (100, 100))

    def _next(self, kind: str, options: dict[str, Any]) -> Result | None:
        self.calls.append((kind, options))
        callback = options.get("progress_callback")
        for seek, total in self.ticks:
            if callback is not None:
                callback(seek, total)
        return self.results.pop(0)

    def align(self, audio: npt.NDArray[np.float32], text: str, **options: Any) -> Result | None:
        return self._next("align", {"text": text, **options})

    def align_words(
        self, audio: npt.NDArray[np.float32], result: list[dict[str, Any]], **options: Any
    ) -> Result | None:
        return self._next("align_words", {"segments": result, **options})


@dataclass
class FakeRuntime:
    """``WhisperRuntime`` over :class:`FakeModel`; ``download`` writes the weights file."""

    results: list[Result | None] = field(default_factory=lambda: [spread()])
    download_error: Exception | None = None
    load_error: Exception | None = None
    detected: str = "en"
    downloads: list[tuple[str, Path]] = field(default_factory=list)
    loads: list[tuple[str, str, Path]] = field(default_factory=list)
    models: list[FakeModel] = field(default_factory=list)

    def download(self, name: str, root: Path) -> Path:
        self.downloads.append((name, root))
        if self.download_error is not None:
            raise self.download_error
        root.mkdir(parents=True, exist_ok=True)
        target = root / "large-v3-turbo.pt"
        target.write_bytes(b"weights")
        return target

    def load(self, name: str, device: str, root: Path) -> FakeModel:
        self.loads.append((name, device, root))
        if self.load_error is not None:
            raise self.load_error
        model = FakeModel(device, self.results)
        self.models.append(model)
        return model

    def detect_language(self, model: Any, audio: npt.NDArray[np.float32]) -> str:
        return self.detected
