"""Size limits of a handout job request (stub)."""

from typing import Any, Final

from stemstudio_worker.schemas import HandoutDocument, HandoutJobRequest

MAX_HANDOUT_BODY_BYTES: Final = 8 * 1024 * 1024
MAX_HANDOUT_TEXT_CHARS: Final = 500_000
MAX_HANDOUT_CELLS: Final = 30_000
MAX_HANDOUT_NOTATION_BYTES: Final = 128 * 1024 * 1024


class HandoutLimitError(ValueError):
    """A handout request over a job limit (stub)."""

    def __init__(self, messages: list[str]) -> None:
        """Stub."""
        super().__init__("; ".join(messages))
        self.messages = messages

    def errors(self) -> list[dict[str, Any]]:
        """Stub."""
        return []


def text_chars(document: HandoutDocument) -> int:
    """Stub."""
    del document
    return 0


def layout_cells(document: HandoutDocument) -> int:
    """Stub."""
    del document
    return 0


def check_request_limits(request: HandoutJobRequest) -> None:
    """Stub."""
    del request


def check_notation_bytes(total: int) -> None:
    """Stub."""
    del total
