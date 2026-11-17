"""API image size check (023 T069, ADR-037): the vendored lyric dictionaries stay under +15 MB.

The dictionaries (``src/StemStudio.Api/LyricAssist/Dictionaries``) are copied into the published
output and so into the image layer of ``/app``; nothing is downloaded at run time. Two checks, one
line each, exit code 1 when any fails:

1. the dictionaries folder inside the image (``/app/LyricAssist/Dictionaries``) is below the budget;
2. when ``--baseline-image`` names an api image built before the dictionaries existed, the whole
   image is less than the budget larger than that baseline (one-off measurement, not run in CI).

Used by ``scripts/check-api-image-size.{sh,ps1}``. Read-only: ``docker image inspect`` and a
throwaway ``docker run --rm --entrypoint du``. Standard library only.
"""

from __future__ import annotations

import argparse
import subprocess
import sys
from collections.abc import Callable, Sequence
from dataclasses import dataclass

BUDGET_BYTES = 15_000_000
DICTIONARIES_DIR = "/app/LyricAssist/Dictionaries"
_MB = 1_000_000

Docker = Callable[[Sequence[str]], str]


@dataclass(frozen=True, slots=True)
class Check:
    """One check result; ``line()`` is the output line."""

    ok: bool
    name: str
    message: str

    def line(self) -> str:
        """``ok   <name>: <message>`` or ``FAIL <name>: <message>``."""
        return f"{'ok  ' if self.ok else 'FAIL'} {self.name}: {self.message}"


def run_docker(arguments: Sequence[str]) -> str:
    """Run ``docker <arguments>`` and return its standard output; a failure raises."""
    done = subprocess.run(  # noqa: S603 - fixed program, arguments are not shell-interpreted
        ["docker", *arguments],  # noqa: S607 - docker comes from PATH, like in the other scripts
        check=True,
        capture_output=True,
        text=True,
    )
    return done.stdout


def parse_du(output: str) -> int:
    """Bytes of ``du -sb <path>`` output (``<bytes>`` TAB ``<path>``)."""
    return int(output.split(maxsplit=1)[0])


def image_bytes(image: str, docker: Docker) -> int:
    """Uncompressed size of ``image`` (``docker image inspect``)."""
    return int(docker(["image", "inspect", "--format", "{{.Size}}", image]).strip())


def dictionaries_bytes(image: str, docker: Docker) -> int:
    """Bytes of the vendored dictionaries inside ``image`` (``du -sb`` in a throwaway container)."""
    output = docker(["run", "--rm", "--entrypoint", "du", image, "-sb", DICTIONARIES_DIR])
    return parse_du(output)


def _mb(size: int) -> str:
    return f"{size / _MB:.1f} MB"


def check_dictionaries(size: int, budget: int) -> Check:
    """The dictionaries folder is below the budget."""
    name = "vendored dictionaries"
    text = f"{_mb(size)} in {DICTIONARIES_DIR}"
    if size < budget:
        return Check(ok=True, name=name, message=f"{text} (budget {_mb(budget)})")
    return Check(ok=False, name=name, message=f"{text}, budget {_mb(budget)}")


def check_growth(size: int, baseline: int, budget: int) -> Check:
    """The image grew by less than the budget compared with the baseline image."""
    growth = size - baseline
    text = f"{_mb(size)} against baseline {_mb(baseline)}: {growth / _MB:+.1f} MB"
    if growth < budget:
        return Check(ok=True, name="api image growth", message=f"{text} (budget {_mb(budget)})")
    return Check(ok=False, name="api image growth", message=f"{text}, budget {_mb(budget)}")


def run_checks(
    image: str, baseline_image: str | None, budget: int, docker: Docker = run_docker
) -> list[Check]:
    """All checks for ``image``; the growth check only with a ``baseline_image``."""
    checks = [check_dictionaries(dictionaries_bytes(image, docker), budget)]
    if baseline_image:
        growth = check_growth(
            image_bytes(image, docker), image_bytes(baseline_image, docker), budget
        )
        checks.append(growth)
    return checks


def parse_args(argv: Sequence[str] | None) -> argparse.Namespace:
    """Command line: ``--image`` (required), ``--baseline-image``, ``--budget-bytes``."""
    parser = argparse.ArgumentParser(description="API image size check (023 T069).")
    parser.add_argument("--image", required=True, help="the api image to measure")
    parser.add_argument("--baseline-image", help="api image from before the dictionaries")
    parser.add_argument("--budget-bytes", type=int, default=BUDGET_BYTES)
    return parser.parse_args(argv)


def main(argv: Sequence[str] | None = None, docker: Docker = run_docker) -> int:
    """Print one line per check; return 1 when any check failed or docker could not run."""
    args = parse_args(argv)
    try:
        checks = run_checks(args.image, args.baseline_image, args.budget_bytes, docker)
    except subprocess.CalledProcessError as error:
        detail = (error.stderr or "").strip() or f"exit code {error.returncode}"
        command = " ".join(map(str, error.cmd))
        sys.stdout.write(f"FAIL docker: {command}: {detail}" + "\n")
        return 1
    for check in checks:
        sys.stdout.write(check.line() + "\n")
    return 0 if all(check.ok for check in checks) else 1


if __name__ == "__main__":
    sys.exit(main())
