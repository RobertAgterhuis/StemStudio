"""Feature 020 (T009): quality rules of the stem quality reports and the lesson handouts.

Worker layering (``dependency_check``): ``insights`` imports only ``ports``, ``schemas`` and
(third party) numpy, scipy, soxr, soundfile; ``handouts`` only ``ports``, ``schemas`` and jinja2,
weasyprint, pypdf, fontTools (the missing-glyph check); neither imports the other, ``jobs``,
``api`` or ``separation``.

``handout_rules``: the handout style copy equals the app sources
(``worker/scripts/sync_handout_style.py --check``), no literal colours in
``handouts/styles/*.css`` (the ``css_tokens_check`` finder), and every ``url_fetcher`` the handout
code passes is an instance of the ``URLFetcher`` subclass of ``handouts/fetcher.py``; every
``HTML(...)``/``CSS(...)`` passes one (FR-018, research P0-4).
"""

import shutil
from pathlib import Path

import pytest
from repo_tree import WORKER_PACKAGE as PACKAGE
from repo_tree import error_findings, write_file, write_module

import handout_rules
import run_all
from common import Severity, owner_of
from dependency_check import PYTHON_ALLOWED, PYTHON_EXTERNAL_ALLOWED
from duplication import DuplicationResult

REPO_ROOT = Path(__file__).resolve().parents[3]
SCRIPT = "worker/scripts/sync_handout_style.py"
TOKENS = "src/StemStudio.Web/wwwroot/css/tokens.css"
FONTS = "src/StemStudio.Web/wwwroot/fonts/archivo"
ASSETS = f"{PACKAGE}/handouts/assets"
STYLES = f"{PACKAGE}/handouts/styles"
FETCHER = (
    "from weasyprint.urls import URLFetcher\n\n\n"
    "class LibraryFetcher(URLFetcher):\n    pass\n\n\n"
    "class StrictFetcher(LibraryFetcher):\n    pass\n"
)


# ------------------------------------------------------------------ worker layering


def test_insights_and_handouts_are_mapped() -> None:
    assert PYTHON_ALLOWED["insights"] == frozenset({"ports", "schemas"})
    assert PYTHON_ALLOWED["handouts"] == frozenset({"ports", "schemas"})
    assert PYTHON_EXTERNAL_ALLOWED["insights"] == frozenset({"numpy", "scipy", "soxr", "soundfile"})
    assert PYTHON_EXTERNAL_ALLOWED["handouts"] == frozenset(
        {"jinja2", "weasyprint", "pypdf", "fontTools"}
    )


def test_allowed_insights_and_handouts_imports_are_clean(tmp_path: Path) -> None:
    write_module(
        tmp_path,
        "insights/measures.py",
        "import math\nimport numpy as np\nimport scipy.signal\nimport soxr\nimport soundfile\n"
        "from stemstudio_worker.ports import QualityInputs\n"
        "from stemstudio_worker.schemas import StemQuality\n"
        "from . import levels\n",
    )
    write_module(tmp_path, "insights/levels.py", "from ..schemas import StemQuality\n")
    write_module(
        tmp_path,
        "handouts/renderer.py",
        "import jinja2\nfrom weasyprint import HTML\nfrom pypdf import PdfReader\n"
        "from fontTools.ttLib import TTFont\n"
        "from stemstudio_worker.ports import HandoutRenderer\n"
        "from stemstudio_worker.schemas import HandoutJobRequest\n"
        "from .fetcher import LibraryFetcher\n",
    )
    write_module(tmp_path, "handouts/fetcher.py", FETCHER)

    assert error_findings(tmp_path) == []


@pytest.mark.parametrize(
    ("rel", "text", "message"),
    [
        ("insights/a.py", "from ..handouts import renderer\n", "insights -> handouts"),
        ("insights/a.py", "from stemstudio_worker.jobs import queue\n", "insights -> jobs"),
        ("insights/a.py", "from ..api import routes\n", "insights -> api"),
        ("insights/a.py", "from ..separation import demucs\n", "insights -> separation"),
        ("insights/a.py", "from ..config import load\n", "insights -> config"),
        ("handouts/a.py", "from ..insights import measures\n", "handouts -> insights"),
        ("handouts/a.py", "from stemstudio_worker.jobs import queue\n", "handouts -> jobs"),
        ("handouts/a.py", "from ..api import routes\n", "handouts -> api"),
        ("handouts/a.py", "from ..separation import demucs\n", "handouts -> separation"),
        ("handouts/a.py", "from ..documents import setlist\n", "handouts -> documents"),
    ],
)
def test_forbidden_insights_and_handouts_edges(
    tmp_path: Path, rel: str, text: str, message: str
) -> None:
    write_module(tmp_path, rel, text)
    assert (f"{PACKAGE}/{rel}", f"forbidden dependency {message}") in error_findings(tmp_path)


