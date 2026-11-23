"""Tests for scripts/loopback_ports_checks.py (refactor 004, ADR-027 D3, threat T7)."""

import io
import json
from pathlib import Path
from typing import Any

import pytest

import loopback_ports_checks as lpc

OPEN = "0.0.0.0"  # noqa: S104 - the opt-in value under test, never bound here


def port(published: str, target: int, host_ip: str | None = "127.0.0.1") -> dict[str, Any]:
    """One published port in the long syntax of `docker compose config --format json`."""
    entry: dict[str, Any] = {"mode": "ingress", "target": target, "published": published}
    if host_ip is not None:
        entry["host_ip"] = host_ip
    return entry


def config(api_ip: str | None = "127.0.0.1", web_ip: str | None = "127.0.0.1") -> dict[str, Any]:
    """A resolved config of the base file; both ports on the given host addresses."""
    return {
        "services": {
            "api": {"ports": [port("8081", 8081, api_ip)]},
            "web": {"ports": [port("8080", 8080, web_ip)]},
            "worker": {"expose": ["8000"]},
        }
    }


def test_default_file_passes_every_check() -> None:
    assert lpc.run_checks(config(), "127.0.0.1") == [
        ("api and web publish a port", None),
        ("api and web ports on 127.0.0.1", None),
        ("no other service publishes a port", None),
    ]


def test_opt_in_address_is_checked_against_the_expected_value() -> None:
    assert lpc.check_bind_address(config(OPEN, OPEN), OPEN) is None
    assert lpc.check_bind_address(config(OPEN, OPEN), "127.0.0.1") == (
        "expected 127.0.0.1, found api:8081 on 0.0.0.0; web:8080 on 0.0.0.0"
    )


def test_port_without_host_ip_binds_every_interface_and_fails() -> None:
    assert lpc.check_bind_address(config(None, "127.0.0.1"), "127.0.0.1") == (
        "expected 127.0.0.1, found api:8081 on <all interfaces>"
    )


def test_a_second_port_on_all_interfaces_fails() -> None:
    document = config()
    document["services"]["web"]["ports"].append(port("8443", 8443, None))

    assert lpc.check_bind_address(document, "127.0.0.1") == (
        "expected 127.0.0.1, found web:8443 on <all interfaces>"
    )


@pytest.mark.parametrize("missing", ["api", "web"])
def test_missing_or_silent_service_fails_so_the_check_is_never_vacuous(missing: str) -> None:
    removed = config()
    del removed["services"][missing]
    silent = config()
    silent["services"][missing]["ports"] = []

    assert lpc.check_publishes(removed) == f"no published port: {missing}"
    assert lpc.check_publishes(silent) == f"no published port: {missing}"


def test_internal_service_with_a_port_fails() -> None:
    document = config()
    document["services"]["worker"]["ports"] = [port("8000", 8000, None), "8001:8001"]

    assert lpc.check_internal_services(document) == (
        "internal services publish ports: worker:8000 on <all interfaces>"
    )


def test_document_without_services_reports_both_published_services() -> None:
    assert lpc.check_publishes({"services": []}) == "no published port: api, web"
    assert lpc.check_internal_services({}) is None


def test_report_prints_one_line_per_check(capsys: pytest.CaptureFixture[str]) -> None:
    status = lpc.report([("first", None), ("second", "broken")])

    assert status == 1
    assert capsys.readouterr().out.splitlines() == ["ok   first", "FAIL second: broken"]


def test_main_reads_a_file(tmp_path: Path) -> None:
    source = tmp_path / "config.json"
    source.write_text(json.dumps(config()), encoding="utf-8")

    assert lpc.main(["--config", str(source)]) == 0
    assert lpc.main(["--config", str(source), "--expect", OPEN]) == 1


def test_main_reads_stdin(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr("sys.stdin", io.StringIO(json.dumps(config(None, None))))

    assert lpc.main(["--config", "-"]) == 1


def test_non_object_document_has_no_services(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr("sys.stdin", io.StringIO("[]"))

    assert lpc.main(["--config", "-"]) == 1


REPO_ROOT = Path(__file__).resolve().parents[2]


def test_repository_compose_file_uses_the_bind_address_variable() -> None:
    text = (REPO_ROOT / "docker-compose.yml").read_text(encoding="utf-8")

    assert '"${STEMSTUDIO_BIND_ADDRESS:-127.0.0.1}:8081:8081"' in text
    assert '"${STEMSTUDIO_BIND_ADDRESS:-127.0.0.1}:8080:8080"' in text
