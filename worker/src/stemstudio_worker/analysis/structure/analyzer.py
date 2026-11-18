"""The ``stems-novelty`` section analyser 1.0.0 (data-model 2.2, ADR-023 D1; beat-analysis).

Pipeline: load the stems mono at 22,050 Hz -> frames from the grid -> frame features ->
self-similarity and Foote novelty -> boundaries (novelty peaks and vocal switches, short segments
merged, limited to ``maxSections``) -> clusters -> kinds and labels -> times snapped to the grid.
Progress: 0.05 when started, 0.4 after loading, 0.7 after the features, 1.0 when done.
"""

from collections.abc import Mapping, Sequence
from typing import Final

import numpy as np
import numpy.typing as npt

from stemstudio_worker.analysis.structure.boundaries import (
    Boundary,
    Span,
    limit_sections,
    merge_short,
    segment_spans,
    spans_in_seconds,
    union,
    vocal_switches,
)
from stemstudio_worker.analysis.structure.clusters import cluster_letters, margins
from stemstudio_worker.analysis.structure.features import (
    FrameFeatures,
    FrameGrid,
    frame_features,
    frame_grid,
    load_stems,
    vocal_activity,
)
from stemstudio_worker.analysis.structure.labels import (
    NO_VOCALS_LEVEL,
    SOLO_SOURCES,
    Labelled,
    SegmentFacts,
    SongFacts,
    label_segments,
)
from stemstudio_worker.analysis.structure.novelty import novelty, peaks
from stemstudio_worker.ports import SectionProgress, StemFiles
from stemstudio_worker.schemas import (
    SECTION_WARNING_TEXTS,
    AnalyzerInfo,
    BeatGrid,
    DetectedSection,
    SectionJobRequest,
    SectionResult,
    SectionWarning,
    SectionWarningCode,
    StructureAnalyzerName,
)

ANALYZER_VERSION: Final = "1.0.0"
STACK_LAG: Final = 1
"""Frames a novelty peak lags behind the change (``stack_memory(n_steps=2)``)."""
Vector = npt.NDArray[np.float64]


class StemsNoveltyAnalyzer:
    """:class:`~stemstudio_worker.ports.StructureAnalyzer` ``stems-novelty`` 1.0.0."""

    name: StructureAnalyzerName = "stems-novelty"
    version: str = ANALYZER_VERSION

    def analyze(
        self, request: SectionJobRequest, files: StemFiles, progress: SectionProgress
    ) -> SectionResult:
        """Detect, label and snap the sections of the song (data-model 2.2-2.3).

        Raises:
            StructureError: A stem cannot be read.
        """
        progress(0.05)
        audio = load_stems(files, request.duration_seconds)
        progress(0.4)
        frames = frame_grid(request.grid, request.duration_seconds)
        features = frame_features(audio, frames)
        progress(0.7)
        result = _Song(features, frames, request).result()
        progress(1.0)
        return result