@pytest.mark.parametrize(
    ("rel", "text", "target"),
    [
        ("insights/a.py", "import librosa\n", "insights -> librosa"),
        ("insights/a.py", "import torch\n", "insights -> torch"),
        ("insights/a.py", "from weasyprint import HTML\n", "insights -> weasyprint"),
        ("handouts/a.py", "import numpy\n", "handouts -> numpy"),
        ("handouts/a.py", "import httpx\n", "handouts -> httpx"),
        ("handouts/a.py", "import requests\n", "handouts -> requests"),
        ("handouts/a.py", "from reportlab.pdfgen import canvas\n", "handouts -> reportlab"),
    ],
)
def test_forbidden_insights_and_handouts_third_party_imports(
    tmp_path: Path, rel: str, text: str, target: str
) -> None:
    write_module(tmp_path, rel, text)
    message = f"forbidden third-party dependency {target}"
    assert error_findings(tmp_path) == [(f"{PACKAGE}/{rel}", message)]


@pytest.mark.parametrize(
    ("path", "owner"),
    [
        (f"{PACKAGE}/insights/measures.py", "beat-analysis"),
        ("worker/tests/insights/test_measures.py", "beat-analysis"),
        (f"{PACKAGE}/handouts/renderer.py", "audio-ingest"),
        ("worker/tests/handouts/test_renderer.py", "audio-ingest"),
        (SCRIPT, "architect"),
    ],
)
def test_020_owners(path: str, owner: str) -> None:
    assert owner_of(path) == owner


# ------------------------------------------------------------------ style drift


def _style_tree(root: Path) -> Path:
    """Copy the real sync script, the app tokens, the Archivo font and the handout assets."""
    for rel in (SCRIPT, TOKENS):
        write_file(root, rel, "")
        shutil.copyfile(REPO_ROOT / rel, root / rel)
    for rel in (FONTS, ASSETS):
        shutil.copytree(REPO_ROOT / rel, root / rel)
    return root


def _messages(root: Path) -> list[tuple[str, str, int, str]]:
    return [(f.check, f.path, f.line, f.message) for f in handout_rules.run(root)]


def test_the_repository_follows_the_handout_rules() -> None:
    assert _messages(REPO_ROOT) == []


def test_style_copy_in_sync_is_clean(tmp_path: Path) -> None:
    assert _messages(_style_tree(tmp_path)) == []


def test_a_tree_without_the_sync_script_is_not_checked_for_drift(tmp_path: Path) -> None:
    assert _messages(tmp_path) == []


def test_changed_app_tokens_are_style_drift(tmp_path: Path) -> None:
    root = _style_tree(tmp_path)
    tokens = root / TOKENS
    tokens.write_bytes(tokens.read_bytes().replace(b":root {\n", b":root {\n  --ss-new: 1px;\n", 1))

    ((check, path, line, message),) = _messages(root)

    assert (check, path, line) == ("handouts", SCRIPT, 0)
    assert "handout assets out of date: tokens.css" in message
    assert "manifest.json" in message
    assert "run uv run --project worker python worker/scripts/sync_handout_style.py" in message


def test_an_edited_copy_is_style_drift(tmp_path: Path) -> None:
    root = _style_tree(tmp_path)
    (root / ASSETS / "tokens.css").write_text("/* edited */\n", encoding="utf-8")

    ((check, _, _, message),) = _messages(root)

    assert check == "handouts"
    assert "tokens.css" in message


def test_a_broken_source_tree_is_reported_not_raised(tmp_path: Path) -> None:
    root = _style_tree(tmp_path)
    (root / TOKENS).write_text("body { margin: 0; }\n", encoding="utf-8")

    ((check, path, _, message),) = _messages(root)

    assert (check, path) == ("handouts", SCRIPT)
    assert "sync_handout_style.py --check failed" in message


# ------------------------------------------------------------------ literal colours


def test_handout_styles_use_tokens_only(tmp_path: Path) -> None:
    write_file(
        tmp_path,
        f"{STYLES}/handout.css",
        "@page { margin: 18mm; }\nh1 {\n  color: var(--ss-text);\n  border: 1px solid currentColor;\n}\n",
    )
    assert _messages(tmp_path) == []


@pytest.mark.parametrize(
    ("css", "literal", "line"),
    [
        ("h1 {\n  color: #1d2733;\n}\n", "#1d2733", 2),
        ("p { background: rgb(0 0 0 / 50%); }\n", "rgb(0 0 0 / 50%)", 1),
        ("footer {\n\n  border-top: 1px solid black;\n}\n", "black", 3),
        ("@page { @bottom-center { color: hsl(0 0% 40%); } }\n", "hsl(0 0% 40%)", 1),
    ],
)
def test_literal_colours_in_handout_styles_are_errors(
    tmp_path: Path, css: str, literal: str, line: int
) -> None:
    write_file(tmp_path, f"{STYLES}/print/handout.css", css)

    (finding,) = handout_rules.run(tmp_path)

    assert finding.check == "raw-colour"
    assert finding.severity is Severity.ERROR
    assert (finding.path, finding.line) == (f"{STYLES}/print/handout.css", line)
    assert f"`{literal}`" in finding.message
    assert "handout style sheet" in finding.message


