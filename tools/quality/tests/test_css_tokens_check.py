from pathlib import Path

import pytest

import css_tokens_check
from common import Severity
from css_tokens_check import ColourHit, find_css_colours, find_razor_colours

WEB = "src/StemStudio.Web"


def _literals(hits: list[ColourHit]) -> list[str]:
    return [hit.literal for hit in hits]


@pytest.mark.parametrize(
    "value",
    [
        "#fff",
        "#FFFA",
        "#141A24",
        "#141a24cc",
        "rgb(8 11 16)",
        "rgba(0, 0, 0, 0.5)",
        "RGB(1,2,3)",
        "hsl(210 20% 10%)",
        "hsla(210, 20%, 10%, 0.4)",
        "hwb(0 0% 0%)",
        "oklch(0.7 0.1 200)",
        "lab(50% 0 0)",
        "red",
        "White",
        "rebeccapurple",
    ],
)
def test_css_rejects_every_raw_colour_form(value: str) -> None:
    hits = find_css_colours(f".a {{\n  color: {value};\n}}\n")
    assert len(hits) == 1
    assert hits[0].line == 2
    assert hits[0].literal == value


@pytest.mark.parametrize(
    "css",
    [
        ".a { color: var(--ss-color-base); }",
        ".a { background: transparent; border-color: currentColor; }",
        ".a { color: inherit; fill: initial; stroke: unset; outline-color: revert; }",
        "#main, .red, a:hover, .white-space { color: var(--ss-text); }",
        ".a { --ss-red-ish: var(--ss-stem-drums); }",
        ".a { color: var(--ss-red); }",
        "/* #fff red rgb(1 2 3) */ .a { color: var(--x); }",
        '.a::after { content: "white #fff"; font-family: "Archivo", sans-serif; }',
        ".a { background: url(#red-gradient); mask: url('data:image/svg+xml,%23fff'); }",
        "@media (prefers-color-scheme: dark) { .a { color: var(--x); } }",
        ".a { background: color-mix(in srgb, var(--ss-a) 40%, transparent); }",
        ".a { transition: color 120ms ease, background-color 120ms; }",
        ".a { animation: ss-split 200ms; grid-area: main; }",
        "",
    ],
)
def test_css_accepts_tokens_keywords_selectors_comments_strings_and_urls(css: str) -> None:
    assert find_css_colours(css) == []


def test_css_reports_a_custom_property_with_a_raw_value() -> None:
    hits = find_css_colours(":root {\n  --ss-x: #000;\n  --ss-y: var(--ss-x, red);\n}")
    assert [(hit.line, hit.literal) for hit in hits] == [(2, "#000"), (3, "red")]


def test_css_reports_every_colour_in_a_multi_line_declaration() -> None:
    css = ".a {\n  box-shadow:\n    0 0 1px #111,\n    0 1px 2px rgb(0 0 0 / 0.4);\n}"
    hits = find_css_colours(css)
    assert [(hit.line, hit.literal) for hit in hits] == [(3, "#111"), (4, "rgb(0 0 0 / 0.4)")]


def test_css_ignores_invalid_hex_lengths_and_hex_like_words() -> None:
    assert find_css_colours(".a { color: #12; grid-area: #abcde; }") == []


def test_css_keeps_line_numbers_after_multi_line_comments_and_strings() -> None:
    css = '/* one\ntwo */\n.a { content: "x\\\ny"; }\n.b { color: blue; }'
    assert [(hit.line, hit.literal) for hit in find_css_colours(css)] == [(5, "blue")]


def test_css_last_declaration_without_semicolon_is_checked() -> None:
    assert _literals(find_css_colours(".a { color: #abc }")) == ["#abc"]


