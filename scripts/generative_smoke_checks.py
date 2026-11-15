"""Generative smoke checks (009 T010 / quickstart Q3, 010 T008): run INSIDE a generator image.

Modes (``python -I - <mode> [arg]`` with this file on stdin, see
``scripts/generative-smoke.{sh,ps1}``):

* ``runtime`` - with ``--gpus all``: torch is ``2.10.0+cu128``, CUDA is available, the GPU's
  ``sm_<cc>`` is in the arch list and a matmul runs.
* ``imports <acestep|soulx>`` - the engine's own modules import: ACE-Step's handler, inference
  and LM modules; or SoulX-Singer's SVC model class (source on ``PYTHONPATH``) and RMVPE loaded
  from ``preprocess/tools/f0_extraction.py`` by file path (ADR-019 Phase 0 outcome).
* ``models <acestep|soulx>`` - the pre-downloaded weights in ``STEMSTUDIO_MODEL_DIR`` have their
  ``REVISION`` and ``SHA256SUMS`` files, and every file listed there exists with a size > 0.
* ``health <kind>`` - polls ``/health`` until it answers; ``ready`` is true and ``kinds``
  contains ``kind`` (``restyle`` for the generator, ``voiceConversion`` for the singer).
* ``restyle`` - one 10 s range restyle of a synthetic 12 s stem, one take; the take is FLAC.
* ``convert`` - one 10 s voice conversion (synthetic harmonic target and 20 s prompt); the remix
  and the vocal stem are FLAC.

Exit code 0 when every check passes, 1 otherwise. Standard library plus torch / the engine's own
packages only; no network access outside the container.
"""

import importlib
import importlib.util
import json
import math
import os
import struct
import sys
import time
import urllib.error
import urllib.request
import uuid
from collections.abc import Callable
from pathlib import Path

TORCH_VERSION = "2.10.0+cu128"
BASE_URL = "http://127.0.0.1:8010"
HEALTH_TIMEOUT_SECONDS = 180.0
JOB_TIMEOUT_SECONDS = 1200.0
SAMPLE_RATE = 48_000
WEIGHT_DIRS = {"acestep": ("acestep/checkpoints",), "soulx": ("soulx", "soulx/whisper-base")}
SOULX_ROOT = Path("/opt/soulx-singer")


def report(ok: bool, text: str) -> bool:
    """Print ``OK``/``FAIL`` with ``text`` and return ``ok``."""
    sys.stdout.write(f"{'OK  ' if ok else 'FAIL'} {text}\n")
    return ok


# Runtime, imports, models.
def check_runtime() -> bool:
    """Check the torch build, CUDA, the GPU's architecture and a matmul."""
    import torch  # noqa: PLC0415 - heavy import only in this mode

    if not report(torch.__version__ == TORCH_VERSION, f"torch {torch.__version__}"):
        return False
    if not report(torch.cuda.is_available(), "torch.cuda.is_available()"):
        return False
    major, minor = torch.cuda.get_device_capability(0)
    archs = torch.cuda.get_arch_list()
    if not report(f"sm_{major}{minor}" in archs, f"{torch.cuda.get_device_name(0)} in {archs}"):
        return False
    a = torch.randn(2048, 2048, device="cuda", dtype=torch.bfloat16)
    return report(bool(torch.isfinite(a @ a).all().item()), "2048x2048 bf16 matmul on cuda")


def import_ok(module: str) -> bool:
    """Import ``module`` and report the result."""
    try:
        importlib.import_module(module)
    except Exception as error:  # any import failure is a finding
        return report(False, f"import {module}: {type(error).__name__}: {error}")
    return report(True, f"import {module}")


def load_by_path(path: Path) -> bool:
    """Load a module from ``path`` (not through its package ``__init__``) and report it."""
    try:
        spec = importlib.util.spec_from_file_location(path.stem, path)
        module = importlib.util.module_from_spec(spec)  # type: ignore[arg-type]
        spec.loader.exec_module(module)  # type: ignore[union-attr]
    except Exception as error:  # any load failure is a finding
        return report(False, f"load {path}: {type(error).__name__}: {error}")
    return report(hasattr(module, "RMVPE"), f"load {path} (RMVPE class present)")


