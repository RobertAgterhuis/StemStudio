"""Features 014 and 015: dependency rules of the practice flow and play-along recording.

014 T015 (plan.md dependency direction):

* worker ``analysis/structure/**`` imports only ``ports``, ``schemas``, its own package and (third
  party) numpy, scipy, sklearn, librosa, soundfile; ``engraving/score_view.py`` only ``ports``,
  ``schemas``, ``engraving`` and verovio (the standard library, e.g. ``xml``, is always allowed);
  ``documents/**`` only ``ports``, ``schemas`` and reportlab; ``jobs`` and ``api`` import none of
  them (composition in ``main.py``);
* engine ``score-follow/**`` imports no Blazor interop module (``callbacks``, ``index``);
* .NET ``Domain/{Sections,Setlists,Practice}`` reference nothing from Api, Persistence or Web.

015 T007: ``Api/Assets`` and ``Api/Tracks`` never reference ``Api.Recording`` (only the
interfaces they own); engine ``recording/**`` is imported only by ``engine.ts`` and itself.
"""

from pathlib import Path

import pytest
from repo_tree import error_findings as _errors
from repo_tree import src_findings, write_file, write_module

from common import Severity
from dependency_check import (
    DOTNET_FOLDER_FORBIDDEN,
    PYTHON_ALLOWED,
    PYTHON_SCOPES,
    TS_MODULE_FORBIDDEN,
    TS_MODULE_IMPORTERS,
)

ENGINE = "src/audio-engine"

STRUCTURE_OK = (
    "import math\n"
    "from dataclasses import dataclass\n"
    "import numpy as np\n"
    "import librosa\n"
    "import soundfile as sf\n"
    "from scipy.ndimage import gaussian_filter\n"
    "from sklearn.cluster import AgglomerativeClustering\n"
    "from stemstudio_worker.ports import StructureError\n"
    "from stemstudio_worker.schemas import SectionResult\n"
    "from stemstudio_worker.analysis.structure.names import channel_name\n"
    "from .features import frames\n"
)
SCORE_VIEW_OK = (
    "import re\n"
    "import xml.etree.ElementTree as ET\n"
    "import verovio\n"
    "from stemstudio_worker.ports import ScoreView\n"
    "from stemstudio_worker.schemas import BarBox\n"
    "from stemstudio_worker.engraving.verovio_svg import VEROVIO_OPTIONS\n"
)
DOCUMENTS_OK = (
    "import io\n"
    "from reportlab.platypus import SimpleDocTemplate\n"
    "from stemstudio_worker.ports import DocumentError\n"
    "from stemstudio_worker.schemas import SetlistDocumentRequest\n"
    "from .fonts import register\n"
)
CLEAN_WORKER = {
    "analysis/structure/analyzer.py": STRUCTURE_OK,
    "analysis/structure/features.py": "import numpy as np\n",
    "engraving/score_view.py": SCORE_VIEW_OK,
    "documents/setlist_pdf.py": DOCUMENTS_OK,
    "documents/fonts.py": "from pathlib import Path\nfrom reportlab.pdfbase import pdfmetrics\n",
    "jobs/practice_service.py": "from stemstudio_worker.ports import StructureAnalyzer\n",
    "main.py": (
        "from stemstudio_worker.analysis.structure import StemsNoveltyAnalyzer\n"
        "from stemstudio_worker.engraving.score_view import VerovioScoreViewRenderer\n"
        "from stemstudio_worker.documents import ReportLabSetlistRenderer\n"
    ),
}


def _worker(root: Path, overrides: dict[str, str] | None = None) -> None:
    for rel, text in {**CLEAN_WORKER, **(overrides or {})}.items():
        write_module(root, rel, text)


# ------------------------------------------------------------------ worker


def test_the_scopes_are_mapped() -> None:
    assert set(PYTHON_SCOPES) == {"analysis/structure/", "engraving/score_view.py"}
    assert PYTHON_ALLOWED["documents"] == frozenset({"ports", "schemas"})


def test_a_clean_practice_worker_has_no_errors(tmp_path: Path) -> None:
    _worker(tmp_path)

    assert _errors(tmp_path) == []


