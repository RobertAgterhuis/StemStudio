"""005:T010: 1 request/s per host family, Retry-After and back-off (FR-011, research R-06)."""

import threading
from datetime import timedelta

import pytest

from stemstudio_worker.metadata.ratelimit import (
    MAX_RETRIES,
    RETRY_AFTER_CAP_SECONDS,
    RateLimiter,
    backoff_seconds,
    host_family,
    retry_after_seconds,
)

from .fakes import NOW, FakeClock


@pytest.mark.parametrize(
    ("host", "family"),
    [
        ("musicbrainz.org", "musicbrainz"),
        ("coverartarchive.org", "musicbrainz"),
        ("www.wikidata.org", "wikimedia"),
        ("en.wikipedia.org", "wikimedia"),
        ("nl.wikipedia.org", "wikimedia"),
        ("archive.org", "archive.org"),
        ("ia800.us.archive.org", "archive.org"),
        ("i.ytimg.com", "i.ytimg.com"),
        ("MusicBrainz.org", "musicbrainz"),
    ],
)
def test_host_family(host: str, family: str) -> None:
    assert host_family(host) == family


def test_first_request_is_not_delayed() -> None:
    clock = FakeClock()
    limiter = RateLimiter(clock=clock, sleep=clock.sleep)

    limiter.acquire("musicbrainz.org")

    assert clock.sleeps == []


def test_requests_to_one_family_are_spaced_one_second_apart() -> None:
    clock = FakeClock()
    limiter = RateLimiter(clock=clock, sleep=clock.sleep)

    limiter.acquire("musicbrainz.org")
    limiter.acquire("coverartarchive.org")
    limiter.acquire("musicbrainz.org")

    assert clock.sleeps == [pytest.approx(1.0), pytest.approx(1.0)]


def test_families_are_independent() -> None:
    clock = FakeClock()
    limiter = RateLimiter(clock=clock, sleep=clock.sleep)

    limiter.acquire("musicbrainz.org")
    limiter.acquire("en.wikipedia.org")
    limiter.acquire("i.ytimg.com")

    assert clock.sleeps == []


def test_elapsed_time_counts_towards_the_interval() -> None:
    clock = FakeClock()
    limiter = RateLimiter(clock=clock, sleep=clock.sleep)

    limiter.acquire("musicbrainz.org")
    clock.now += 0.75
    limiter.acquire("musicbrainz.org")
    clock.now += 5
    limiter.acquire("musicbrainz.org")

    assert clock.sleeps == [pytest.approx(0.25)]


def test_interval_is_configurable() -> None:
    clock = FakeClock()
    limiter = RateLimiter(clock=clock, sleep=clock.sleep, interval=2.5)

    limiter.acquire("musicbrainz.org")
    limiter.acquire("musicbrainz.org")

    assert clock.sleeps == [pytest.approx(2.5)]


def test_concurrent_callers_reserve_distinct_slots() -> None:
    clock = FakeClock()
    reserved: list[float] = []
    lock = threading.Lock()

    def sleep(seconds: float) -> None:
        with lock:
            reserved.append(seconds)

    limiter = RateLimiter(clock=clock, sleep=sleep)
    threads = [
        threading.Thread(target=limiter.acquire, args=("musicbrainz.org",)) for _ in range(4)
    ]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()

    assert sorted(reserved) == [pytest.approx(1.0), pytest.approx(2.0), pytest.approx(3.0)]


@pytest.mark.parametrize(
    ("header", "expected"),
    [
        ("3", 3.0),
        ("0", 0.0),
        (" 12 ", 12.0),
        ("120", RETRY_AFTER_CAP_SECONDS),
        ((NOW + timedelta(seconds=7)).strftime("%a, %d %b %Y %H:%M:%S GMT"), 7.0),
        ((NOW - timedelta(seconds=7)).strftime("%a, %d %b %Y %H:%M:%S GMT"), 0.0),
        ((NOW + timedelta(hours=1)).strftime("%a, %d %b %Y %H:%M:%S GMT"), 30.0),
    ],
)
def test_retry_after_seconds_and_http_date(header: str, expected: float) -> None:
    assert retry_after_seconds(header, NOW) == pytest.approx(expected)


@pytest.mark.parametrize("header", [None, "", "soon", "-5", "1.5e999x"])
def test_unusable_retry_after_is_ignored(header: str | None) -> None:
    assert retry_after_seconds(header, NOW) is None


def test_retry_after_cap_is_30_seconds() -> None:
    assert RETRY_AFTER_CAP_SECONDS == 30.0


@pytest.mark.parametrize(
    ("attempt", "jitter", "expected"),
    [
        (0, 0.5, 2.0),
        (1, 0.5, 4.0),
        (2, 0.5, 8.0),
        (0, 0.0, 1.6),
        (2, 0.0, 6.4),
        (0, 0.999999, 2.4),
        (2, 1.0, 9.6),
    ],
)
def test_backoff_is_2_4_8_seconds_with_20_percent_jitter(
    attempt: int, jitter: float, expected: float
) -> None:
    assert backoff_seconds(attempt, jitter) == pytest.approx(expected, abs=1e-4)


def test_at_most_three_retries() -> None:
    assert MAX_RETRIES == 3