def check_imports(engine: str) -> bool:
    """Check that the engine's own modules import."""
    if engine == "acestep":
        modules = ("acestep.handler", "acestep.inference", "acestep.llm_inference")
        results = [import_ok(name) for name in modules]  # every import is reported
        return all(results)
    # ``python -I`` ignores PYTHONPATH: check the image's value, then apply it to this process.
    on_path = str(SOULX_ROOT) in os.environ.get("PYTHONPATH", "").split(os.pathsep)
    sys.path.insert(0, str(SOULX_ROOT))
    svc = report(on_path, f"PYTHONPATH contains {SOULX_ROOT}")
    svc = import_ok("soulxsinger.models.soulxsinger_svc") and svc
    return load_by_path(SOULX_ROOT / "preprocess/tools/f0_extraction.py") and svc


def listed_files(folder: Path) -> list[str]:
    """Return the file names listed in ``folder/SHA256SUMS`` (sha256sum format)."""
    lines = (folder / "SHA256SUMS").read_text(encoding="utf-8").splitlines()
    return [line.split(maxsplit=1)[1].lstrip("*") for line in lines if line.strip()]


def check_weight_folder(folder: Path) -> bool:
    """Check ``REVISION``, ``SHA256SUMS`` and the listed files of one weights folder."""
    if not report((folder / "REVISION").is_file() and (folder / "SHA256SUMS").is_file(),
                  f"{folder}: REVISION and SHA256SUMS"):  # fmt: skip
        return False
    names = listed_files(folder)
    missing = [name for name in names if not _non_empty(folder / name)]
    return report(not missing, f"{folder}: {len(names)} listed files present; missing {missing}")


def _non_empty(path: Path) -> bool:
    """True when ``path`` is a file with at least one byte."""
    return path.is_file() and path.stat().st_size > 0


def check_models(engine: str) -> bool:
    """Check every pre-downloaded weights folder of the engine."""
    root = Path(os.environ.get("STEMSTUDIO_MODEL_DIR", "/models"))
    results = [check_weight_folder(root / name) for name in WEIGHT_DIRS[engine]]
    return all(results)


# HTTP helpers.
def http_json(method: str, path: str, body: bytes | None = None, ctype: str = "") -> object:
    """Send one request to the service and return the decoded JSON answer."""
    headers = {"Content-Type": ctype} if ctype else {}
    request = urllib.request.Request(BASE_URL + path, body, headers, method=method)  # noqa: S310
    with urllib.request.urlopen(request, timeout=30) as response:  # noqa: S310 - fixed local URL
        return json.loads(response.read().decode("utf-8"))


def http_bytes(path: str) -> bytes:
    """GET ``path`` and return the body."""
    with urllib.request.urlopen(BASE_URL + path, timeout=60) as response:  # noqa: S310
        return bytes(response.read())


def wait_for_health() -> dict[str, object] | None:
    """Poll ``/health`` until it answers or the timeout passes."""
    deadline = time.monotonic() + HEALTH_TIMEOUT_SECONDS
    while time.monotonic() < deadline:
        try:
            return dict(http_json("GET", "/health"))  # type: ignore[call-overload]
        except (urllib.error.URLError, OSError, ValueError):
            time.sleep(2)
    return None


def check_health(kind: str) -> bool:
    """Check that the service is ready and serves ``kind``."""
    health = wait_for_health()
    if not report(health is not None, f"{BASE_URL}/health answers"):
        return False
    sys.stdout.write(json.dumps(health) + "\n")
    ready = report(health.get("ready") is True, f"ready (reason {health.get('reason')!r})")  # type: ignore[union-attr]
    kinds = health.get("kinds") or []  # type: ignore[union-attr]
    return report(kind in kinds, f"kinds {kinds} contain {kind}") and ready  # type: ignore[operator]


# Synthetic audio + multipart.
def tone_wav(seconds: float, base_hz: float) -> bytes:
    """Return a 48 kHz 16-bit stereo WAV of a harmonic tone with vibrato (voice-like pitch)."""
    frames = int(seconds * SAMPLE_RATE)
    samples = bytearray()
    for n in range(frames):
        t = n / SAMPLE_RATE
        f = base_hz * (1 + 0.02 * math.sin(2 * math.pi * 5 * t))
        v = sum(math.sin(2 * math.pi * f * k * t) / k for k in (1, 2, 3)) * 0.25
        samples += struct.pack("<hh", int(v * 32767), int(v * 32767))
    return _wav_header(len(samples)) + bytes(samples)


def _wav_header(data_bytes: int) -> bytes:
    """Return the 44-byte RIFF header of a 48 kHz 16-bit stereo PCM WAV."""
    fmt = struct.pack("<HHIIHH", 1, 2, SAMPLE_RATE, SAMPLE_RATE * 4, 4, 16)
    return (b"RIFF" + struct.pack("<I", 36 + data_bytes) + b"WAVEfmt " + struct.pack("<I", 16)
            + fmt + b"data" + struct.pack("<I", data_bytes))  # fmt: skip


