"""Cover Art Archive: the front picture of a release group (research F-03, ADR-014 D2).

``/release-group/{mbid}/front-500`` usually redirects to ``archive.org``; the client follows and
re-validates it. The picture is re-encoded like every other picture. Cover Art Archive states no
image licence: pictures are shown in-app only, never exported (FR-014).
"""

from typing import Final

from stemstudio_worker.metadata.http import OnlineClient
from stemstudio_worker.metadata.images import PillowImageNormalizer
from stemstudio_worker.ports import ArtworkCandidate, ArtworkImages
from stemstudio_worker.schemas import ArtworkInfo

CAA_ROOT: Final = "https://coverartarchive.org"
"""Root of the Cover Art Archive."""


class CoverArtArchive:
    """Downloads and normalises release group front covers."""

    def __init__(self, client: OnlineClient) -> None:
        """Create the client wrapper.

        Args:
            client: The process-wide online client.
        """
        self._client = client
        self._normalizer = PillowImageNormalizer()

    def front(self, release_group_mbid: str) -> tuple[ArtworkImages, ArtworkInfo] | None:
        """The normalised 500 px front cover of a release group.

        Args:
            release_group_mbid: The release group id.

        Returns:
            The JPEGs and their ``coverartarchive`` record (origin: the CAA URL), or None when
            the release group has no front cover (404).

        Raises:
            OnlineRequestError: The download failed.
            ArtworkError: The picture could not be decoded or is not allowed.
        """
        url = f"{CAA_ROOT}/release-group/{release_group_mbid}/front-500"
        response = self._client.get(url)
        if response is None:
            return None
        images = self._normalizer.normalize(
            ArtworkCandidate(source="coverartarchive", data=response.content)
        )
        return images, images.info("coverartarchive", url)
