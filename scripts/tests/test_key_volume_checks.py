"""Tests for scripts/key_volume_checks.py (011 follow-up claude-key, T019; CK-FR-003, CK-FR-005)."""

import copy
import io
import json
from pathlib import Path, PurePosixPath, PureWindowsPath
from typing import Any

import pytest

import key_volume_checks as kvc

REPO_ROOT = Path(__file__).resolve().parents[2]

GOOD_DOCKERFILE = """\
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN dotnet publish
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN mkdir -p /data \\
 && install -d -m 0700 -o "$APP_UID" -g "$APP_UID" /keys
USER $APP_UID
VOLUME ["/data", "/keys"]
"""

GOOD_COMPOSE_TEXT = """\
services:
  api:
    environment:
      # PowerShell: $env:ANTHROPIC_API_KEY = Read-Host -MaskInput
      - StemStudio__DataProtection__KeyRingDir=/keys
      - ANTHROPIC_API_KEY
"""


API_ENVIRONMENT = {
    "StemStudio__DataProtection__KeyRingDir": "/keys",
    "StemStudio__Agent__KeyEntry__InternalCaller": "web",
    "ANTHROPIC_API_KEY": None,
}
API_VOLUMES = [
    {"type": "volume", "source": "api-data", "target": "/data"},
    {"type": "volume", "source": "api-keys", "target": "/keys"},
]


def good_config() -> dict[str, Any]:
    """A resolved `docker compose config --format json` document that passes every check."""
    services = {
        "api": {"environment": dict(API_ENVIRONMENT), "volumes": copy.deepcopy(API_VOLUMES)},
        "web": {"volumes": [{"type": "volume", "source": "web-keys", "target": "/k"}]},
        "worker": {"volumes": [{"type": "volume", "source": "api-data", "target": "/library"}]},
        "generator": {},
    }
    volumes = {"api-data": {}, "api-keys": {"name": "stemstudio_api-keys"}, "web-keys": {}}
    return {"services": services, "volumes": volumes}


# R5 (quality-gate): negative tests assert the exact problem, which names the service,
# path or rule that is wrong.
MISSING_KEYS_DIR = 'runtime stage lacks: install -d -m 0700 -o "$APP_UID" -g "$APP_UID" /keys'
NOT_APP_USER = "runtime stage does not end as USER $APP_UID"


def test_good_inputs_pass_every_check() -> None:
    results = kvc.run_checks(good_config(), GOOD_DOCKERFILE, [GOOD_COMPOSE_TEXT])
    assert [problem for _, problem in results] == [None] * len(results)
    assert len(results) == 9


def test_volume_must_be_declared() -> None:
    config = good_config()
    del config["volumes"]["api-keys"]
    assert "api-keys" in (kvc.check_volume_declared(config) or "")


def test_volume_check_without_top_level_volumes() -> None:
    config = good_config()
    del config["volumes"]
    assert kvc.check_volume_declared(config) == "top-level volume 'api-keys' is not declared"


def test_api_must_mount_the_volume_at_keys() -> None:
    config = good_config()
    config["services"]["api"]["volumes"][1]["target"] = "/data/keys"
    assert "/keys" in (kvc.check_api_mount(config) or "")


def test_api_mount_must_be_a_named_volume() -> None:
    config = good_config()
    config["services"]["api"]["volumes"][1] = {
        "type": "bind",
        "source": "api-keys",
        "target": "/keys",
    }
    assert kvc.check_api_mount(config) == "api does not mount the named volume 'api-keys' at /keys"


def test_missing_api_service_fails_the_api_checks() -> None:
    config = good_config()
    del config["services"]["api"]
    assert kvc.check_api_mount(config) == "service 'api' is missing"
    assert kvc.check_key_ring_env(config) == "service 'api' is missing"


@pytest.mark.parametrize("service", ["worker", "web", "generator", "singer"])
def test_no_other_service_mounts_the_volume(service: str) -> None:
    config = good_config()
    config["services"][service] = {
        "volumes": [
            {"type": "volume", "source": "api-keys", "target": "/elsewhere", "read_only": True}
        ]
    }
    problem = kvc.check_only_api_mounts(config)
    assert problem == f"{service} mounts 'api-keys' at /elsewhere"


def test_no_other_service_mounts_anything_at_keys() -> None:
    config = good_config()
    config["services"]["web"]["volumes"].append(
        {"type": "volume", "source": "x", "target": "/keys"}
    )
    assert kvc.check_only_api_mounts(config) == "web mounts 'x' at /keys"


def test_api_must_not_mount_the_volume_twice() -> None:
    config = good_config()
    config["services"]["api"]["volumes"].append(
        {"type": "volume", "source": "api-keys", "target": "/k2"}
    )
    assert kvc.check_only_api_mounts(config) == "api mounts api-keys 2 times"


def test_short_syntax_strings_are_ignored_safely() -> None:
    config = good_config()
    config["services"]["web"]["volumes"].append("api-keys:/keys")
    assert kvc.check_only_api_mounts(config) == "web mounts 'api-keys' at /keys"


