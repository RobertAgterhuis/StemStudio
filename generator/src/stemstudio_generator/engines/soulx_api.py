"""SoulX-Singer-SVC's Python API as the singer uses it (``soulx`` image variant only).

The singer image puts the SoulX-Singer source (commit ``81aeb3a``, Apache-2.0) on the path
(ADR-019 Phase 0 outcome). ``preprocess/tools/f0_extraction.py`` (RMVPE) is loaded by file path,
because the ``preprocess`` package ``__init__`` imports heavy tools the singer does not need.
"""

import importlib.util
from dataclasses import dataclass
from pathlib import Path
from types import ModuleType
from typing import Any, Final

from stemstudio_generator.ports import GeneratorError

F0_MODULE: Final = "preprocess/tools/f0_extraction.py"
CONFIG_FILE: Final = "soulxsinger/config/soulxsinger.yaml"
SOURCE_MISSING_TEXT: Final = "The SoulX-Singer source is missing from this image."


@dataclass(frozen=True, slots=True)
class SoulXApi:
    """The parts of SoulX-Singer the singer uses (replaced by fakes in tests).

    Attributes:
        svc: ``soulxsinger.models.soulxsinger_svc.SoulXSingerSVC``.
        load_config: ``soulxsinger.utils.file_utils.load_config``.
        load_wav: ``soulxsinger.utils.audio_utils.load_wav`` (path, rate) -> (1, T) tensor.
        f0_extractor: ``F0Extractor`` of ``f0_extraction.py`` (RMVPE on the SVC frame grid).
        rmvpe: ``RMVPE`` of ``f0_extraction.py`` (10 ms frames, voiced-ratio checks).
        load_16k: Loads an audio file as 16 kHz mono float32 (``librosa.load``).
        torch: The ``torch`` module.
        whisper: ``soulxsinger.models.modules.whisper_encoder`` (patched to load locally).
        config_path: The model configuration file.
    """

    svc: Any
    load_config: Any
    load_wav: Any
    f0_extractor: Any
    rmvpe: Any
    load_16k: Any
    torch: Any
    whisper: Any
    config_path: Path


def load_f0_module(source: Path) -> ModuleType:
    """Load ``preprocess/tools/f0_extraction.py`` of the SoulX source by file path."""
    path = source / F0_MODULE
    spec = importlib.util.spec_from_file_location("stemstudio_soulx_f0_extraction", path)
    if spec is None or spec.loader is None or not path.is_file():
        raise GeneratorError(SOURCE_MISSING_TEXT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def import_soulx() -> SoulXApi:  # pragma: no cover - needs the soulx image variant
    """Import SoulX-Singer, torch and librosa."""
    import librosa  # noqa: PLC0415
    import soulxsinger  # noqa: PLC0415
    import torch  # noqa: PLC0415
    from soulxsinger.models.modules import whisper_encoder  # noqa: PLC0415
    from soulxsinger.models.soulxsinger_svc import SoulXSingerSVC  # noqa: PLC0415
    from soulxsinger.utils.audio_utils import load_wav  # noqa: PLC0415
    from soulxsinger.utils.file_utils import load_config  # noqa: PLC0415

    source = Path(soulxsinger.__file__).resolve().parent.parent
    f0 = load_f0_module(source)
    return SoulXApi(
        svc=SoulXSingerSVC, load_config=load_config, load_wav=load_wav,
        f0_extractor=f0.F0Extractor, rmvpe=f0.RMVPE,
        load_16k=lambda path: librosa.load(path, sr=16_000, mono=True)[0], torch=torch,
        whisper=whisper_encoder, config_path=source / CONFIG_FILE,
    )  # fmt: skip
