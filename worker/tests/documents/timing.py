"""Times the 20-song setlist PDF outside the test process (SC-008: under 10 s).

Coverage tracing slows ReportLab (pure Python) about twentyfold, so the budget is measured in a
child process without a tracer. Prints the seconds and the page count.
"""

import sys
import time
from datetime import UTC, datetime
from pathlib import Path

from stemstudio_worker.documents import ReportLabSetlistRenderer
from stemstudio_worker.documents.fonts import FontFiles
from stemstudio_worker.schemas import SetlistDocumentRequest


def main(regular: str, bold: str) -> None:
    """Render 20 songs with about 10,000 lyric characters each and print time and pages."""
    lines = [("la " * 30).strip() for _ in range(110)]
    songs = [{"position": n, "title": f"Song {n}", "durationSeconds": 200,
              "lyrics": {"lines": lines, "shortened": False}} for n in range(1, 21)]  # fmt: skip
    request = SetlistDocumentRequest.model_validate({
        "title": "Timing", "generatedAt": datetime(2026, 11, 16, tzinfo=UTC).isoformat(),
        "options": {"includeLyrics": True, "includeSections": True, "includeNotes": True,
                    "pageBreakPerSong": True},
        "songs": songs,
    })  # fmt: skip
    renderer = ReportLabSetlistRenderer(lambda: FontFiles(Path(regular), Path(bold)))
    started = time.perf_counter()
    document = renderer.render(request)
    print(f"{time.perf_counter() - started:.3f} {document.pages}")  # noqa: T201 - child output


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