def test_key_ring_env_must_be_keys() -> None:
    config = good_config()
    config["services"]["api"]["environment"]["StemStudio__DataProtection__KeyRingDir"] = "/data/k"
    problem = kvc.check_key_ring_env(config)
    assert problem == "StemStudio__DataProtection__KeyRingDir is '/data/k', expected '/keys'"


def test_key_ring_env_missing() -> None:
    config = good_config()
    del config["services"]["api"]["environment"]["StemStudio__DataProtection__KeyRingDir"]
    assert kvc.check_key_ring_env(config) == (
        "StemStudio__DataProtection__KeyRingDir is None, expected '/keys'"
    )


def test_environment_as_list_is_understood() -> None:
    config = good_config()
    config["services"]["api"]["environment"] = [
        "StemStudio__DataProtection__KeyRingDir=/keys",
        "StemStudio__Agent__KeyEntry__InternalCaller=web",
        "ANTHROPIC_API_KEY",
    ]
    assert kvc.check_key_ring_env(config) is None
    assert kvc.check_internal_caller_env(config) is None


def test_internal_caller_must_be_web() -> None:
    config = good_config()
    config["services"]["api"]["environment"]["StemStudio__Agent__KeyEntry__InternalCaller"] = "api"
    assert kvc.check_internal_caller_env(config) == (
        "StemStudio__Agent__KeyEntry__InternalCaller is 'api', expected 'web'"
    )


def test_internal_caller_service_must_exist() -> None:
    config = good_config()
    del config["services"]["web"]
    assert kvc.check_internal_caller_env(config) == "service 'web' is missing"


def test_dockerfile_must_create_keys_0700_for_the_app_user() -> None:
    text = GOOD_DOCKERFILE.replace("-m 0700", "-m 0755")
    assert kvc.check_dockerfile_keys_dir(text) == MISSING_KEYS_DIR


def test_dockerfile_keys_dir_must_be_in_the_runtime_stage() -> None:
    build_only = (
        'FROM sdk AS build\nRUN install -d -m 0700 -o "$APP_UID" -g "$APP_UID" /keys\n'
        "FROM aspnet AS runtime\nUSER $APP_UID\n"
    )
    assert kvc.check_dockerfile_keys_dir(build_only) == MISSING_KEYS_DIR


def test_dockerfile_accepts_braced_app_uid() -> None:
    text = GOOD_DOCKERFILE.replace('"$APP_UID"', "${APP_UID}")
    assert kvc.check_dockerfile_keys_dir(text) is None


def test_dockerfile_must_declare_the_keys_volume() -> None:
    problem = kvc.check_dockerfile_volume(GOOD_DOCKERFILE.replace(', "/keys"', ""))
    assert problem == 'runtime stage lacks VOLUME ["/keys"]'


def test_dockerfile_must_run_as_the_app_user() -> None:
    problem = kvc.check_dockerfile_user(GOOD_DOCKERFILE.replace("USER $APP_UID\n", ""))
    assert problem == NOT_APP_USER


def test_dockerfile_user_root_after_app_user_fails() -> None:
    assert kvc.check_dockerfile_user(GOOD_DOCKERFILE + "USER root\n") == NOT_APP_USER


def test_claude_key_must_stay_a_bare_pass_through() -> None:
    assert kvc.check_claude_key_pass_through([GOOD_COMPOSE_TEXT]) is None
    missing = GOOD_COMPOSE_TEXT.replace("      - ANTHROPIC_API_KEY\n", "")
    assert kvc.check_claude_key_pass_through([missing]) == (
        "no compose file passes ANTHROPIC_API_KEY through as a bare entry"
    )


@pytest.mark.parametrize(
    "line",
    [
        "      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}",
        "      ANTHROPIC_API_KEY: ${ANTHROPIC_API_KEY:-}",
        "      - ANTHROPIC_API_KEY=dummy",
    ],
)
def test_claude_key_value_in_any_compose_file_fails(line: str) -> None:
    problem = kvc.check_claude_key_pass_through([GOOD_COMPOSE_TEXT, f"services:\n{line}\n"])
    assert problem == (
        "a compose file assigns ANTHROPIC_API_KEY a value (keep the bare pass-through entry)"
    )


def write_inputs(tmp_path: Path, config: dict[str, Any]) -> list[str]:
    dockerfile = tmp_path / "api.Dockerfile"
    dockerfile.write_text(GOOD_DOCKERFILE, encoding="utf-8")
    compose = tmp_path / "docker-compose.yml"
    compose.write_text(GOOD_COMPOSE_TEXT, encoding="utf-8")
    config_path = tmp_path / "config.json"
    config_path.write_text(json.dumps(config), encoding="utf-8")
    return [
        "--config",
        str(config_path),
        "--dockerfile",
        str(dockerfile),
        "--compose-file",
        str(compose),
    ]


