"""Wikipedia REST page summary with the data its CC BY-SA 4.0 attribution needs (research F-05).

The first configured language with a sitelink and a usable ``standard`` summary wins. The
``extract`` is kept exactly as the API returns it (summaries that are empty or longer than 2000
characters are dropped rather than cut, so the text is never modified). The attribution links
(article, "Wikipedia contributors" history page, licence) are built here (ADR-014 D8).
"""

from typing import Final
from urllib.parse import quote

from pydantic import Field

from stemstudio_worker.metadata.http import LenientModel, OnlineClient, parse_answer
from stemstudio_worker.schemas import WikipediaSummary

MAX_EXTRACT_LENGTH: Final = 2000
"""Longest extract kept."""

_MAX_REVISION_LENGTH: Final = 50


class _Desktop(LenientModel):
    page: str = ""


class _ContentUrls(LenientModel):
    desktop: _Desktop = Field(default_factory=_Desktop)


class _Summary(LenientModel):
    type: str = ""
    title: str = ""
    extract: str = ""
    revision: int | str | None = None
    content_urls: _ContentUrls = Field(default_factory=_ContentUrls)


class Wikipedia:
    """Reads page summaries of artist articles."""

    def __init__(self, client: OnlineClient) -> None:
        """Create the client wrapper.

        Args:
            client: The process-wide online client.
        """
        self._client = client

    def summary(
        self, sitelinks: dict[str, str], languages: tuple[str, ...]
    ) -> WikipediaSummary | None:
        """The summary in the first configured language that has a usable one.

        Args:
            sitelinks: Wiki language -> article title (from Wikidata).
            languages: Languages in the order they are tried.

        Returns:
            The summary with attribution data, or None.

        Raises:
            OnlineRequestError: A request failed after retries or an answer was malformed.
        """
        for language in languages:
            title = sitelinks.get(language)
            found = self._summary(language, title) if title else None
            if found is not None:
                return found
        return None

    def _summary(self, language: str, title: str) -> WikipediaSummary | None:
        """The usable summary of one article, or None."""
        slug = quote(title.replace(" ", "_"), safe="")
        url = f"https://{language}.wikipedia.org/api/rest_v1/page/summary/{slug}"
        response = self._client.get(url)
        if response is None:
            return None
        return _attributed(language, slug, parse_answer(response, _Summary, "Wikipedia"))


def _attributed(language: str, slug: str, raw: _Summary) -> WikipediaSummary | None:
    """The contract summary, or None when the answer is not a usable standard summary."""
    page = raw.content_urls.desktop.page
    revision = "" if raw.revision is None else str(raw.revision)[:_MAX_REVISION_LENGTH]
    if not _usable(raw, page, revision):
        return None
    return WikipediaSummary(
        language=language,
        title=raw.title,
        extract=raw.extract,
        page_url=page,
        history_url=f"https://{language}.wikipedia.org/w/index.php?title={slug}&action=history",
        revision_id=revision,
    )


def _usable(raw: _Summary, page: str, revision: str) -> bool:
    """A standard summary with title, revision, an https page and a 1..2000 char extract."""
    standard = raw.type == "standard" and bool(raw.title) and bool(revision)
    return standard and page.startswith("https://") and 0 < len(raw.extract) <= MAX_EXTRACT_LENGTH
