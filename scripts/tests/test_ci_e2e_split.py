"""Tests of the split CI E2E jobs (feature 016 T144, LESSONS 014/015; T011 fake-mic outline).

The default Playwright suite runs per browser (``e2e``, matrix ``chromium``/``firefox``) and the
feature passes run in parallel (``e2e-passes``, one matrix entry per pass, each on its own fake
stack): recording, practice-flow, restart, agent, lyric, gpu, instrument and fake-mic. Each job
has a timeout of at most 90 minutes; ``scripts/ci-e2e-pass.sh`` holds the commands of every pass
(``--dry-run`` prints them). The union of the selections is the 014/015 set plus the 016 ones.
The ci.yml part needs PyYAML (``uv run --with pyyaml``); it is skipped without it.
"""

import json
import re
import shutil
import subprocess
from pathlib import Path
from typing import Any

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
PASS_SCRIPT = REPO_ROOT / "scripts" / "ci-e2e-pass.sh"
CI = REPO_ROOT / ".github" / "workflows" / "ci.yml"
PACKAGE = REPO_ROOT / "e2e" / "package.json"
PLAYWRIGHT = REPO_ROOT / "e2e" / "playwright.config.ts"
BASH = shutil.which("bash")

PASSES = {
    "recording": ["E2E_RECORDING_LOOPBACK=1 npm run test:recording"],
    "practice-flow": ["npm run test:practice-flow-no-detect"],
    "restart": ["docker compose restart api web", "npm run test:restart"],
    "agent": ["scripts/agent-e2e.sh fake", "scripts/agent-e2e.sh none"],
    "lyric": ["scripts/lyric-e2e.sh fake", "scripts/lyric-e2e.sh none"],
    "gpu": ["STEMSTUDIO_FAKE_DEVICE=gpu", "STEMSTUDIO_FAKE_DEVICE=gpu-oom", "npm run test:gpu"],
    "instrument": ["npm run test:instrument"],
    "fake-mic": ["npm run test:fake-mic"],
}
"""Every feature pass and what its dry run must show (014/015 set + instrument, fake-mic)."""

FAKE_MIC = {
    "fake-mic-on-pitch": "sing-on-pitch.wav",
    "fake-mic-semitone": "sing-semitone-off.wav",
    "fake-mic-silence": "silence.wav",
}


def _dry(name: str) -> subprocess.CompletedProcess[str]:
    argv = [str(BASH), PASS_SCRIPT.as_posix(), "--dry-run", name]
    return subprocess.run(argv, capture_output=True, text=True, check=False, timeout=60)  # noqa: S603


def _scripts() -> dict[str, str]:
    scripts: dict[str, str] = json.loads(PACKAGE.read_text(encoding="utf-8"))["scripts"]
    return scripts


def _grep_invert(command: str) -> set[str]:
    found = re.search(r'--grep-invert \\?"([^"\\]+)\\?"', command)
    assert found, command
    return set(found.group(1).split("|"))


# ------------------------------------------------------------------ the pass script


@pytest.mark.skipif(BASH is None, reason="bash not available")
@pytest.mark.parametrize(("name", "commands"), sorted(PASSES.items()))
def test_every_pass_names_its_commands(name: str, commands: list[str]) -> None:
    result = _dry(name)

    assert result.returncode == 0, result.stderr
    for command in commands:
        assert command in result.stdout, (name, command)


@pytest.mark.skipif(BASH is None, reason="bash not available")
def test_the_pass_list_is_the_014_015_set_plus_the_016_passes() -> None:
    argv = [str(BASH), PASS_SCRIPT.as_posix(), "--list"]
    listed = subprocess.run(argv, capture_output=True, text=True, check=True, timeout=60)  # noqa: S603

    assert listed.stdout.split() == list(PASSES)


