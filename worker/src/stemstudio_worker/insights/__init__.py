"""Reference-free stem quality estimates for the ``insights`` lane (feature 020, ADR-029).

Imports only ``ports``, ``schemas``, numpy, scipy, soxr and soundfile (plan.md dependency
direction). ``analyzer.SignalQualityAnalyzer`` and ``fake.FakeQualityAnalyzer`` implement
``ports.QualityAnalyzer``.
"""
