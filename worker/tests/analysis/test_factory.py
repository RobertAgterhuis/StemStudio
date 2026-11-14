"""The composition-root factory builds the production beat analyzer (T141)."""

from pathlib import Path

from stemstudio_worker.analysis import LibrosaBeatAnalyzer, create_beat_analyzer
from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import BeatAnalyzer


def test_factory_returns_the_librosa_beat_analyzer(tmp_path: Path) -> None:
    analyzer: BeatAnalyzer = create_beat_analyzer(WorkerSettings(data_dir=tmp_path))

    assert isinstance(analyzer, LibrosaBeatAnalyzer)


def test_factory_ignores_the_separator_kind(tmp_path: Path) -> None:
    settings = WorkerSettings(data_dir=tmp_path, separator="fake")

    assert isinstance(create_beat_analyzer(settings), LibrosaBeatAnalyzer)
