"""The ``song`` kind (010 US1): lyrics -> ACE-Step ``text2music`` -> labelled takes.

The GPU needs depend on the request: with ``thinking`` the planner profile comes first
(``SONG_PLANNED``), then the DiT alone (``SONG_DIRECT``) and its low-memory variant; without
``thinking`` only the DiT profiles. Takes with vocals carry ``vocal: synthetic`` and the
``AI_VOICE=synthetic`` tag; instrumentals ``vocal: none`` and no voice tag (consent and AI-label
rules of 010).
"""

from collections.abc import Sequence

from stemstudio_generator.jobs.progress import JobOutcome
from stemstudio_generator.jobs.restyle import TakeOutput
from stemstudio_generator.ports import (
    SONG_NEEDS,
    SONG_PLANNED,
    DeviceUse,
    EngineSong,
    GpuNeed,
    MusicEngine,
    ProgressCallback,
)
from stemstudio_generator.schemas import Generation, SongParams, VocalLabel


def song_params(job: Generation) -> SongParams:
    """The job's song parameters (a song job always has them)."""
    if job.song is None:  # pragma: no cover - guaranteed by Generation's validator
        msg = "not a song job"
        raise ValueError(msg)
    return job.song


def song_needs(params: SongParams) -> tuple[GpuNeed, ...]:
    """Planner first when ``thinking`` is requested, else the DiT profiles only."""
    return SONG_NEEDS if params.thinking else tuple(n for n in SONG_NEEDS if n is not SONG_PLANNED)


def vocal_label(params: SongParams) -> VocalLabel:
    """``synthetic`` for a song with vocals, ``none`` for an instrumental."""
    return "none" if params.instrumental else "synthetic"


class SongHandler:
    """``JobHandler`` of the ``song`` kind."""

    def __init__(self, engine: MusicEngine, output: TakeOutput) -> None:
        """Generate with ``engine``; ``output`` labels the takes."""
        self._engine = engine
        self._output = output

    @property
    def engine(self) -> MusicEngine:
        """The music engine."""
        return self._engine

    def needs(self, job: Generation) -> Sequence[GpuNeed]:
        """See ``song_needs``."""
        return song_needs(song_params(job))

    def prepare(self, job: Generation) -> None:
        """Nothing to decode (the request is JSON)."""
        del job

    def execute(self, job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
        """Run ``text2music`` and label the takes with the AI voice label."""
        del use  # the runner loaded the engine for it
        params = song_params(job)
        request = EngineSong(params, params.thinking, self._output.files.work(job.generation_id))
        voice = vocal_label(params)
        takes = self._engine.song(request, on_progress)
        return JobOutcome(takes=[self._output.label(job, take, voice) for take in takes])

    def cleanup(self, job: Generation) -> None:
        """Delete the engine's work files."""
        self._output.files.clean(job.generation_id)
