"""Data copies (feature 011): clones touching a listed data file are warnings with a reason.

``src/StemStudio.Agent/Policy/ImitationLexicon.cs`` is a C# copy of the 009 v1 imitation lexicon
(``StemStudio.Agent`` may not reference ``StemStudio.Domain``), so a clone with it is data, not
logic. Every listed file carries a mandatory reason; any other production clone stays an error.
"""

from common import Severity
from duplication import DATA_COPIES, Clone, evaluate

LEXICON = "src/StemStudio.Agent/Policy/ImitationLexicon.cs"


def test_lexicon_is_a_listed_data_copy_with_a_reason() -> None:
    assert "imitation lexicon" in DATA_COPIES[LEXICON]
    assert all(reason.strip() for reason in DATA_COPIES.values())


def test_clone_with_a_data_copy_is_a_warning_with_the_reason() -> None:
    (finding,) = evaluate([Clone("src/StemStudio.Domain/Styles/X.cs", 1, LEXICON, 5, 9)])
    assert finding.severity is Severity.WARNING
    assert finding.message == (
        f"9 duplicated lines, also in {LEXICON}:5 (data copy: {DATA_COPIES[LEXICON]})"
    )


def test_data_copy_as_first_side_is_a_warning() -> None:
    (finding,) = evaluate([Clone(LEXICON, 1, LEXICON, 40, 7)])
    assert finding.severity is Severity.WARNING
    assert "(data copy: " in finding.message


def test_other_production_clones_stay_errors() -> None:
    (finding,) = evaluate([Clone("src/StemStudio.Agent/Policy/Gate.cs", 1, "src/A/B.cs", 5, 7)])
    assert finding.severity is Severity.ERROR
    assert "data copy" not in finding.message