class _Song:
    """One analysis: boundaries, segments and their labels."""

    def __init__(self, features: FrameFeatures, frames: FrameGrid, request: SectionJobRequest):
        self._features = features
        self._frames = frames
        self._request = request
        self._no_vocals = features.vocals_p95 < NO_VOCALS_LEVEL * features.mix_p95
        self._bounds, self._limited = self._boundaries()
        self._spans = segment_spans(self._bounds, frames.count)

    def result(self) -> SectionResult:
        means = np.array([_mean(self._features.matrix, span) for span in self._spans])
        letters = cluster_letters(means)
        labelled = label_segments(self._facts(letters), self._song_facts())
        sections = self._sections(labelled, letters, margins(means, letters))
        return SectionResult(analyzer=AnalyzerInfo(name="stems-novelty", version=ANALYZER_VERSION),
                             snap=self._frames.snap, sections=sections,
                             warnings=self._warnings(labelled))  # fmt: skip

    def _boundaries(self) -> tuple[list[Boundary], bool]:
        matrix = self._features.matrix
        curve = novelty(matrix.T @ matrix)
        # stack_memory puts the previous frame into every column, which delays a novelty peak
        # by one frame: the boundary is the frame before the peak.
        novel = [(i - STACK_LAG, float(curve[i])) for i in peaks(curve) if i > STACK_LAG]
        active = vocal_activity(self._features)
        vocal = [] if self._no_vocals else vocal_switches(active)
        merged = merge_short(union(novel, vocal), matrix)
        return limit_sections(merged, matrix, self._request.max_sections)

    def _facts(self, letters: Sequence[str]) -> list[SegmentFacts]:
        return [_segment_facts(self._features, span, letter)
                for span, letter in zip(self._spans, letters, strict=True)]  # fmt: skip

    def _song_facts(self) -> SongFacts:
        shares = _solo_shares(_sources(self._features))
        return SongFacts(median_shares={name: float(np.median(v)) for name, v in shares.items()},
                         no_vocals=bool(self._no_vocals))  # fmt: skip

    def _sections(
        self, labelled: Labelled, letters: Sequence[str], margin: Sequence[float]
    ) -> list[DetectedSection]:
        times = spans_in_seconds(self._spans, self._frames)
        strengths = self._strengths()
        return [
            DetectedSection(kind=labelled.kinds[k], label=labelled.labels[k], start_seconds=start,
                            end_seconds=end, cluster=letters[k],
                            instrument=labelled.instruments[k],
                            confidence=round(min(1.0, 0.5 * strengths[k] + 0.5 * margin[k]), 2))
            for k, (start, end) in enumerate(times)
        ]  # fmt: skip

    def _strengths(self) -> list[float]:
        """Strength of each segment's start boundary (the first segment: its end boundary)."""
        starts = [b.strength for b in self._bounds]
        return [starts[0] if starts else 1.0, *starts]

    def _warnings(self, labelled: Labelled) -> list[SectionWarning]:
        codes: list[SectionWarningCode] = [*_grid_warnings(self._request.grid, self._frames)]
        codes += labelled.warnings
        if self._limited:
            codes.append("merged-to-limit")
        return [SectionWarning(code=code, message=SECTION_WARNING_TEXTS[code]) for code in codes]


def _grid_warnings(grid: BeatGrid | None, frames: FrameGrid) -> list[SectionWarningCode]:
    if frames.snap == "time":
        return ["no-grid"]
    return [] if grid is None or grid.reliable else ["grid-unreliable"]


def _mean(matrix: Vector, span: Span) -> Vector:
    return np.asarray(matrix[:, span[0] : span[1]].mean(axis=1))


def _segment_facts(features: FrameFeatures, span: Span, letter: str) -> SegmentFacts:
    part = slice(*span)
    summed = {name: np.asarray(values[part].sum()) for name, values in _sources(features).items()}
    shares = _solo_shares(summed)
    return SegmentFacts(cluster=letter, vocal_ratio=float(features.vocal_ratio[part].mean()),
                        mix_rms=float(features.mix_rms[part].mean()),
                        shares={name: float(v) for name, v in shares.items()},
                        detail_shares=_detail_shares(features.energy, part))  # fmt: skip


def _sources(features: FrameFeatures) -> dict[str, Vector]:
    """Energy per frame of the solo sources the track has."""
    return {name: features.energy[name] for name in SOLO_SOURCES if name in features.energy}


def _solo_shares(sources: Mapping[str, Vector]) -> dict[str, Vector]:
    """Each solo source's share of their summed energy (per frame or per segment)."""
    total = np.sum(list(sources.values()), axis=0)
    safe = np.where(total > 0, total, 1.0)
    return {name: np.asarray(values / safe) for name, values in sources.items()}


def _detail_shares(energy: Mapping[str, Vector], part: slice) -> dict[str, float]:
    details = {}
    for name, values in energy.items():
        parent = name.partition(".")[0]
        if "." in name and parent in energy:
            whole = float(energy[parent][part].sum())
            details[name] = float(values[part].sum()) / whole if whole > 0 else 0.0
    return details
