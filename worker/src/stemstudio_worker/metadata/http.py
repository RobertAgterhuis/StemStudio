"""The allow-listed https client used for every online request of feature 005 (ADR-014 D6/D7).

Rules: https only, hosts on ``ALLOWED_HOSTS`` (or below ``ALLOWED_DOMAINS``), default port, no
user-info; at most three redirects, each target re-validated; bodies capped at 10 MB; 10 s
timeouts; 1 request/s per host family; 503/429 retried (``Retry-After`` or 2/4/8 s back-off,
three retries) before ``ServiceBusyError``. 404 is a normal answer (``None``).
"""

import random
import time
from collections.abc import Callable, Mapping
from dataclasses import dataclass, field
from datetime import UTC, datetime
from json import loads
from typing import Final
from urllib.parse import urljoin, urlsplit

import httpx
from pydantic import BaseModel, ConfigDict, ValidationError

from stemstudio_worker.config import OnlineSettings
from stemstudio_worker.metadata.ratelimit import (
    MAX_RETRIES,
    RateLimiter,
    backoff_seconds,
    retry_after_seconds,
)
from stemstudio_worker.metadata.user_agent import build_user_agent

ALLOWED_HOSTS: Final = frozenset(
    {
        "musicbrainz.org",
        "coverartarchive.org",
        "archive.org",
        "www.wikidata.org",
        "i.ytimg.com",
        "i.scdn.co",
        "lrclib.net",
    }
)
"""Hosts the worker may contact (005 research R-02; lrclib.net added for 006)."""

ALLOWED_DOMAINS: Final = (".archive.org", ".wikipedia.org")
"""Domains whose sub-domains are allowed (``ia*.us.archive.org``, ``<lang>.wikipedia.org``)."""

MAX_REDIRECTS: Final = 3
"""Redirects followed per request."""

MAX_RESPONSE_BYTES: Final = 10 * 1024 * 1024
"""Largest accepted response body (10 MB)."""

TIMEOUT_SECONDS: Final = 10.0
"""Connect, read, write and pool timeout of every request."""

_REDIRECTS: Final = frozenset({301, 302, 303, 307, 308})
_BUSY: Final = frozenset({429, 503})
_NOT_FOUND: Final = 404


class OnlineRequestError(Exception):
    """A request failed (network, status, refused host, too big); the message has no secrets."""


class HostNotAllowedError(OnlineRequestError):
    """The URL (or a redirect target) is not an allow-listed https URL; nothing was sent."""


class ResponseTooLargeError(OnlineRequestError):
    """The response body is larger than 10 MB."""


class ServiceBusyError(OnlineRequestError):
    """The service still answered 503/429 after three retries."""


@dataclass(frozen=True, slots=True)
class OnlineResponse:
    """A successful (200) response.

    Attributes:
        url: The final URL after redirects.
        content: The body (<= 10 MB).
        content_type: The ``Content-Type`` header ("" when absent).
    """

    url: str
    content: bytes
    content_type: str = ""

    def json(self) -> object:
        """Decode the body as JSON."""
        return loads(self.content)


@dataclass(frozen=True, slots=True)
class _Answer:
    """One received response with its (capped) body."""

    url: httpx.URL
    status: int
    headers: httpx.Headers
    body: bytes


@dataclass(frozen=True, slots=True)
class RequestPacing:
    """Rate limiter and injected time sources of the retry loop (fakes in tests).

    Attributes:
        limiter: Process-wide rate limiter (1 request/s per host family).
        sleep: Blocks for the given number of seconds.
        now: Current wall-clock time (for ``Retry-After`` HTTP dates).
        jitter: Random value in [0, 1] for the back-off jitter.
    """

    limiter: RateLimiter = field(default_factory=RateLimiter)
    sleep: Callable[[float], None] = time.sleep
    now: Callable[[], datetime] = field(default=lambda: datetime.now(UTC))
    jitter: Callable[[], float] = random.random


class LenientModel(BaseModel):
    """Base of the response models: unknown fields are ignored, known ones validated."""

    model_config = ConfigDict(extra="ignore", frozen=True)


def parse_answer[M: BaseModel](response: OnlineResponse, model: type[M], service: str) -> M:
    """Validate a JSON answer of ``service``; anything unexpected is an ``OnlineRequestError``.

    Args:
        response: The 200 response.
        model: The response model.
        service: Service name for the error message, e.g. ``"Wikidata"``.

    Returns:
        The validated answer.
    """
    try:
        return model.model_validate_json(response.content)
    except ValidationError:
        msg = f"{service} sent an answer that could not be read"
        raise OnlineRequestError(msg) from None


def is_allowed_url(url: str) -> bool:
    """Return True for an https URL on an allow-listed host, default port, no user-info."""
    try:
        parts = urlsplit(url)
        plain = _plain_authority(parts.port, parts.username)
    except ValueError:
        return False
    return parts.scheme == "https" and plain and _allowed_host(parts.hostname or "")


def _plain_authority(port: int | None, username: str | None) -> bool:
    """True when the URL uses the default https port and carries no user-info."""
    return port in (None, 443) and username is None


def _allowed_host(host: str) -> bool:
    """True for a host on ``ALLOWED_HOSTS`` or below ``ALLOWED_DOMAINS``."""
    name = host.lower()
    return name in ALLOWED_HOSTS or name.endswith(ALLOWED_DOMAINS)


