"""Wikidata claims of an artist (research F-04; CC0 data).

One ``wbgetentities`` call reads the sitelinks of the configured wikis and the claims P136
(genres), P527 (members, qualifiers P580/P582), P571 (inception), P576 (dissolved) and P856
(website); a second call reads the English labels of the genre and member items (<= 50 ids).
"""

import re
from dataclasses import dataclass, field
from typing import Final

from pydantic import Field

from stemstudio_worker.metadata.http import LenientModel, OnlineClient, parse_answer
from stemstudio_worker.metadata.matcher import year_of
from stemstudio_worker.schemas import ArtistMember

WIKIDATA_API: Final = "https://www.wikidata.org/w/api.php"
"""The Wikidata action API."""

MAX_GENRES: Final = 8
"""Genres kept (``ArtistInfo.genres`` limit)."""

MAX_MEMBERS: Final = 12
"""Members kept (``ArtistInfo.members`` limit)."""

MAX_LABEL_IDS: Final = 50
"""Most ids one ``wbgetentities`` call accepts."""

LABEL_LANGUAGE: Final = "en"
"""Language of the genre and member labels."""

_MAX_GENRE_LENGTH: Final = 100
_MAX_NAME_LENGTH: Final = 500
_ITEM: Final = re.compile(r"Q[0-9]+")
_TIME: Final = re.compile(r"\+([0-9]{4})-")
_YEAR_PRECISION: Final = 9


class _DataValue(LenientModel):
    value: object = None


class _Snak(LenientModel):
    datavalue: _DataValue | None = None


class _Claim(LenientModel):
    mainsnak: _Snak = Field(default_factory=_Snak)
    qualifiers: dict[str, list[_Snak]] = Field(default_factory=dict)


class _Sitelink(LenientModel):
    title: str = ""


class _Label(LenientModel):
    value: str = ""


class _Entity(LenientModel):
    sitelinks: dict[str, _Sitelink] = Field(default_factory=dict)
    claims: dict[str, list[_Claim]] = Field(default_factory=dict)
    labels: dict[str, _Label] = Field(default_factory=dict)


class _Entities(LenientModel):
    entities: dict[str, _Entity] = Field(default_factory=dict)


@dataclass(frozen=True, slots=True)
class WikidataFacts:
    """What Wikidata adds to the MusicBrainz artist facts.

    Attributes:
        genres: Genre labels (P136), at most 8.
        members: Group members (P527 with P580/P582 years), at most 12.
        begin_year: Inception year (P571), or None.
        end_year: Dissolution year (P576), or None.
        website: First https official website (P856), or None.
        sitelinks: Wiki language -> article title, for the configured languages.
    """

    genres: list[str] = field(default_factory=list)
    members: list[ArtistMember] = field(default_factory=list)
    begin_year: int | None = None
    end_year: int | None = None
    website: str | None = None
    sitelinks: dict[str, str] = field(default_factory=dict)


class Wikidata:
    """Reads artist claims and the labels of the items they point to."""

    def __init__(self, client: OnlineClient) -> None:
        """Create the client wrapper.

        Args:
            client: The process-wide online client.
        """
        self._client = client

    def facts(self, qid: str, languages: tuple[str, ...]) -> WikidataFacts:
        """Read the facts of item ``qid``; an unknown item gives empty facts.

        Args:
            qid: The artist's Wikidata id (``Q15920``).
            languages: Wiki languages whose sitelinks are wanted, in order.

        Raises:
            OnlineRequestError: A request failed after retries or an answer was malformed.
        """
        entity = self._entities(_entity_params(qid, languages)).get(qid)
        if entity is None:
            return WikidataFacts()
        genre_ids = _item_ids(entity.claims.get("P136", []))[:MAX_GENRES]
        members = _member_claims(entity)
        labels = self._labels([*genre_ids, *_item_ids(members)])
        return WikidataFacts(
            genres=_genres(genre_ids, labels),
            members=_members(members, labels),
            begin_year=_claim_year(entity, "P571"),
            end_year=_claim_year(entity, "P576"),
            website=_website(entity),
            sitelinks=_sitelinks(entity, languages),
        )

    def _labels(self, ids: list[str]) -> dict[str, str]:
        """English labels of ``ids`` (one call; no call without ids)."""
        if not ids:
            return {}
        params = {
            "action": "wbgetentities",
            "ids": "|".join(ids[:MAX_LABEL_IDS]),
            "props": "labels",
            "languages": LABEL_LANGUAGE,
            "format": "json",
        }
        entities = self._entities(params)
        return {qid: _label(entity) for qid, entity in entities.items() if _label(entity)}

    def _entities(self, params: dict[str, str]) -> dict[str, _Entity]:
        """Run one ``wbgetentities`` call (404 gives no entities)."""
        response = self._client.get(WIKIDATA_API, params)
        if response is None:
            return {}
        return parse_answer(response, _Entities, "Wikidata").entities


