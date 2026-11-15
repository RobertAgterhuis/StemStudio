"""Artwork normalisation and online metadata lookups of feature 005 (audio-ingest agent, ADR-014).

The package imports only ``ports``, ``schemas`` and ``config`` (plan.md dependency direction);
the composition root (``main.py``) builds its adapters and hands them to the job pipeline.
Every outbound request goes through ``http.OnlineClient`` (https, allow-listed hosts, <= 3
re-validated redirects, 10 MB cap, 10 s timeouts, 1 request/s per service, Retry-After and
back-off) with a User-Agent that contains no personal data.

Modules:
    user_agent: The ``StemStudio/<version> ( <contact> )`` User-Agent and contact URL check.
    ratelimit: Per-host-family token bucket, Retry-After parsing and back-off delays.
    http: The allow-listed ``OnlineClient``.
"""
