"""The song path of the ACE-Step engine (010 US1, research S-04/S-07, ADR-018 D6).

* ``song_values``: ``text2music`` parameters. ``planned`` runs the 1.7B LM planner
  (``thinking=True``, metadata by chain of thought); ``direct`` runs the DiT alone with the
  request's BPM, key and time signature or 120 BPM in 4/4.
* ``init_lm``: the LM with the PyTorch (``pt``) backend; vLLM keeps its VRAM after unload and
  refused a 3-minute song on 16 GB. ACE-Step's own loader reads the weights in fp32 and casts
  afterwards (15.4 GB peak); ``bf16_llm`` overrides that loader so the weights are read
  directly in bf16 (11.8 GB, the measured ``SONG_PLANNED`` need). No upstream file is changed.
"""

from typing import Any, Final

from stemstudio_generator.engines.acestep_api import LOAD_FAILED_TEXT, AceStepApi, failure
from stemstudio_generator.ports import EngineSong
from stemstudio_generator.schemas import SongParams

LM_MODEL: Final = "acestep-5Hz-lm-1.7B"
"""LM folder in the checkpoint directory."""

_PLANNED_DEFAULTS: Final[dict[str, Any]] = {"bpm": None, "keyscale": "", "timesignature": ""}
_DIRECT_DEFAULTS: Final[dict[str, Any]] = {"bpm": 120, "keyscale": "", "timesignature": "4"}


def song_values(request: EngineSong, planned: bool) -> dict[str, Any]:
    """``GenerationParams`` of a song."""
    params = request.params
    return {
        "task_type": "text2music", "caption": params.caption, "lyrics": params.lyrics,
        "instrumental": params.instrumental, "vocal_language": params.vocal_language,
        "duration": params.duration_seconds, "inference_steps": params.inference_steps,
        "shift": params.shift, "thinking": planned, "use_cot_metas": planned,
        "use_cot_caption": False, "use_cot_language": False, **_metadata(params, planned),
    }  # fmt: skip


def _metadata(params: SongParams, planned: bool) -> dict[str, Any]:
    """BPM, key and time signature: the request's; the planner fills gaps, else 120 BPM 4/4."""
    given = {
        "bpm": params.bpm,
        "keyscale": params.key_scale,
        "timesignature": params.time_signature,
    }
    defaults = _PLANNED_DEFAULTS if planned else _DIRECT_DEFAULTS
    return {key: defaults[key] if value is None else value for key, value in given.items()}


def bf16_llm(api: AceStepApi) -> Any:
    """``LLMHandler`` subclass whose PyTorch loader reads the weights directly in bf16."""
    causal_lm = api.causal_lm

    class Bf16LLMHandler(api.llm):  # type: ignore[name-defined,misc]
        """ACE-Step's LM handler with a bf16 ``from_pretrained`` (research S-07)."""

        def _load_pytorch_model(self, model_path: str, device: str) -> tuple[bool, str]:
            try:
                model = causal_lm.from_pretrained(
                    model_path, trust_remote_code=True, torch_dtype=self.dtype
                )
                self.llm = model.to("cpu" if self.offload_to_cpu else device)
                self.llm.eval()
            except (OSError, RuntimeError, ValueError) as error:
                return False, f"Error initializing 5Hz LM: {error}"
            self.llm_backend = "pt"
            self.llm_initialized = True
            return True, "5Hz LM initialized in bf16 (PyTorch backend)"

    return Bf16LLMHandler


def init_lm(api: AceStepApi, checkpoint_dir: str) -> Any:
    """Initialise the LM planner on CUDA in bf16 with the ``pt`` backend."""
    llm = bf16_llm(api)()
    status, ok = llm.initialize(
        checkpoint_dir=checkpoint_dir, lm_model_path=LM_MODEL, backend="pt", device="cuda",
        offload_to_cpu=False, dtype=api.torch.bfloat16,
    )  # fmt: skip
    if not ok:
        raise failure(str(status), LOAD_FAILED_TEXT)
    return llm