def _entity_params(qid: str, languages: tuple[str, ...]) -> dict[str, str]:
    """Parameters of the claims and sitelinks call."""
    return {
        "action": "wbgetentities",
        "ids": qid,
        "props": "sitelinks|claims",
        "sitefilter": "|".join(f"{language}wiki" for language in languages),
        "format": "json",
    }


def _value(snak: _Snak) -> object:
    """The data value of a snak (None when absent)."""
    return snak.datavalue.value if snak.datavalue else None


def _item_id(snak: _Snak) -> str | None:
    """The item id a snak points to (``Q...``), or None."""
    value = _value(snak)
    item = value.get("id") if isinstance(value, dict) else None
    return item if isinstance(item, str) and _ITEM.fullmatch(item) else None


def _item_ids(claims: list[_Claim]) -> list[str]:
    """The item ids of ``claims`` in order, skipping claims without one."""
    return [item for item in (_item_id(claim.mainsnak) for claim in claims) if item]


def _member_claims(entity: _Entity) -> list[_Claim]:
    """The first 12 P527 claims that point to an item."""
    return [claim for claim in entity.claims.get("P527", []) if _item_id(claim.mainsnak)][
        :MAX_MEMBERS
    ]


def _genres(genre_ids: list[str], labels: dict[str, str]) -> list[str]:
    """The labels of the genre items that have one (cut to 100 characters)."""
    return [labels[item][:_MAX_GENRE_LENGTH] for item in genre_ids if item in labels]


def _members(claims: list[_Claim], labels: dict[str, str]) -> list[ArtistMember]:
    """The members of the P527 claims whose item has a label."""
    return [member for member in (_member(claim, labels) for claim in claims) if member]


def _year(snak: _Snak) -> int | None:
    """The year of a time value with at least year precision (1000..2999), or None."""
    value = _value(snak)
    if not isinstance(value, dict) or not _precise(value):
        return None
    match = _TIME.match(str(value.get("time")))
    return year_of(match.group(1)) if match else None


def _precise(value: dict[object, object]) -> bool:
    """True for a time value (string ``time``) with at least year precision."""
    precision = value.get("precision")
    precise = isinstance(precision, int) and precision >= _YEAR_PRECISION
    return precise and isinstance(value.get("time"), str)


def _claim_year(entity: _Entity, prop: str) -> int | None:
    """The year of the first claim of ``prop``."""
    claims = entity.claims.get(prop, [])
    return _year(claims[0].mainsnak) if claims else None


def _qualifier_year(claim: _Claim, prop: str) -> int | None:
    """The year of the first ``prop`` qualifier of ``claim``."""
    snaks = claim.qualifiers.get(prop, [])
    return _year(snaks[0]) if snaks else None


def _member(claim: _Claim, labels: dict[str, str]) -> ArtistMember | None:
    """A member from a P527 claim, or None when its item has no label."""
    name = labels.get(str(_item_id(claim.mainsnak)))
    if name is None:
        return None
    return ArtistMember(
        name=name[:_MAX_NAME_LENGTH],
        from_year=_qualifier_year(claim, "P580"),
        to_year=_qualifier_year(claim, "P582"),
    )


def _website(entity: _Entity) -> str | None:
    """The first https P856 value."""
    for claim in entity.claims.get("P856", []):
        value = _value(claim.mainsnak)
        if isinstance(value, str) and value.startswith("https://"):
            return value
    return None


def _sitelinks(entity: _Entity, languages: tuple[str, ...]) -> dict[str, str]:
    """Article titles of the configured wikis that have one."""
    titles = {lang: entity.sitelinks.get(f"{lang}wiki", _Sitelink()).title for lang in languages}
    return {language: title for language, title in titles.items() if title}


def _label(entity: _Entity) -> str:
    """The English label of an entity ("" when absent)."""
    return entity.labels.get(LABEL_LANGUAGE, _Label()).value.strip()