@pytest.mark.parametrize(
    ("rel", "text", "message"),
    [
        ("analysis/structure/labels.py", "import torch\n",
         "forbidden third-party dependency analysis/structure/ -> torch"),
        ("analysis/structure/labels.py", "from stemstudio_worker.config import WorkerSettings\n",
         "forbidden dependency analysis/structure/ -> config"),
        ("analysis/structure/labels.py", "from stemstudio_worker.analysis.tempo import x\n",
         "forbidden dependency analysis/structure/ -> analysis.tempo"),
        ("analysis/structure/labels.py", "from ..tempo import x\n",
         "forbidden dependency analysis/structure/ -> analysis.tempo"),
        ("engraving/score_view.py", SCORE_VIEW_OK + "import cairosvg\n",
         "forbidden third-party dependency engraving/score_view.py -> cairosvg"),
        ("engraving/score_view.py", SCORE_VIEW_OK + "from lxml import etree\n",
         "forbidden third-party dependency engraving/score_view.py -> lxml"),
        ("documents/setlist_pdf.py", "import verovio\n",
         "forbidden third-party dependency documents -> verovio"),
        ("documents/setlist_pdf.py", "from stemstudio_worker.jobs import paths\n",
         "forbidden dependency documents -> jobs"),
    ],
)  # fmt: skip
def test_scope_breaches_fail(tmp_path: Path, rel: str, text: str, message: str) -> None:
    _worker(tmp_path, {rel: text})

    errors = _errors(tmp_path)

    # lxml also breaks the engraving-wide third-party rule: the scope rule adds its own error.
    assert {path for path, _ in errors} == {f"worker/src/stemstudio_worker/{rel}"}
    assert [m for _, m in errors if m.startswith(message)], errors
    assert len(errors) <= 2


@pytest.mark.parametrize("layer", ["jobs", "api"])
@pytest.mark.parametrize(
    "module",
    ["analysis.structure", "engraving.score_view", "documents.setlist_pdf"],
)
def test_jobs_and_api_never_import_the_practice_adapters(
    tmp_path: Path, layer: str, module: str
) -> None:
    _worker(tmp_path, {f"{layer}/practice_extra.py": f"from stemstudio_worker.{module} import x\n"})

    errors = _errors(tmp_path)

    assert len(errors) == 1, errors
    top = module.split(".", maxsplit=1)[0]
    assert errors[0][1].startswith(f"forbidden dependency {layer} -> {top}")


def test_vendor_and_other_modules_are_outside_the_scopes(tmp_path: Path) -> None:
    _worker(tmp_path, {"analysis/tempo.py": "import torch\nfrom ..config import WorkerSettings\n"})

    assert _errors(tmp_path) == []


# ------------------------------------------------------------------ engine

ENGINE_TREE = {
    "package.json": "{}",
    "src/engine.ts": (
        "import { Recording } from './recording/surface';\n"
        "import { ScoreFollower } from './score-follow/follower';\n"
        "import { DotNetNotifier } from './callbacks';\n"
    ),
    "src/index.ts": "export { createEngine } from './engine';\n",
    "src/callbacks.ts": "import type { RecordingState } from './types';\n",
    "src/recording/surface.ts": (
        "import { Recorder } from './recorder';\nimport type { X } from '../types';\n"
    ),
    "src/recording/recorder.ts": "import { FrameLoop } from '../frame-loop';\n",
    "src/score-follow/follower.ts": (
        "import { FrameSubscription } from '../frame-loop';\nimport type { Y } from '../types';\n"
    ),
    "src/frame-loop.ts": "export class FrameLoop {}\n",
    "src/types.ts": "export type X = number;\n",
}  # fmt: skip


def _engine(root: Path, overrides: dict[str, str] | None = None) -> None:
    for rel, text in {**ENGINE_TREE, **(overrides or {})}.items():
        write_file(root, f"{ENGINE}/{rel}", text)


def test_engine_rules_are_mapped() -> None:
    assert TS_MODULE_FORBIDDEN["audio-engine/score-follow"] == frozenset(
        {"audio-engine/callbacks", "audio-engine/index"}
    )
    assert dict(TS_MODULE_IMPORTERS) == {"audio-engine/recording": {"audio-engine/engine"}}


