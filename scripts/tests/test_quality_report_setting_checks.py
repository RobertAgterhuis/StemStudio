"""Tests for scripts/quality_report_setting_checks.py (feature 020, report off by default)."""

import io
import json
import os
import shutil
import subprocess
from pathlib import Path
from typing import Any

import pytest

import quality_report_setting_checks as qrs

REPO = Path(__file__).resolve().parents[2]


def config(api: str | None = "false", web: str | None = "false", **others: str) -> dict[str, Any]:
    """A resolved config with the setting on api and web (None = absent) and other services."""

    def service(value: str | None) -> dict[str, Any]:
        return {"environment": {} if value is None else {qrs.SETTING: value}}

    services = {"api": service(api), "web": service(web), "worker": {"environment": {"X": "1"}}}
    services.update({name: service(value) for name, value in others.items()})
    return {"services": services}


def test_the_base_file_shape_passes_every_check_with_false() -> None:
    assert qrs.run_checks(config(), "false") == [
        ("api and web carry the quality report setting", None),
        ("api and web have it false", None),
        ("no other service carries it", None),
    ]


def test_the_override_shape_passes_with_true_in_any_case() -> None:
    assert all(problem is None for _, problem in qrs.run_checks(config("True", " TRUE "), "true"))


def test_a_missing_setting_is_reported_per_service() -> None:
    assert qrs.check_present(config(None, None)) == f"no {qrs.SETTING}: api, web"
    assert qrs.check_present(config("false", None)) == f"no {qrs.SETTING}: web"


def test_a_wrong_value_names_the_service_and_value() -> None:
    assert qrs.check_value(config("true", "false"), "false") == "expected false, found api=true"
    assert qrs.check_value(config(None, "false"), "true") == (
        "expected true, found api=None; web=false"
    )


def test_another_service_with_the_setting_fails() -> None:
    assert qrs.check_only_api_and_web(config(worker2="true", generator="false")) == (
        "also set in: generator, worker2"
    )


@pytest.mark.parametrize("document", [{}, {"services": []}, {"services": {"api": "x"}}])
def test_odd_documents_count_as_missing(document: dict[str, Any]) -> None:
    assert qrs.check_present(document) == f"no {qrs.SETTING}: api, web"


def test_an_environment_that_is_not_a_map_counts_as_missing() -> None:
    assert qrs.setting_of({"environment": ["A=1"]}) is None


def test_main_reads_stdin_and_exits_1_on_a_failure(
    monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    monkeypatch.setattr("sys.stdin", io.StringIO(json.dumps(config("true", "false"))))

    assert qrs.main(["--config", "-", "--expect", "false"]) == 1
    out = capsys.readouterr().out
    assert "FAIL api and web have it false: expected false, found api=true" in out


def test_main_reads_a_file_and_exits_0(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    path = tmp_path / "config.json"
    path.write_text(json.dumps(config("true", "true")), encoding="utf-8")

    assert qrs.main(["--config", str(path), "--expect", "true"]) == 0
    assert capsys.readouterr().out.count("ok   ") == 3


def test_a_config_that_is_not_an_object_stops(tmp_path: Path) -> None:
    path = tmp_path / "config.json"
    path.write_text("[]", encoding="utf-8")

    with pytest.raises(SystemExit, match="not a JSON object"):
        qrs.main(["--config", str(path), "--expect", "true"])


def _resolved(tmp_path: Path, *files: str) -> dict[str, Any]:
    """Resolve the real compose files without a .env file and the shell's STEMSTUDIO_* values."""
    docker = shutil.which("docker")
    if docker is None:
        pytest.skip("docker is not installed")
    empty = tmp_path / "empty.env"
    empty.write_text("", encoding="utf-8")
    args = [docker, "compose", "--env-file", str(empty)]
    for file in files:
        args += ["-f", file]
    environment = {
        key: value for key, value in os.environ.items() if not key.startswith("STEMSTUDIO_")
    }
    result = subprocess.run(  # noqa: S603 - fixed argument list, no shell
        [*args, "config", "--format", "json"],
        cwd=REPO, capture_output=True, text=True, check=False, env=environment,
    )  # fmt: skip
    if result.returncode != 0:
        pytest.skip("docker compose config is not available here")
    document: dict[str, Any] = json.loads(result.stdout)
    return document


def test_the_real_base_file_has_the_report_off(tmp_path: Path) -> None:
    results = qrs.run_checks(_resolved(tmp_path, "docker-compose.yml"), "false")

    assert [problem for _, problem in results] == [None, None, None]


def test_the_real_e2e_override_switches_the_report_on(tmp_path: Path) -> None:
    files = ("docker-compose.yml", "docker-compose.e2e.yml")
    results = qrs.run_checks(_resolved(tmp_path, *files), "true")

    assert [problem for _, problem in results] == [None, None, None]