class OnlineClient:
    """Sends throttled, allow-listed GET requests with the StemStudio User-Agent."""

    def __init__(
        self,
        user_agent: str,
        transport: httpx.BaseTransport | None = None,
        pacing: RequestPacing | None = None,
    ) -> None:
        """Create the client.

        Args:
            user_agent: The ``User-Agent`` header of every request.
            transport: httpx transport; ``httpx.MockTransport`` in tests, default network.
            pacing: Rate limiter, sleeper, wall clock and jitter (one limiter per process).
        """
        self._http = httpx.Client(
            transport=transport,
            headers={"User-Agent": user_agent},
            timeout=httpx.Timeout(TIMEOUT_SECONDS),
            follow_redirects=False,
        )
        self._timing = pacing or RequestPacing()

    @property
    def closed(self) -> bool:
        """True after ``close``."""
        return self._http.is_closed

    def close(self) -> None:
        """Close the connection pool."""
        self._http.close()

    def get(self, url: str, params: Mapping[str, str] | None = None) -> OnlineResponse | None:
        """GET ``url`` following up to three re-validated redirects.

        Args:
            url: An allow-listed https URL.
            params: Query parameters.

        Returns:
            The 200 response, or None for 404.

        Raises:
            HostNotAllowedError: ``url`` or a redirect target is not allowed.
            ResponseTooLargeError: The body exceeds 10 MB.
            ServiceBusyError: 503/429 after three retries.
            OnlineRequestError: Network failure, too many redirects or another status.
        """
        request = self._http.build_request("GET", _allowed(url), params=params)
        for _ in range(MAX_REDIRECTS + 1):
            answer = self._send_with_retries(request)
            if answer.status not in _REDIRECTS:
                return _result(answer)
            request = self._http.build_request("GET", _redirect_target(answer))
        msg = f"More than {MAX_REDIRECTS} redirects for {request.url.host}"
        raise OnlineRequestError(msg)

    def _send_with_retries(self, request: httpx.Request) -> _Answer:
        """Send ``request``, retrying 503/429 answers."""
        for attempt in range(MAX_RETRIES):
            answer = self._send(request)
            if answer.status not in _BUSY:
                return answer
            self._timing.sleep(self._delay(answer, attempt))
        answer = self._send(request)
        if answer.status in _BUSY:
            msg = f"{request.url.host} is busy (HTTP {answer.status})"
            raise ServiceBusyError(msg)
        return answer

    def _delay(self, answer: _Answer, attempt: int) -> float:
        """Wait before retry ``attempt``: ``Retry-After`` when usable, else jittered back-off."""
        header = retry_after_seconds(answer.headers.get("retry-after"), self._timing.now())
        return backoff_seconds(attempt, self._timing.jitter()) if header is None else header

    def _send(self, request: httpx.Request) -> _Answer:
        """Send one request after the rate limiter allows it; read at most 10 MB."""
        self._timing.limiter.acquire(request.url.host)
        try:
            response = self._http.send(request, stream=True)
        except httpx.HTTPError as exc:
            msg = f"Request to {request.url.host} failed ({type(exc).__name__})"
            raise OnlineRequestError(msg) from None
        try:
            body = _capped_body(response)
        finally:
            response.close()
        return _Answer(request.url, response.status_code, response.headers, body)


def _allowed(url: str) -> str:
    """Return ``url`` when it is allowed; raise ``HostNotAllowedError`` otherwise."""
    if not is_allowed_url(url):
        msg = "The URL is not on the list of allowed https hosts"
        raise HostNotAllowedError(msg)
    return url


def _redirect_target(answer: _Answer) -> str:
    """The absolute, allow-listed target of a redirect answer."""
    location = answer.headers.get("location")
    if not location:
        msg = f"Redirect without a location from {answer.url.host}"
        raise OnlineRequestError(msg)
    return _allowed(urljoin(str(answer.url), location))


def _capped_body(response: httpx.Response) -> bytes:
    """Read the body, refusing more than ``MAX_RESPONSE_BYTES`` (declared or streamed)."""
    declared = response.headers.get("content-length", "")
    if declared.isdigit() and int(declared) > MAX_RESPONSE_BYTES:
        raise ResponseTooLargeError(_too_large(response))
    try:
        return _read_capped(response)
    except httpx.HTTPError as exc:
        msg = f"Reading the answer of {response.request.url.host} failed ({type(exc).__name__})"
        raise OnlineRequestError(msg) from None


def _read_capped(response: httpx.Response) -> bytes:
    """Stream the body and stop as soon as it exceeds ``MAX_RESPONSE_BYTES``."""
    body = bytearray()
    for chunk in response.iter_bytes():
        body += chunk
        if len(body) > MAX_RESPONSE_BYTES:
            raise ResponseTooLargeError(_too_large(response))
    return bytes(body)


def _too_large(response: httpx.Response) -> str:
    """Message of ``ResponseTooLargeError``."""
    return f"The answer of {response.request.url.host} is larger than 10 MB"


def _result(answer: _Answer) -> OnlineResponse | None:
    """Map a final (non-redirect) answer: 200 -> result, 404 -> None, else an error."""
    if answer.status == _NOT_FOUND:
        return None
    if answer.status != httpx.codes.OK:
        msg = f"{answer.url.host} answered HTTP {answer.status}"
        raise OnlineRequestError(msg)
    return OnlineResponse(str(answer.url), answer.body, answer.headers.get("content-type", ""))


def create_online_client(
    settings: OnlineSettings, transport: httpx.BaseTransport | None = None
) -> OnlineClient:
    """Build the process-wide client (one rate limiter) from the online settings.

    Args:
        settings: ``WorkerSettings.online`` (the contact URL feeds the User-Agent).
        transport: Optional httpx transport (tests).

    Returns:
        The client; share one instance per process so the rate limit holds.
    """
    return OnlineClient(build_user_agent(settings.contact_url), transport)