def test_main_passes_and_prints_one_line_per_check(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    assert kvc.main(write_inputs(tmp_path, good_config())) == 0
    lines = capsys.readouterr().out.splitlines()
    assert len(lines) == 9
    assert all(line.startswith("ok   ") for line in lines)


def test_main_fails_and_never_prints_the_claude_key_value(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    config = copy.deepcopy(good_config())
    marker = "pass-through-value-" + "x" * 12
    config["services"]["api"]["environment"]["ANTHROPIC_API_KEY"] = marker
    config["services"]["api"]["environment"]["StemStudio__DataProtection__KeyRingDir"] = "/data"
    assert kvc.main(write_inputs(tmp_path, config)) == 1
    out = capsys.readouterr().out
    assert "FAIL api StemStudio__DataProtection__KeyRingDir" in out
    assert marker not in out


def test_main_reads_the_config_from_stdin(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    args = write_inputs(tmp_path, good_config())
    args[1] = "-"
    monkeypatch.setattr("sys.stdin", io.StringIO(json.dumps(good_config())))
    assert kvc.main(args) == 0
    assert "FAIL" not in capsys.readouterr().out


def test_main_rejects_a_config_that_is_not_an_object(tmp_path: Path) -> None:
    args = write_inputs(tmp_path, good_config())
    Path(args[1]).write_text("[]", encoding="utf-8")
    with pytest.raises(SystemExit):
        kvc.main(args)


def test_repository_files_pass_the_static_checks() -> None:
    dockerfile = (REPO_ROOT / "docker" / "api.Dockerfile").read_text(encoding="utf-8")
    compose = (REPO_ROOT / "docker-compose.yml").read_text(encoding="utf-8")
    assert kvc.check_dockerfile_keys_dir(dockerfile) is None
    assert kvc.check_dockerfile_volume(dockerfile) is None
    assert kvc.check_dockerfile_user(dockerfile) is None
    assert kvc.check_claude_key_pass_through([compose]) is None


# ---------------------------------------------------------------- non-Docker path (T050)


def test_default_key_ring_dir_on_windows_uses_local_app_data() -> None:
    environ = {"LOCALAPPDATA": r"C:\Users\owner\AppData\Local"}
    path = kvc.default_key_ring_dir("win32", environ, PureWindowsPath(r"C:\Users\owner"))
    assert path == PureWindowsPath(r"C:\Users\owner\AppData\Local\StemStudio\keys")


def test_default_key_ring_dir_on_windows_without_local_app_data() -> None:
    path = kvc.default_key_ring_dir("win32", {}, PureWindowsPath(r"C:\Users\owner"))
    assert path == PureWindowsPath(r"C:\Users\owner\AppData\Local\StemStudio\keys")


def test_default_key_ring_dir_on_linux_uses_xdg_data_home() -> None:
    environ = {"XDG_DATA_HOME": "/home/owner/.data"}
    path = kvc.default_key_ring_dir("linux", environ, PurePosixPath("/home/owner"))
    assert path == PurePosixPath("/home/owner/.data/stemstudio/keys")


@pytest.mark.parametrize("xdg", [None, "", "relative/dir"])
def test_default_key_ring_dir_on_linux_falls_back_to_local_share(xdg: str | None) -> None:
    environ = {} if xdg is None else {"XDG_DATA_HOME": xdg}
    path = kvc.default_key_ring_dir("darwin", environ, PurePosixPath("/home/owner"))
    assert path == PurePosixPath("/home/owner/.local/share/stemstudio/keys")


def test_key_ring_outside_repository_and_library_passes() -> None:
    repo = PurePosixPath("/home/owner/src/StemStudio")
    keys = PurePosixPath("/home/owner/.local/share/stemstudio/keys")
    assert kvc.check_non_docker_key_ring(keys, repo) is None


@pytest.mark.parametrize(
    "keys",
    ["/home/owner/src/StemStudio", "/home/owner/src/StemStudio/data/api/keys", "/srv/lib/keys"],
)
def test_key_ring_inside_repository_or_library_fails(keys: str) -> None:
    repo = PurePosixPath("/home/owner/src/StemStudio")
    library = PurePosixPath("/srv/lib")
    problem = kvc.check_non_docker_key_ring(PurePosixPath(keys), repo, library)
    where = "library /srv/lib" if keys.startswith("/srv") else f"repository {repo}"
    assert problem == f"key ring {keys} is inside the {where}"


def test_main_checks_the_non_docker_default_against_the_repository(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    monkeypatch.setenv("LOCALAPPDATA", str(tmp_path / "repo" / "inside"))
    monkeypatch.setenv("XDG_DATA_HOME", str(tmp_path / "repo" / "inside"))
    monkeypatch.delenv("StemStudio__DataProtection__KeyRingDir", raising=False)
    args = [*write_inputs(tmp_path, good_config()), "--repo-root", str(tmp_path / "repo")]
    assert kvc.main(args) == 1
    assert "FAIL non-Docker key ring" in capsys.readouterr().out


def test_main_honours_a_key_ring_override(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch, capsys: pytest.CaptureFixture[str]
) -> None:
    monkeypatch.setenv("StemStudio__DataProtection__KeyRingDir", str(tmp_path / "outside"))
    args = [*write_inputs(tmp_path, good_config()), "--repo-root", str(tmp_path / "repo")]
    assert kvc.main(args) == 0
    assert "ok   non-Docker key ring" in capsys.readouterr().out
