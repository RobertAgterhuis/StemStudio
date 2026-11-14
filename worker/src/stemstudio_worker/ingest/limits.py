"""Shared duration limit handling for the fetcher and the normaliser."""

from typing import Final

from stemstudio_worker.ports import AudioTooLongError

DEFAULT_MAX_DURATION_SECONDS: Final = 900.0
"""Longest accepted audio (15 minutes); production uses ``WorkerSettings.max_duration_seconds``."""


def too_long_error(max_duration_seconds: float) -> AudioTooLongError:
    """Build the user-facing error for audio over the limit.

    Args:
        max_duration_seconds: The configured limit.

    Returns:
        ``AudioTooLongError("Audio is longer than 15 minutes")`` for the default 900 s limit.
    """
    minutes = f"{max_duration_seconds / 60:g}"
    return AudioTooLongError(f"Audio is longer than {minutes} minutes")
