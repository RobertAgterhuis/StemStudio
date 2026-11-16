"""Data copies (feature 011): clones touching a listed data file are warnings with a reason.

The mechanism stays for future data files; the list is empty since the agent's imitation lexicon
copy (``src/StemStudio.Agent/Policy/ImitationLexicon.cs``) was replaced by the embedded 009
resource files (``StemStudio.Agent.Resources.EmbeddedLexicon``, T080). Every listed file carries a
mandatory reason; any other production clone stays an error.
"""

import pytest

import duplication
from common import Severity
from duplication import DATA_COPIES, Clone, evaluate

DATA_FILE = "src/StemStudio.Agent/Resources/Sample.cs"
REASON = "sample data file"


@pytest.fixture
def listed(monkeypatch: pytest.MonkeyPatch) -> str:
    """Lists ``DATA_FILE`` as a data copy for one test."""
    monkeypatch.setitem(duplication.DATA_COPIES, DATA_FILE, REASON)
    return DATA_FILE


def test_the_removed_lexicon_copy_is_no_longer_listed() -> None:
    assert "src/StemStudio.Agent/Policy/ImitationLexicon.cs" not in DATA_COPIES
    assert all(reason.strip() for reason in DATA_COPIES.values())


def test_clone_with_a_data_copy_is_a_warning_with_the_reason(listed: str) -> None:
    (finding,) = evaluate([Clone("src/StemStudio.Domain/Styles/X.cs", 1, listed, 5, 9)])
    assert finding.severity is Severity.WARNING
    assert finding.message == f"9 duplicated lines, also in {listed}:5 (data copy: {REASON})"


def test_data_copy_as_first_side_is_a_warning(listed: str) -> None:
    (finding,) = evaluate([Clone(listed, 1, listed, 40, 7)])
    assert finding.severity is Severity.WARNING
    assert "(data copy: " in finding.message


def test_other_production_clones_stay_errors() -> None:
    (finding,) = evaluate([Clone("src/StemStudio.Agent/Policy/Gate.cs", 1, "src/A/B.cs", 5, 7)])
    assert finding.severity is Severity.ERROR
    assert "data copy" not in finding.message
