"""Shared fakes for the metadata tests: clock, sleeper and a recording mock transport."""

from collections.abc import Callable, Iterable
from dataclasses import dataclass, field
from datetime import UTC, datetime

import httpx

from stemstudio_worker.metadata.http import OnlineClient, RequestPacing
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
    limiter = RateLimiter(clock=clock, sleep=clock.sleep)
    pacing = RequestPacing(limiter, sleep=clock.sleep, now=lambda: NOW, jitter=lambda: jitter)
    client = OnlineClient(USER_AGENT, recorder.transport(), pacing)
    return OnlineHarness(recorder, clock, client)


def json_response(body: object, status: int = 200) -> httpx.Response:
    """A JSON response."""
    return httpx.Response(status, json=body)


RouteKey = tuple[str, str, str]
"""``(host, path, props)``: Wikidata calls share a path and differ by ``props``."""


@dataclass
class Router:
    """A ``MockTransport`` handler answering by host, path and ``props`` (order-independent).

    A route with several replies hands them out in order and then keeps repeating the last one.
    """

    routes: dict[RouteKey, list[httpx.Response]] = field(default_factory=dict)
    requests: list[httpx.Request] = field(default_factory=list)

    def add(self, host: str, path: str, *replies: httpx.Response, props: str = "") -> None:
        self.routes.setdefault((host, path, props), []).extend(replies)

    def __call__(self, request: httpx.Request) -> httpx.Response:
        self.requests.append(request)
        key = (request.url.host, request.url.path, request.url.params.get("props", ""))
        replies = self.routes.get(key)
        if not replies:
            return httpx.Response(400, text=f"no route for {key}")
        return replies.pop(0) if len(replies) > 1 else replies[0]


def routed_client(router: Router) -> OnlineClient:
    """An ``OnlineClient`` on ``router`` with a fake clock (no real sleeping)."""
    clock = FakeClock()
    limiter = RateLimiter(clock=clock, sleep=clock.sleep)
    pacing = RequestPacing(limiter, sleep=clock.sleep, now=lambda: NOW, jitter=lambda: 0.5)
    return OnlineClient(USER_AGENT, httpx.MockTransport(router), pacing)
