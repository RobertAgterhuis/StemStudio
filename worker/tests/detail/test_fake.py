"""Tests of the fake detail models used with ``STEMSTUDIO_SEPARATOR=fake`` (T042, D-12)."""

import json
from pathlib import Path

import pytest

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.detail import create_detail_adapters, create_detail_pipeline
from stemstudio_worker.detail.fake import CHUNKS, FAKE_SCALES, FakeModelProvider, FakeSource
from stemstudio_worker.detail.steps import DetailPipeline
from stemstudio_worker.devices.runner import CpuRunner
from stemstudio_worker.ports import DetailModelProvider
from stemstudio_worker.schemas import ParentOutcomeKind, StemName

from .models import write_stems
from .signals import noise

K = ParentOutcomeKind
SIX: tuple[StemName, ...] = ("vocals", "drums", "bass", "guitar", "piano", "other")


class Sleeper:
    def __init__(self) -> None:
        self.calls: list[float] = []

    def __call__(self, seconds: float) -> None:
        self.calls.append(seconds)


def _fake_stems(tmp_path: Path) -> dict[StemName, Path]:
    mix = noise(1.0, 0.4)
    return write_stems(tmp_path / "stems", dict.fromkeys(SIX, mix))


def test_the_fake_table_matches_the_e2e_expectations(tmp_path: Path) -> None:
    pipeline = DetailPipeline(FakeSource(0.0, Sleeper()))
    stems = _fake_stems(tmp_path)
    partial = tmp_path / "partial"
    for step in pipeline.plan(stems, partial):
        step.run(lambda f: None)
    outcomes = {o.parent: o for o in pipeline.outcomes(SIX, partial)}
    names = {p: [s.name for s in o.stems] for p, o in outcomes.items()}
    assert {p: o.outcome for p, o in outcomes.items()} == {
        "vocals": K.SPLIT, "drums": K.SPLIT, "bass": K.NOT_SUPPORTED,
        "guitar": K.NOTHING_FURTHER, "piano": K.NOT_RELIABLE, "other": K.SPLIT,
    }  # fmt: skip
    assert names["drums"] == ["kick", "snare", "toms", "hihat", "rest"]
    assert names["vocals"] == ["lead", "backing", "rest"]
    assert names["other"] == ["strings", "brass", "woodwinds", "rest"]
    assert {o.model for p, o in outcomes.items() if p != "bass"} == {"fake-detail"}


def test_fake_scales_follow_the_tasks_table() -> None:
    assert FAKE_SCALES["drums"] == {"kick": 0.5, "snare": 0.25, "toms": 0.15, "hihat": 0.1}
    assert FAKE_SCALES["vocals"] == {"lead": 0.7, "backing": 0.3}
    assert sum(FAKE_SCALES["piano"].values()) == pytest.approx(0.3)
    assert FAKE_SCALES["other"] == {"brass": 0.4, "strings": 0.3, "woodwinds": 0.3}


def test_fake_parents_run_one_by_one(tmp_path: Path) -> None:
    source = FakeSource(0.0, Sleeper())
    assert not source.sum_instruments
    steps = DetailPipeline(source).plan(_fake_stems(tmp_path), tmp_path / "partial")
    assert [s.parents for s in steps] == [
        ("drums",),
        ("vocals",),
        ("guitar",),
        ("piano",),
        ("other",),
    ]


def test_every_model_call_waits_the_step_delay_in_chunks(tmp_path: Path) -> None:
    sleeper = Sleeper()
    fractions: list[float] = []
    model = FakeSource(2.0, sleeper).choose("drums").load("cuda")
    outputs = model.separate(noise(0.1, 0.2), lambda f, s: fractions.append(f))
    assert sorted(outputs) == ["hihat", "kick", "snare", "toms"]
    assert sleeper.calls == [0.5] * CHUNKS
    assert fractions == [(i + 1) / CHUNKS for i in range(CHUNKS)]


def test_create_detail_adapters_in_fake_mode_needs_no_downloads(tmp_path: Path) -> None:
    settings = WorkerSettings(data_dir=tmp_path, separator="fake", model_dir=tmp_path / "models")
    adapters = create_detail_adapters(settings)
    assert isinstance(adapters.models, FakeModelProvider)
    assert isinstance(adapters.models, DetailModelProvider)
    drums = adapters.models.get("mdx23c-drumsep-6s").separate(noise(0.1, 0.2), lambda f, s: None)
    assert sorted(drums) == ["hihat", "kick", "snare", "toms"]
    vocals = adapters.models.get("uvr-mdxnet-kara-2").separate(noise(0.1, 0.2), lambda f, s: None)
    assert sorted(vocals) == ["backing", "lead"]
    other = adapters.models.get("fake-detail").separate(noise(0.1, 0.2), lambda f, s: None)
    assert sorted(other) == ["brass", "strings", "woodwinds"]
    assert not (tmp_path / "models").exists()


def test_create_detail_pipeline_in_fake_mode_uses_the_fake_source(tmp_path: Path) -> None:
    settings = WorkerSettings(data_dir=tmp_path, separator="fake", fake_detail_step_seconds=0.0)
    pipeline = create_detail_pipeline(settings, CpuRunner())
    stems = _fake_stems(tmp_path)
    partial = tmp_path / "partial"
    for step in pipeline.plan(stems, partial):
        step.run(lambda f: None)
    checkpoint = json.loads((partial / "drums" / "outcome.json").read_text(encoding="utf-8"))
    assert checkpoint["outcome"]["model"] == "fake-detail"
