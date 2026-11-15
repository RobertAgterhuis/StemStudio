"""005:T010: User-Agent without personal data (FR-010, research R-07, ADR-014 D6)."""

import logging
import re

import pytest

from stemstudio_worker.metadata.user_agent import (
    GENERIC_CONTACT,
    build_user_agent,
    is_valid_contact_url,
    worker_version,
)

DEFAULT_PATTERN = re.compile(r"^StemStudio/[0-9.]+ \( local-practice-tool; contact not configured \)$")

INVALID_CONTACTS = [
    "http://stemstudio.example/",
    "mailto:someone@example.org",
    "https://someone@example.org/",
    "https://user:secret@example.org/",
    "https://example.org/contact?mail=a@b",
    "https://example.org/" + "a" * 200,
    "https://example.org/a b",
    "https://example.org/\tx",
    "https://example.org/\x00",
    "https:///no-host",
    "https://",
    "ftp://example.org/",
    "example.org",
    "",
]


def test_default_user_agent_has_the_generic_token() -> None:
    agent = build_user_agent(None)

    assert DEFAULT_PATTERN.fullmatch(agent)
    assert GENERIC_CONTACT in agent


def test_version_is_the_worker_package_version() -> None:
    assert re.fullmatch(r"[0-9]+(\.[0-9]+)*", worker_version())
    assert build_user_agent(None).startswith(f"StemStudio/{worker_version()} ")


def test_version_can_be_given() -> None:
    assert build_user_agent(None, version="9.8.7").startswith("StemStudio/9.8.7 ( ")


def test_valid_contact_url_is_used() -> None:
    agent = build_user_agent("https://stemstudio.example/about")

    assert agent == f"StemStudio/{worker_version()} ( https://stemstudio.example/about )"


def test_contact_url_of_exactly_200_characters_is_accepted() -> None:
    url = "https://example.org/" + "a" * (200 - len("https://example.org/"))

    assert len(url) == 200
    assert is_valid_contact_url(url)


@pytest.mark.parametrize("contact", INVALID_CONTACTS)
def test_invalid_contact_urls_fall_back_to_the_generic_token(
    contact: str, caplog: pytest.LogCaptureFixture
) -> None:
    caplog.set_level(logging.WARNING)

    agent = build_user_agent(contact)

    assert not is_valid_contact_url(contact)
    assert DEFAULT_PATTERN.fullmatch(agent)
    if contact:
        assert "STEMSTUDIO_CONTACT_URL" in caplog.text
        assert contact not in caplog.text


def test_no_warning_without_a_contact_url(caplog: pytest.LogCaptureFixture) -> None:
    caplog.set_level(logging.WARNING)

    build_user_agent(None)

    assert caplog.text == ""


def test_missing_package_metadata_gives_a_fallback_version(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    from importlib import metadata

    from stemstudio_worker.metadata import user_agent

    def missing(name: str) -> str:
        raise metadata.PackageNotFoundError(name)

    monkeypatch.setattr(user_agent.metadata, "version", missing)

    assert user_agent.worker_version() == "0.0.0"