@pytest.mark.skipif(BASH is None, reason="bash not available")
def test_the_gpu_suite_runs_with_the_device_of_its_stack() -> None:
    """The @gpu specs read STEMSTUDIO_FAKE_DEVICE themselves (e2e/support/env.ts), as in 003."""
    lines = _dry("gpu").stdout.splitlines()

    suites = [line for line in lines if "npm run test:gpu" in line]
    assert len(suites) == 2
    assert "STEMSTUDIO_FAKE_DEVICE=gpu " in suites[0]
    assert "STEMSTUDIO_FAKE_DEVICE=gpu-oom " in suites[1]


@pytest.mark.skipif(BASH is None, reason="bash not available")
def test_an_unknown_pass_is_a_usage_error() -> None:
    result = _dry("nope")

    assert result.returncode == 2
    assert "unknown pass" in result.stderr


# ------------------------------------------------------------------ e2e/package.json selections


def test_the_default_suite_runs_per_browser_with_the_same_exclusions() -> None:
    scripts = _scripts()
    excluded = _grep_invert(scripts["test"])

    assert "--project=chromium --project=firefox" in scripts["test"]
    for browser in ("chromium", "firefox"):
        assert f"--project={browser}" in scripts[f"test:{browser}"]
        assert _grep_invert(scripts[f"test:{browser}"]) == excluded
    assert {"@instrument", "@fake-mic", "@recording", "@restart", "@gpu", "@agent"} <= excluded


def test_the_instrument_and_fake_mic_scripts_select_the_016_specs() -> None:
    scripts = _scripts()

    assert "--grep @instrument" in scripts["test:instrument"]
    assert "@fake-mic" in _grep_invert(scripts["test:instrument"])
    for project in FAKE_MIC:
        assert f"--project={project}" in scripts["test:fake-mic"]


def test_the_fake_mic_projects_feed_the_qa_fixtures_to_chromium() -> None:
    config = PLAYWRIGHT.read_text(encoding="utf-8")

    for project, wav in FAKE_MIC.items():
        assert f"'{project}'" in config and wav in config
    assert "--use-file-for-fake-audio-capture=" in config
    assert "--use-fake-device-for-media-stream" in config
    assert re.search(r"name: 'chromium', grepInvert: /@recording\|@fake-mic/", config)


# ------------------------------------------------------------------ ci.yml


def _jobs() -> dict[str, Any]:
    yaml = pytest.importorskip("yaml")
    jobs: dict[str, Any] = yaml.safe_load(CI.read_text(encoding="utf-8"))["jobs"]
    return jobs


def _runs(job: dict[str, Any]) -> str:
    return "\n".join(step.get("run", "") for step in job["steps"])


def test_the_default_suite_job_is_a_browser_matrix_under_90_minutes() -> None:
    job = _jobs()["e2e"]

    assert job["strategy"]["matrix"]["browser"] == ["chromium", "firefox"]
    assert job["strategy"]["fail-fast"] is False
    assert job["timeout-minutes"] <= 90
    assert "npm run test:${{ matrix.browser }}" in _runs(job)


def test_the_feature_passes_run_in_parallel_under_90_minutes() -> None:
    job = _jobs()["e2e-passes"]

    assert job["strategy"]["matrix"]["pass"] == list(PASSES)
    assert job["strategy"]["fail-fast"] is False
    assert job["timeout-minutes"] <= 90
    assert "scripts/ci-e2e-pass.sh ${{ matrix.pass }}" in _runs(job)


def test_both_e2e_jobs_start_their_own_fake_stack_and_tear_it_down() -> None:
    jobs = _jobs()
    for name in ("e2e", "e2e-passes"):
        uses = [step.get("uses", "") for step in jobs[name]["steps"]]
        assert "./.github/actions/fake-stack" in uses, name
        assert "docker compose down -v" in _runs(jobs[name]), name


def test_the_web_serves_the_engine_worklets_in_the_default_job() -> None:
    assert "scripts/check-web-assets.sh" in _runs(_jobs()["e2e"])
