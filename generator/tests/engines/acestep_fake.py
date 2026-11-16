"""A fake of the ACE-Step 1.5 Python API (handler, LM handler, ``generate_music``)."""

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from stemstudio_generator.engines.acestep_engine import AceStepApi

from ..signals import stereo, tone, write


@dataclass
class Params:
    """``GenerationParams`` stand-in (keyword arguments kept as attributes)."""

    values: dict[str, Any]

    def __getattr__(self, name: str) -> Any:
        return self.values[name]


def params(**values: Any) -> Params:
    return Params(values)


def config(**values: Any) -> Params:
    return Params(values)


@dataclass
class Result:
    success: bool
    audios: list[dict[str, Any]]
    error: str | None = None


@dataclass
class Recorder:
    """Shared record of every call the engine makes."""

    inits: list[dict[str, Any]] = field(default_factory=list)
    llm_inits: list[dict[str, Any]] = field(default_factory=list)
    calls: list[tuple[Params, Params, str]] = field(default_factory=list)
    llm_unloads: int = 0
    progress: list[tuple[float, str]] = field(default_factory=list)
    init_result: tuple[str, bool] = ("ok", True)
    result: Result | None = None
    raise_in_generate: BaseException | None = None
    take_seconds: float = 2.0
    llm_result: tuple[str, bool] | None = None
    pretrained: list[dict[str, Any]] = field(default_factory=list)
    moved: list[str] = field(default_factory=list)
    pretrained_error: BaseException | None = None


class _Handler:
    recorder: Recorder

    def initialize_service(self, **kwargs: Any) -> tuple[str, bool]:
        self.recorder.inits.append(kwargs)
        return self.recorder.init_result


class _Llm:
    """Mirrors ``LLMHandler.initialize``: sets dtype/offload, then ``_load_pytorch_model``."""

    recorder: Recorder
    llm_initialized = False

    def initialize(self, **kwargs: Any) -> tuple[str, bool]:
        self.recorder.llm_inits.append(kwargs)
        if self.recorder.llm_result is not None:
            return self.recorder.llm_result
        self.dtype, self.offload_to_cpu = kwargs["dtype"], kwargs["offload_to_cpu"]
        path = f"{kwargs['checkpoint_dir']}/{kwargs['lm_model_path']}"
        ok, status = self._load_pytorch_model(path, kwargs["device"])
        return status, ok

    def _load_pytorch_model(self, model_path: str, device: str) -> tuple[bool, str]:
        raise AssertionError("the engine must override the fp32 loader")

    def unload(self) -> None:
        self.recorder.llm_unloads += 1


class _Generate:
    def __init__(self, recorder: Recorder) -> None:
        self.recorder = recorder

    def __call__(self, *args: Any, **kwargs: Any) -> Result:
        _, _, p, c = args
        save_dir, progress = kwargs["save_dir"], kwargs["progress"]
        self.recorder.calls.append((p, c, save_dir))
        for value, desc in ((0.3, "Generating music (batch size: 1)..."), (0.9, "Decoding...")):
            try:  # ACE-Step calls progress from a thread that swallows exceptions
                progress(value, desc=desc)
            except Exception:  # noqa: S112 - mirrors ACE-Step
                continue
        if self.recorder.raise_in_generate is not None:
            raise self.recorder.raise_in_generate
        return self.recorder.result or _audios(c, Path(save_dir), self.recorder.take_seconds)


def fake_api(recorder: Recorder) -> AceStepApi:
    """An ``AceStepApi`` over fakes that write short FLAC takes into ``save_dir``."""
    handler = type("Handler", (_Handler,), {"recorder": recorder})
    llm = type("Llm", (_Llm,), {"recorder": recorder})
    return AceStepApi(
        handler=handler,
        llm=llm,
        params=params,
        config=config,
        generate=_Generate(recorder),
        torch=_Torch(),
        causal_lm=_CausalLm(recorder),
    )


class _Model:
    def __init__(self, recorder: Recorder) -> None:
        self.recorder = recorder

    def to(self, device: str) -> "_Model":
        self.recorder.moved.append(device)
        return self

    def eval(self) -> "_Model":
        return self


class _CausalLm:
    """``transformers.AutoModelForCausalLM`` stand-in."""

    def __init__(self, recorder: Recorder) -> None:
        self.recorder = recorder

    def from_pretrained(self, path: str, **kwargs: Any) -> _Model:
        if self.recorder.pretrained_error is not None:
            raise self.recorder.pretrained_error
        self.recorder.pretrained.append(kwargs)
        return _Model(self.recorder)


def _audios(c: Params, save_dir: Path, seconds: float) -> Result:
    audios = []
    for number, seed in enumerate(c.seeds):
        path = write(save_dir / f"key{number}.flac", stereo(tone(seconds)), 48_000)
        audios.append({"path": str(path), "key": f"key{number}", "params": {"seed": seed}})
    return Result(success=True, audios=audios)


class _Cuda:
    def __init__(self) -> None:
        self.emptied = 0

    def empty_cache(self) -> None:
        self.emptied += 1


class _Torch:
    def __init__(self) -> None:
        self.cuda = _Cuda()
        self.bfloat16 = "bfloat16"
