"""The ``voiceConversion`` kind (010 US3, singer service): vocal stem -> SoulX-SVC -> remix.

The job has one take: the converted vocal mixed into the accompaniment stems, plus the converted
vocal alone (``GET .../takes/1/vocal``). Both files carry ``AI_VOICE=converted`` and
``AI_VOICE_PROFILE=<name>`` (AI-label rules of 010). The service's copies of the prompt (the
own-voice sample) and the target are deleted when the job ends, whatever the outcome.
"""

from collections.abc import Sequence
from pathlib import Path
from typing import Final

from stemstudio_generator.jobs.progress import JobOutcome
from stemstudio_generator.jobs.restyle import TakeOutput, generated_take
from stemstudio_generator.ports import (
    CONVERSION_NEEDS,
    ConversionInput,
    DeviceUse,
    EngineTake,
    GpuNeed,
    ProgressCallback,
    RemixRequest,
    TakeLabels,
    VoiceConverter,
    VoiceRemixer,
)
from stemstudio_generator.schemas import GeneratedTake, Generation, VoiceConversionParams

TARGET_PART: Final = "target"
PROMPT_PART: Final = "prompt"
ACCOMPANIMENT_PART: Final = "accompaniment"
_SEED: Final = 0


def conversion_params(job: Generation) -> VoiceConversionParams:
    """The job's conversion parameters (a conversion job always has them)."""
    if job.voice_conversion is None:  # pragma: no cover - guaranteed by Generation's validator
        msg = "not a voice-conversion job"
        raise ValueError(msg)
    return job.voice_conversion


class ConversionHandler:
    """``JobHandler`` of the ``voiceConversion`` kind."""

    def __init__(
        self, converter: VoiceConverter, remixer: VoiceRemixer, output: TakeOutput
    ) -> None:
        """Convert with ``converter``, remix with ``remixer``, label with ``output``."""
        self._converter = converter
        self._remixer = remixer
        self._output = output

    @property
    def engine(self) -> VoiceConverter:
        """The voice converter."""
        return self._converter

    def needs(self, job: Generation) -> Sequence[GpuNeed]:
        """SoulX-SVC in fp16."""
        del job
        return CONVERSION_NEEDS

    def prepare(self, job: Generation) -> None:
        """The parts were checked at intake."""
        del job

    def execute(self, job: Generation, use: DeviceUse, on_progress: ProgressCallback) -> JobOutcome:
        """Convert, remix and label the take and its vocal stem."""
        del use  # the runner loaded the converter for it
        params, files = conversion_params(job), self._output.files
        inputs = files.inputs(job.generation_id)
        request = ConversionInput(_one(inputs, TARGET_PART), _one(inputs, PROMPT_PART), params)
        vocal = self._converter.convert(request, on_progress)
        accompaniment = sorted(inputs.glob(f"{ACCOMPANIMENT_PART}-*"))
        work = files.work(job.generation_id)
        remixed = self._remixer.remix(
            vocal, RemixRequest(accompaniment, params.vocal_gain_db, work)
        )
        return JobOutcome(takes=[self._label(job, remixed.mix, remixed.vocal)])

    def cleanup(self, job: Generation) -> None:
        """Delete the prompt and target copies, the accompaniment and the work files."""
        self._output.files.clean(job.generation_id)

    def _label(self, job: Generation, mix: Path, vocal: Path) -> GeneratedTake:
        output, generation_id = self._output, job.generation_id
        profile = conversion_params(job).profile_name
        labels = TakeLabels(output.identity.generator, generation_id, 1, "converted", profile)
        written = output.writer.write(mix, output.files.take(generation_id, 1), labels)
        output.writer.write(vocal, output.files.vocal(generation_id, 1), labels)
        take = EngineTake(1, _SEED, written.path, written.duration_seconds)
        record = generated_take(take, written, output.identity.model, "converted")
        return record.model_copy(update={"has_vocal_stem": True})


def _one(directory: Path, name: str) -> Path:
    """The single saved part ``<name>-1.<suffix>``."""
    return sorted(directory.glob(f"{name}-*"))[0]
