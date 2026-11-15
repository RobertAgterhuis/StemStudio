"""A fake image normaliser for the artwork step tests (no Pillow, no network)."""

import hashlib
import threading
from dataclasses import dataclass, field

from stemstudio_worker.ports import ArtworkCandidate, ArtworkImages
from stemstudio_worker.schemas import ArtworkSource

YOUTUBE_THUMB = "https://i.ytimg.com/vi/dQw4w9WgXcQ/maxresdefault.jpg"
SPOTIFY_COVER = "https://i.scdn.co/image/ab67616d0000b273"


def images_for(source: ArtworkSource) -> ArtworkImages:
    """Deterministic 'JPEGs' per source, so tests can tell which candidate won."""
    cover = f"cover:{source}".encode()
    return ArtworkImages(cover=cover, thumb=f"thumb:{source}".encode(),
                         sha256=hashlib.sha256(cover).hexdigest())  # fmt: skip


@dataclass
class FakeNormalizer:
    """Records candidates; ``fail`` maps a source to an exception; ``hang`` blocks forever."""

    seen: list[ArtworkCandidate] = field(default_factory=list)
    fail: dict[str, Exception] = field(default_factory=dict)
    hang: threading.Event | None = None

    def normalize(self, candidate: ArtworkCandidate) -> ArtworkImages:
        self.seen.append(candidate)
        if self.hang is not None:
            self.hang.wait(5.0)
        if candidate.source in self.fail:
            raise self.fail[candidate.source]
        return images_for(candidate.source)