def test_the_copied_tokens_file_may_hold_colours(tmp_path: Path) -> None:
    write_file(tmp_path, f"{ASSETS}/tokens.css", ":root {\n  --ss-text: #1d2733;\n}\n")
    assert _messages(tmp_path) == []


# ------------------------------------------------------------------ url_fetcher


def _handout(root: Path, code: str) -> list[tuple[str, int, str]]:
    write_module(root, "handouts/fetcher.py", FETCHER)
    write_module(root, "handouts/renderer.py", code)
    return [(f.path, f.line, f.message) for f in handout_rules.run(root) if f.check == "handouts"]


@pytest.mark.parametrize(
    "code",
    [
        "HTML(string=s, url_fetcher=LibraryFetcher(files))\n",
        "HTML(string=s, url_fetcher=fetcher.StrictFetcher(files)).write_pdf()\n",
        "f = LibraryFetcher(files)\nHTML(string=s, url_fetcher=f)\nCSS(string=c, url_fetcher=f)\n",
        "f: LibraryFetcher = make()\nweasyprint.HTML(string=s, url_fetcher=f)\n",
        "def render(f: URLFetcher) -> None:\n    HTML(string=s, url_fetcher=f)\n",
        "class R:\n"
        "    def __init__(self) -> None:\n"
        "        self._fetcher = LibraryFetcher(())\n"
        "    def run(self) -> None:\n"
        "        HTML(string=s, url_fetcher=self._fetcher)\n",
        "from jinja2 import Environment\nEnvironment(autoescape=True).get_template('x')\n",
    ],
)
def test_url_fetcher_instances_of_the_fetcher_subclass_are_clean(tmp_path: Path, code: str) -> None:
    assert _handout(tmp_path, code) == []


RENDERER = f"{PACKAGE}/handouts/renderer.py"
PLAIN = "is not an instance of the URLFetcher subclass of handouts/fetcher.py"


@pytest.mark.parametrize(
    ("code", "line", "message"),
    [
        (
            "def fetch(url):\n    return {}\n\nHTML(string=s, url_fetcher=fetch)\n",
            4,
            f"url_fetcher `fetch` {PLAIN}",
        ),
        (
            "HTML(string=s, url_fetcher=lambda url: {})\n",
            1,
            f"url_fetcher `lambda url: {{}}` {PLAIN}",
        ),
        (
            "HTML(string=s, url_fetcher=weasyprint.default_url_fetcher)\n",
            1,
            f"url_fetcher `weasyprint.default_url_fetcher` {PLAIN}",
        ),
        ("HTML(string=s, url_fetcher=URLFetcher())\n", 1, f"url_fetcher `URLFetcher()` {PLAIN}"),
        ("HTML(string=s)\n", 1, "HTML(...) without url_fetcher uses the default (network) fetcher"),
        (
            "x = 1\nweasyprint.CSS(string=c)\n",
            2,
            "CSS(...) without url_fetcher uses the default (network) fetcher",
        ),
        (
            "HTML(string=s, **options)\n",
            1,
            "HTML(...) without url_fetcher uses the default (network) fetcher",
        ),
    ],
)
def test_plain_or_missing_url_fetchers_are_errors(
    tmp_path: Path, code: str, line: int, message: str
) -> None:
    findings = _handout(tmp_path, code)
    assert findings == [(RENDERER, line, f"{message} (FR-018, research P0-4)")]


def test_fetcher_classes_outside_fetcher_py_do_not_count(tmp_path: Path) -> None:
    code = "class Mine(URLFetcher):\n    pass\n\nHTML(string=s, url_fetcher=Mine())\n"
    assert _handout(tmp_path, code) == [
        (RENDERER, 4, f"url_fetcher `Mine()` {PLAIN} (FR-018, research P0-4)")
    ]


def test_url_fetcher_rules_apply_only_to_the_handouts_package(tmp_path: Path) -> None:
    write_module(tmp_path, "documents/setlist.py", "HTML(string=s)\n")
    assert _messages(tmp_path) == []


# ------------------------------------------------------------------ run_all


def _dup() -> run_all.DuplicationRun:
    def fake(root: Path, paths: object) -> DuplicationResult:
        return DuplicationResult(duplicated_percentage=0.0)

    return fake


def test_run_all_reports_the_handout_rules(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    write_module(tmp_path, "handouts/renderer.py", "HTML(string=s)\n")
    report = tmp_path / "r.md"

    assert run_all.main(["--root", str(tmp_path), "--report", str(report)], _dup()) == 1

    assert "[handouts]" in capsys.readouterr().out
    text = report.read_text(encoding="utf-8")
    assert "## Insights and handouts\n\n**FAIL** - 1 errors" in text
    assert f"| error | handouts | `{RENDERER}:1` | audio-ingest |" in text


def test_clean_report_has_a_passing_insights_and_handouts_section(tmp_path: Path) -> None:
    report = tmp_path / "r.md"
    run_all.main(["--root", str(tmp_path), "--report", str(report)], _dup())
    text = report.read_text(encoding="utf-8")
    assert "## Insights and handouts\n\n**PASS** - handout style in sync" in text
