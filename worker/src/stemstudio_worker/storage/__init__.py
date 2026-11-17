"""Worker storage care (feature 013, worker-api 1.7.0): measuring, clean-up and rehydration.

``meter`` walks the data and model directories (``GET /v1/storage``), ``janitor`` removes cache
entries, scratch and old job files no job uses (``POST /v1/storage/cleanup``) and ``rehydrate``
re-creates a completed standard job from the read-only library stems (``POST /v1/jobs/rehydrate``).
This package may use ``jobs`` (paths, stores, records) but never ``separation``, ``detail`` or
``api`` (quality rule, tasks.md T009).
"""
