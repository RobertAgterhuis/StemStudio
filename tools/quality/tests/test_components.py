import pytest

from common import is_test
from components import COMPONENTS, Component, component_rows, render_components
from duplication import Clone
from sig_check import UnitMetrics


def _unit(path: str, nloc: int, ccn: int = 1, params: int = 0) -> UnitMetrics:
    return UnitMetrics(path, "f", 1, nloc, ccn, params, is_test(path))


API = Component("Api", ("src/StemStudio.Api/",))
E2E = Component("e2e", ("e2e/",), scope="all")
TESTS = Component("Test code", ("",), scope="tests")


def test_scope_selects_production_all_or_tests() -> None:
    assert API.contains("src/StemStudio.Api/A.cs", test=False)
    assert not API.contains("src/StemStudio.Api/A.cs", test=True)
    assert not API.contains("src/StemStudio.Web/A.cs", test=False)
    assert E2E.contains("e2e/tests/a.spec.ts", test=True)
    assert E2E.contains("e2e/pages/page.ts", test=False)
    assert TESTS.contains("tests/X.Tests/A.cs", test=True)
    assert not TESTS.contains("src/A.cs", test=False)


UNITS = [
    _unit("src/StemStudio.Api/A.cs", 10),
    _unit("src/StemStudio.Api/B.cs", 20, ccn=7, params=3),
    _unit("src/StemStudio.Api/C.cs", 70, ccn=11, params=5),
    _unit("tests/StemStudio.Api.Tests/T.cs", 50),
]


def test_rows_use_three_bands() -> None:
    (row,) = component_rows(UNITS, [], [API])
    assert (row.units, row.loc) == (3, 100)
    for profile in (row.size, row.complexity, row.params):
        assert (profile.low, profile.moderate, profile.high) == (10.0, 20.0, 70.0)
    assert row.stars == (1, 1, 1)


def test_rows_count_clones_touching_the_component() -> None:
    clones = [
        Clone("tests/StemStudio.Api.Tests/T.cs", 1, "src/StemStudio.Api/A.cs", 2, 6),
        Clone("tests/a/T.cs", 1, "tests/b/T.cs", 2, 6),
    ]
    (row,) = component_rows(UNITS, clones, [API])
    assert row.clones == 1


def test_empty_component_is_all_low_with_five_stars() -> None:
    (row,) = component_rows([], [], [API])
    assert (row.units, row.loc, row.clones, row.stars) == (0, 0, 0, (5, 5, 5))


def test_render_components_table() -> None:
    rows = component_rows([_unit("src/StemStudio.Api/A.cs", 10)], [], [API])
    text = render_components(rows)
    assert "## Components" in text
    low = "100.0 / 0.0 / 0.0"
    assert f"| Api | 1 | 10 | {low} | {low} | {low} | 5 / 5 / 5 | 0 |" in text
    assert "estimate" in text


REQUESTED = [
    "Domain",
    "Contracts",
    "Persistence",
    "Api",
    "Api/Agent/Secrets",
    "Web",
    "Agent",
    "Library (022)",
    "Lyric assist (023)",
    "worker/ingest",
    "worker/metadata",
    "worker/lyrics",
    "worker/engraving",
    "worker/render",
    "worker/separation",
    "worker/detail",
    "worker/devices",
    "worker/alignment",
    "worker/transcription",
    "worker/analysis",
    "worker/notation",
    "worker/jobs",
    "worker/api",
    "worker/storage",
    "worker/core",
    "generator/engines",
    "generator/devices",
    "generator/audio",
    "generator/jobs",
    "generator/api",
    "generator/core",
    "audio-engine",
    "audio-engine/stretch",
    "audio-engine/practice",
    "soundtouch shim",
    "web-interop",
    "e2e",
    "tools/quality",
    "tools/agent-eval",
    "scripts",
]


def test_default_components_cover_every_requested_area() -> None:
    names = [c.name for c in COMPONENTS]
    assert names == [*REQUESTED, "Test code (all)"]


def test_worker_detail_component_measures_detail_production_code() -> None:
    (detail,) = [c for c in COMPONENTS if c.name == "worker/detail"]
    assert detail.contains("worker/src/stemstudio_worker/detail/steps.py", test=False)
    assert not detail.contains("worker/src/stemstudio_worker/jobs/scheduler.py", test=False)


def test_worker_devices_component_measures_devices_production_code() -> None:
    (devices,) = [c for c in COMPONENTS if c.name == "worker/devices"]
    assert devices.contains("worker/src/stemstudio_worker/devices/runner.py", test=False)
    assert not devices.contains("worker/src/stemstudio_worker/detail/steps.py", test=False)
    assert not devices.contains("worker/tests/devices/test_runner.py", test=True)


def test_worker_metadata_component_measures_metadata_production_code() -> None:
    (metadata,) = [c for c in COMPONENTS if c.name == "worker/metadata"]
    assert metadata.contains("worker/src/stemstudio_worker/metadata/musicbrainz.py", test=False)
    assert not metadata.contains("worker/src/stemstudio_worker/ingest/fetch.py", test=False)
    assert not metadata.contains("worker/tests/metadata/test_musicbrainz.py", test=True)


def test_worker_render_component_measures_render_production_code() -> None:
    (render,) = [c for c in COMPONENTS if c.name == "worker/render"]
    assert render.contains("worker/src/stemstudio_worker/render/mixing.py", test=False)
    assert not render.contains("worker/src/stemstudio_worker/jobs/render_lane.py", test=False)
    assert not render.contains("worker/tests/render/test_mixing.py", test=True)


WORKER = "worker/src/stemstudio_worker"


@pytest.mark.parametrize(
    ("name", "inside", "outside"),
    [
        ("worker/lyrics", "lyrics/lrclib.py", "metadata/http.py"),
        ("worker/alignment", "alignment/adapter.py", "jobs/lyrics_lane.py"),
        ("worker/transcription", "transcription/piano.py", "jobs/transcription_store.py"),
        ("worker/notation", "notation/keys.py", "analysis/drum_onsets.py"),
        ("worker/engraving", "engraving/pdf.py", "notation/writer.py"),
    ],
)
def test_006_008_components_measure_their_production_code(
    name: str, inside: str, outside: str
) -> None:
    (component,) = [c for c in COMPONENTS if c.name == name]
    assert component.contains(f"{WORKER}/{inside}", test=False)
    assert not component.contains(f"{WORKER}/{outside}", test=False)
    assert not component.contains(f"worker/tests/{inside}", test=True)
