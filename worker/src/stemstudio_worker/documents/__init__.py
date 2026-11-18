"""Setlist documents of feature 014 (ADR-023 D4; audio-ingest).

Public API (composed by ``main.py``): :class:`ReportLabSetlistRenderer`, the
:class:`~stemstudio_worker.ports.SetlistDocumentRenderer` with ReportLab and the DejaVu fonts.
Imports only ``ports``, ``schemas``, reportlab and the standard library (014 T015 rule).
"""

from stemstudio_worker.documents.setlist_pdf import ReportLabSetlistRenderer

__all__ = ["ReportLabSetlistRenderer"]
