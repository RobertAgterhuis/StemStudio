"""Score warnings with their fixed texts (data-model.md 2.7)."""

from stemstudio_worker.schemas import SCORE_WARNING_TEXTS, ScoreWarning, ScoreWarningCode


def score_warning(code: ScoreWarningCode, **values: object) -> ScoreWarning:
    """Return the warning ``code`` with its text filled from ``values`` (``n``, ``piece``)."""
    return ScoreWarning(code=code, text=SCORE_WARNING_TEXTS[code].format(**values))