def test_a_clean_engine_has_no_errors(tmp_path: Path) -> None:
    _engine(tmp_path)

    assert _errors(tmp_path) == []


@pytest.mark.parametrize(
    ("rel", "text", "message"),
    [
        ("src/score-follow/dom.ts", "import { DotNetNotifier } from '../callbacks';\n",
         "forbidden dependency audio-engine/score-follow -> audio-engine/callbacks"),
        ("src/score-follow/dom.ts", "import type { Engine } from '../index';\n",
         "forbidden dependency audio-engine/score-follow -> audio-engine/index"),
        ("src/mixer.ts", "import { Recorder } from './recording/recorder';\n",
         "forbidden dependency audio-engine/mixer -> audio-engine/recording"
         " (imported only by audio-engine/engine)"),
        ("src/index.ts", "export { wavHeader } from './recording/wav';\n",
         "forbidden dependency audio-engine/index -> audio-engine/recording"
         " (imported only by audio-engine/engine)"),
        ("src/callbacks.ts", "import type { Take } from './recording/takes';\n",
         "forbidden dependency audio-engine/callbacks -> audio-engine/recording"
         " (imported only by audio-engine/engine)"),
    ],
)  # fmt: skip
def test_engine_breaches_fail(tmp_path: Path, rel: str, text: str, message: str) -> None:
    _engine(tmp_path, {rel: text})

    errors = _errors(tmp_path)

    assert errors == [(f"{ENGINE}/{rel}", message)]


# ------------------------------------------------------------------ .NET


@pytest.mark.parametrize("folder", ["Sections", "Setlists", "Practice"])
def test_practice_domain_folders_forbid_api_persistence_and_web(folder: str) -> None:
    assert {"StemStudio.Api", "StemStudio.Persistence", "StemStudio.Web"} <= (
        DOTNET_FOLDER_FORBIDDEN[f"StemStudio.Domain/{folder}"]
    )


@pytest.mark.parametrize("folder", ["Sections", "Setlists", "Practice"])
@pytest.mark.parametrize("project", ["Api", "Persistence"])
def test_practice_domain_breaches_fail(tmp_path: Path, folder: str, project: str) -> None:
    code = f"using StemStudio.{project}.Sections;\nnamespace StemStudio.Domain.{folder};\n"

    found = src_findings(tmp_path, f"StemStudio.Domain/{folder}/Thing.cs", code)

    assert [(s, m) for s, m, _, _ in found] == [
        (Severity.ERROR, f"forbidden dependency StemStudio.Domain/{folder} -> StemStudio.{project}")
    ]


def test_practice_domain_may_use_its_own_namespaces(tmp_path: Path) -> None:
    code = (
        "using StemStudio.Domain.Sections;\nusing System.Linq;\n"
        "namespace StemStudio.Domain.Practice;\n"
    )

    assert src_findings(tmp_path, "StemStudio.Domain/Practice/Thing.cs", code) == []


@pytest.mark.parametrize("folder", ["Assets", "Tracks"])
def test_assets_and_tracks_never_reference_recording(tmp_path: Path, folder: str) -> None:
    code = f"using StemStudio.Api.Recording;\nnamespace StemStudio.Api.{folder};\n"

    found = src_findings(tmp_path, f"StemStudio.Api/{folder}/Thing.cs", code)

    assert [(s, m) for s, m, _, _ in found] == [
        (
            Severity.ERROR,
            f"forbidden dependency StemStudio.Api/{folder} -> StemStudio.Api.Recording",
        )
    ]


@pytest.mark.parametrize("folder", ["Assets", "Tracks"])
def test_assets_and_tracks_may_use_their_own_interfaces(tmp_path: Path, folder: str) -> None:
    code = (
        f"using StemStudio.Api.{folder};\nusing StemStudio.Contracts.Recording;\n"
        f"namespace StemStudio.Api.{folder};\npublic interface ITrackTakesCleanup {{ }}\n"
    )

    assert src_findings(tmp_path, f"StemStudio.Api/{folder}/Thing.cs", code) == []
