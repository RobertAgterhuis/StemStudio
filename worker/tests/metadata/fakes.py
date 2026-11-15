"""Shared fakes for the metadata tests: clock, sleeper and a recording mock transport."""

from collections.abc import Callable, Iterable
from dataclasses import dataclass, field
from datetime import UTC, datetime

import httpx

from stemstudio_worker.metadata.http import OnlineClient, RetryTiming
from stemstudio_worker.metadata.ratelimit import RateLimiter

USER_AGENT = "StemStudio/0.1.0 ( local-practice-tool; contact not configured )"
NOW = datetime(2026, 11, 15, 12, 0, 0, tzinfo=UTC)

Handler = Callable[[httpx.Request], httpx.Response]


@dataclass
class FakeClock:
    """Monotonic clock that only moves when the fake sleeper sleeps."""

    now: float = 1000.0
    sleeps: list[float] = field(default_factory=list)

    def __call__(self) -> float:
        return self.now

    def sleep(self, seconds: float) -> None:
        self.sleeps.append(seconds)
        self.now += seconds


@dataclass
class Recorder:
    """A ``MockTransport`` handler that records requests and replays queued responses."""

    responses: list[httpx.Response | Handler] = field(default_factory=list)
    requests: list[httpx.Request] = field(default_factory=list)

    def __call__(self, request: httpx.Request) -> httpx.Response:
        self.requests.append(request)
        if not self.responses:
            return httpx.Response(500)
        reply = self.responses.pop(0)
        return reply if isinstance(reply, httpx.Response) else reply(request)

    def transport(self) -> httpx.MockTransport:
        return httpx.MockTransport(self)


@dataclass
class OnlineHarness:
    """An ``OnlineClient`` wired to a recorder and a fake clock."""

    recorder: Recorder
    clock: FakeClock
    client: OnlineClient


def online_harness(
    responses: Iterable[httpx.Response | Handler] = (), jitter: float = 0.5
) -> OnlineHarness:
    """Build a client whose clock, sleeper and jitter are fakes (tests run instantly)."""
    recorder = Recorder(list(responses))
    clock = FakeClock()
    timing = RetryTiming(sleep=clock.sleep, now=lambda: NOW, jitter=lambda: jitter)
    limiter = RateLimiter(clock=clock, sleep=clock.sleep)
    client = OnlineClient(USER_AGENT, limiter, recorder.transport(), timing)
    return OnlineHarness(recorder, clock, client)


def json_response(body: object, status: int = 200) -> httpx.Response:
    """A JSON response."""
    return httpx.Response(status, json=body)