def multipart(params: dict[str, object], files: list[tuple[str, bytes]]) -> tuple[bytes, str]:
    """Encode ``params`` (JSON part) and the audio ``files`` as multipart/form-data."""
    boundary = uuid.uuid4().hex
    parts = [_part(boundary, 'name="params"', "application/json", json.dumps(params).encode())]
    for index, (name, data) in enumerate(files):
        disposition = f'name="{name}"; filename="{name}-{index}.wav"'
        parts.append(_part(boundary, disposition, "audio/wav", data))
    body = b"".join(parts) + f"--{boundary}--\r\n".encode()
    return body, f"multipart/form-data; boundary={boundary}"


def _part(boundary: str, disposition: str, ctype: str, data: bytes) -> bytes:
    """Return one multipart part."""
    head = f"--{boundary}\r\nContent-Disposition: form-data; {disposition}\r\n"
    return (head + f"Content-Type: {ctype}\r\n\r\n").encode() + data + b"\r\n"


# Jobs.
def run_job(params: dict[str, object], files: list[tuple[str, bytes]]) -> dict[str, object] | None:
    """Create a job, poll it until it ends and return the final generation (None on timeout)."""
    body, ctype = multipart(params, files)
    job = dict(http_json("POST", "/v1/generations", body, ctype))  # type: ignore[call-overload]
    started = time.monotonic()
    while job.get("status") not in ("completed", "failed", "cancelled"):
        if time.monotonic() - started > JOB_TIMEOUT_SECONDS:
            return None
        time.sleep(2)
        job = dict(http_json("GET", f"/v1/generations/{job['generationId']}"))  # type: ignore[call-overload]
    report(True, f"{params['kind']} {job['status']} in {time.monotonic() - started:.1f} s")
    return job


def check_flac(path: str) -> bool:
    """GET ``path`` and check that it is a FLAC file."""
    data = http_bytes(path)
    return report(data[:4] == b"fLaC", f"{path}: FLAC, {len(data):,} bytes")


def check_completed(job: dict[str, object] | None, suffixes: tuple[str, ...]) -> bool:
    """Check that ``job`` completed with one take and that its audio files are FLAC."""
    if not report(job is not None and job.get("status") == "completed", f"job ended: {job}"):
        return False
    base = f"/v1/generations/{job['generationId']}/takes/1"  # type: ignore[index]
    results = [check_flac(base + suffix) for suffix in suffixes]
    url = f"{BASE_URL}/v1/generations/{job['generationId']}"  # type: ignore[index]
    delete = urllib.request.Request(url, method="DELETE")  # noqa: S310 - fixed local URL
    with urllib.request.urlopen(delete, timeout=30):  # noqa: S310
        pass
    return all(results)


def check_restyle() -> bool:
    """Run one 10 s range restyle with one take."""
    params = {"kind": "restyle", "restyle": {
        "caption": "Jazz trio, swing feel, piano, upright bass, brushed drums, warm",
        "audioCoverStrength": 0.55, "inferenceSteps": 8, "shift": 3.0, "seeds": [42],
        "mode": "range", "rangeStartSeconds": 1.0, "rangeEndSeconds": 11.0}}  # fmt: skip
    return check_completed(run_job(params, [("stem", tone_wav(12.0, 220.0))]), ("",))


def check_convert() -> bool:
    """Run one 10 s voice conversion (target 10 s, prompt 20 s, no accompaniment)."""
    params = {"kind": "voiceConversion", "voiceConversion": {
        "pitchShift": 0, "autoShift": True, "steps": 32, "cfg": 3.0, "vocalGainDb": 0,
        "profileName": "Smoke test"}}  # fmt: skip
    files = [("target", tone_wav(10.0, 196.0)), ("prompt", tone_wav(20.0, 247.0))]
    return check_completed(run_job(params, files), ("", "/vocal"))


MODES: dict[str, Callable[..., bool]] = {
    "runtime": check_runtime,
    "imports": check_imports,
    "models": check_models,
    "health": check_health,
    "restyle": check_restyle,
    "convert": check_convert,
}


def main(argv: list[str]) -> int:
    """Run the mode named in ``argv``; 0 when every check passes, 1 otherwise, 2 on usage."""
    if not argv or argv[0] not in MODES:
        sys.stdout.write(f"usage: python -I - <{'|'.join(MODES)}> [arg]\n")
        return 2
    return 0 if MODES[argv[0]](*argv[1:]) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
