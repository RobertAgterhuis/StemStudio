from razor import find_code_blocks, to_csharp

COMPONENT = """<p>@count</p>
@code
{
    private int count;
}
<div>{ not code }</div>
@functions {
    private void A() { }
"""


def test_blocks_with_brace_on_next_line_and_unclosed_block() -> None:
    blocks = find_code_blocks(COMPONENT)
    assert [(b.start_line, b.end_line) for b in blocks] == [(2, 5), (7, 8)]
    assert blocks[0].lines == 2


def test_to_csharp_keeps_only_bodies_on_original_lines() -> None:
    lines = to_csharp(COMPONENT).split("\n")
    assert len(lines) == COMPONENT.count("\n") + 1
    assert lines[3] == "    private int count;"
    assert lines[0].strip() == ""
    assert lines[5].strip() == ""
    assert "not code" not in to_csharp(COMPONENT)


def test_markup_only_component() -> None:
    assert find_code_blocks("<h1>Hi</h1>\n") == []
    assert to_csharp("<h1>Hi</h1>\n").strip() == ""
