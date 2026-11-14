"""Extraction of ``@code`` / ``@functions`` blocks from Razor components.

lizard does not parse Razor, so the C# inside the blocks is cut out (other lines blanked, line
numbers preserved) and measured as C#.
"""

import re
from dataclasses import dataclass

_BLOCK_START = re.compile(r"@(?:code|functions)\s*\{")


@dataclass(frozen=True, slots=True)
class CodeBlock:
    """One ``@code`` block.

    Attributes:
        start_line: 1-based line of the ``@code`` keyword.
        end_line: 1-based line of the closing brace.
        body_start: Character index just after the opening brace.
        body_end: Character index of the closing brace.
    """

    start_line: int
    end_line: int
    body_start: int
    body_end: int

    @property
    def lines(self) -> int:
        """Number of lines between the keyword line and the closing-brace line."""
        return max(self.end_line - self.start_line - 1, 0)


def find_code_blocks(text: str) -> list[CodeBlock]:
    """Return all ``@code``/``@functions`` blocks; an unclosed block runs to the end of file."""
    blocks: list[CodeBlock] = []
    position = 0
    while match := _BLOCK_START.search(text, position):
        close = _matching_brace(text, match.end() - 1)
        start, end = _line_of(text, match.start()), _line_of(text, close)
        blocks.append(CodeBlock(start, end, match.end(), close))
        position = close + 1
    return blocks


def to_csharp(text: str) -> str:
    """Return C# source holding only the code-block bodies; everything else becomes blanks.

    Newlines are kept, so line numbers in the result match the Razor file.
    """
    keep = [char if char == "\n" else " " for char in text]
    for block in find_code_blocks(text):
        keep[block.body_start : block.body_end] = text[block.body_start : block.body_end]
    return "".join(keep)


def _matching_brace(text: str, open_index: int) -> int:
    depth = 0
    for index in range(open_index, len(text)):
        depth += {"{": 1, "}": -1}.get(text[index], 0)
        if depth == 0:
            return index
    return len(text) - 1


def _line_of(text: str, index: int) -> int:
    return text.count("\n", 0, index) + 1