def test_razor_style_attributes_are_checked_case_insensitively() -> None:
    razor = (
        "<div>\n"
        '  <div class="x" style="color: #fff; width: 50%">a</div>\n'
        "  <RadzenButton Style='background: red' />\n"
        '  <span data-style="color: red" style="padding: var(--ss-space-2)"></span>\n'
        "</div>\n"
    )
    hits = find_razor_colours(razor)
    assert [(hit.line, hit.literal) for hit in hits] == [(2, "#fff"), (3, "red")]


def test_razor_style_blocks_are_checked_as_css() -> None:
    razor = "<p>red</p>\n<style>\n  .a { color: blue; }\n</style>\n<p>#fff</p>\n"
    assert [(hit.line, hit.literal) for hit in find_razor_colours(razor)] == [(3, "blue")]


def test_razor_text_and_code_outside_style_are_ignored() -> None:
    razor = '<p class="red">white</p>\n@code { private string c = "#fff"; }\n'
    assert find_razor_colours(razor) == []


def _write(root: Path, rel: str, text: str) -> None:
    path = root / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def _tree(root: Path) -> None:
    _write(root, f"{WEB}/wwwroot/css/tokens.css", ":root { --ss-a: #141A24; }\n")
    _write(root, f"{WEB}/wwwroot/css/base.css", "body {\n  color: #fff;\n}\n")
    _write(root, f"{WEB}/wwwroot/css/components/mixer.css", ".m { color: red; }\n")
    _write(root, f"{WEB}/wwwroot/css/radzen-map.css", ":root { --rz-base: var(--ss-a); }\n")
    _write(root, f"{WEB}/wwwroot/app.css", ".legacy { color: #000; }\n")
    _write(root, f"{WEB}/wwwroot/lib/vendor.css", ".v { color: #000; }\n")
    _write(root, f"{WEB}/Components/Shell/Rail.razor", '<nav style="color: blue"></nav>\n')
    _write(root, f"{WEB}/Components/Shell/Rail.razor.css", "nav { color: green; }\n")
    _write(root, f"{WEB}/obj/Debug/x.razor", '<a style="color: red"></a>\n')
    _write(root, "tests/StemStudio.Web.Tests/T.razor", '<a style="color: red"></a>\n')


def test_run_flags_everything_outside_the_token_file(tmp_path: Path) -> None:
    _tree(tmp_path)
    found = {(f.path, f.line, f.severity) for f in css_tokens_check.run(tmp_path)}
    assert found == {
        (f"{WEB}/wwwroot/css/base.css", 2, Severity.ERROR),
        (f"{WEB}/wwwroot/css/components/mixer.css", 1, Severity.ERROR),
        (f"{WEB}/Components/Shell/Rail.razor", 1, Severity.ERROR),
        (f"{WEB}/Components/Shell/Rail.razor.css", 1, Severity.ERROR),
        (f"{WEB}/wwwroot/app.css", 1, Severity.WARNING),
    }


def test_findings_name_the_literal_the_check_and_the_owner(tmp_path: Path) -> None:
    _tree(tmp_path)
    finding = next(f for f in css_tokens_check.run(tmp_path) if f.path.endswith("base.css"))
    assert finding.check == "raw-colour"
    assert "#fff" in finding.message
    assert "tokens.css" in finding.message
    assert finding.owner == "frontend"
    assert finding.excess == 1


def test_run_without_web_project_finds_nothing(tmp_path: Path) -> None:
    assert css_tokens_check.run(tmp_path) == []


def test_main_fails_on_raw_colours(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    _tree(tmp_path)
    assert css_tokens_check.main(["--root", str(tmp_path)]) == 1
    out = capsys.readouterr().out
    assert "[raw-colour]" in out
    assert out.strip().endswith("css_tokens_check: 4 errors, 1 warnings")


def test_main_passes_on_a_clean_tree(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    _write(tmp_path, f"{WEB}/wwwroot/css/tokens.css", ":root { --ss-a: #141A24; }\n")
    assert css_tokens_check.main(["--root", str(tmp_path)]) == 0
    assert capsys.readouterr().out == "css_tokens_check: 0 errors, 0 warnings\n"
