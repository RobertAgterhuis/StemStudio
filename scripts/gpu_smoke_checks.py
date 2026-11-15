"""GPU smoke checks (feature 003, FR-009, FR-003; quickstart G3/G4): run INSIDE the worker image.

Modes (``python -I - <mode> [arg]`` with this file on stdin, see ``scripts/gpu-smoke.{sh,ps1}``):

* ``runtime`` - with ``--gpus all``: torch is a ``+cu132`` build, CUDA is available, the GPU's
  ``sm_<cc>`` is in the arch list, a matmul runs, torchaudio is a ``+cu132`` build that resamples
  on cuda (006), and onnxruntime uses the CUDA execution
  provider after ``onnxruntime.preload_dlls()`` (a one-node ONNX model, encoded here without the
  onnx package).
* ``health <gpu|cpu>`` - polls the worker's ``/health`` until it answers and checks
  ``deviceInfo.kind``; for ``cpu`` the ``reason`` must be set (no usable GPU, FR-003).

Exit code 0 when every check passes, 1 otherwise. Standard library plus torch/onnxruntime only.
"""

import json
import sys
import time
import urllib.request
from collections.abc import Callable

TORCH_SUFFIX = "+cu132"
HEALTH_URL = "http://127.0.0.1:8000/health"
HEALTH_TIMEOUT_SECONDS = 120.0


def report(ok: bool, text: str) -> bool:
    """Print ``OK``/``FAIL`` with ``text`` and return ``ok``."""
    sys.stdout.write(f"{'OK  ' if ok else 'FAIL'} {text}\n")
    return ok


def check_torch() -> bool:
    """Check the torch build, CUDA availability, the GPU architecture and a matmul."""
    import torch  # noqa: PLC0415 - heavy import only in this mode

    if not report(torch.__version__.endswith(TORCH_SUFFIX), f"torch {torch.__version__}"):
        return False
    if not report(torch.cuda.is_available(), "torch.cuda.is_available()"):
        return False
    return check_arch_and_matmul()


def check_arch_and_matmul() -> bool:
    """Check that the GPU's ``sm_<cc>`` is in torch's arch list and that a matmul runs."""
    import torch  # noqa: PLC0415

    major, minor = torch.cuda.get_device_capability(0)
    arch = f"sm_{major}{minor}"
    archs = torch.cuda.get_arch_list()
    name = torch.cuda.get_device_name(0)
    if not report(arch in archs, f"{name}: {arch} in {archs}"):
        return False
    a = torch.randn(2048, 2048, device="cuda")
    finite = bool(torch.isfinite(a @ a).all().item())
    return report(finite, "2048x2048 matmul on cuda")


def varint(value: int) -> bytes:
    """Encode ``value`` as a protobuf varint."""
    out = bytearray()
    while True:
        byte, value = value & 0x7F, value >> 7
        out.append(byte | (0x80 if value else 0))
        if not value:
            return bytes(out)


def field(number: int, payload: bytes | int) -> bytes:
    """Encode one protobuf field: varint for ``int``, length-delimited for ``bytes``."""
    if isinstance(payload, int):
        return varint(number << 3) + varint(payload)
    return varint(number << 3 | 2) + varint(len(payload)) + payload


def value_info(name: str) -> bytes:
    """Encode an ONNX ValueInfoProto for a float tensor of shape [4]."""
    shape = field(2, field(1, field(1, 4)))
    tensor_type = field(1, field(1, 1) + shape)
    return field(1, name.encode()) + field(2, tensor_type)


def relu_model() -> bytes:
    """Return a serialised one-node ONNX model ``y = Relu(x)`` (IR 8, opset 13)."""
    node = field(1, b"x") + field(2, b"y") + field(4, b"Relu")
    graph = field(1, node) + field(2, b"smoke") + field(11, value_info("x"))
    graph += field(12, value_info("y"))
    opset = field(1, b"") + field(2, 13)
    return field(1, 8) + field(7, graph) + field(8, opset)


def check_onnxruntime() -> bool:
    """Check that an onnxruntime session runs on the CUDA execution provider."""
    import numpy as np  # noqa: PLC0415
    import onnxruntime as ort  # noqa: PLC0415

    ort.preload_dlls()
    session = ort.InferenceSession(relu_model(), providers=["CUDAExecutionProvider"])
    active = session.get_providers()
    if not report(active[0] == "CUDAExecutionProvider", f"onnxruntime {ort.__version__} {active}"):
        return False
    result = session.run(None, {"x": np.array([-1.0, 0.0, 1.0, 2.0], dtype=np.float32)})[0]
    return report(result.tolist() == [0.0, 0.0, 1.0, 2.0], "Relu on CUDAExecutionProvider")


def fetch_health() -> dict[str, object]:
    """Return the JSON of ``/health``, retrying until it answers or the timeout passes."""
    deadline = time.monotonic() + HEALTH_TIMEOUT_SECONDS
    while True:
        try:
            with urllib.request.urlopen(HEALTH_URL, timeout=4) as response:
                return dict(json.loads(response.read().decode()))
        except OSError:
            if time.monotonic() > deadline:
                raise
            time.sleep(2)


def check_health(expected: str) -> bool:
    """Check ``deviceInfo.kind == expected`` (and a reason when the CPU is used)."""
    health = fetch_health()
    sys.stdout.write(json.dumps(health) + "\n")
    info = health.get("deviceInfo")
    if not isinstance(info, dict):
        return report(False, "/health has deviceInfo")
    if not report(info.get("kind") == expected, f"deviceInfo.kind == {expected}"):
        return False
    return expected == "gpu" or report(bool(info.get("reason")), f"reason: {info.get('reason')}")


def check_torchaudio() -> bool:
    """Check the torchaudio build (006: ``+cu132`` next to torch) and a resample on cuda."""
    import torch  # noqa: PLC0415
    import torchaudio  # noqa: PLC0415

    version = torchaudio.__version__
    if not report(version.endswith(TORCH_SUFFIX), f"torchaudio {version}"):
        return False
    wave = torch.randn(1, 16000, device="cuda")
    resampled = torchaudio.functional.resample(wave, 16000, 8000)
    return report(tuple(resampled.shape) == (1, 8000), "torchaudio resample on cuda")


def check_runtime() -> bool:
    """Run the torch, torchaudio and onnxruntime checks (stops at the first failure)."""
    return check_torch() and check_torchaudio() and check_onnxruntime()


MODES: dict[tuple[str, ...], Callable[[], bool]] = {
    ("runtime",): check_runtime,
    ("health", "gpu"): lambda: check_health("gpu"),
    ("health", "cpu"): lambda: check_health("cpu"),
}


def main(argv: list[str]) -> int:
    """Run the mode named in ``argv`` and return the exit code (2 for an unknown mode)."""
    check = MODES.get(tuple(argv))
    if check is None:
        sys.stdout.write(f"usage: python - runtime | health <gpu|cpu> (got {argv})\n")
        return 2
    return 0 if check() else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
