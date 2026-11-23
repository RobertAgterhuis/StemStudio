"""Tests of the handout allow-list and its WeasyPrint fetcher (020 T018, FR-018, research P0-4).

The allow-list tests run everywhere; the fetcher tests need WeasyPrint (Pango), so they run in
the Linux worker image and CI. Every hostile case must be refused without touching the network
or a file outside the allow-list.
"""

from pathlib import Path

import pytest

from stemstudio_worker.handouts.resources import (
    ARCHIVO_FONT,
    MAX_RESOURCE_BYTES,
    AllowList,
    RefusedResourceError,
)

from .support import jpeg, needs_weasyprint

TRACK = "7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f"
ASSET = "1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6"
ARTWORK = f"tracks/{TRACK}/artwork/auto/cover.jpg"
NOTATION = f"assets/{ASSET}/tab.pdf"


@pytest.fixture
def library(tmp_path: Path) -> Path:
    root = tmp_path / "library"
    jpeg(root / ARTWORK)
    (root / NOTATION).parent.mkdir(parents=True)
    (root / NOTATION).write_bytes(b"%PDF-1.7")
    (tmp_path / "secret.jpg").write_bytes(b"secret")
    return root.resolve()


@pytest.fixture
def files(library: Path) -> dict[str, Path]:
    return {ARTWORK: library / ARTWORK, NOTATION: library / NOTATION}


@pytest.fixture
def allowed(files: dict[str, Path]) -> AllowList:
    return AllowList.for_job(files)


def test_the_job_artwork_and_the_packaged_font_are_served(
    allowed: AllowList, files: dict[str, Path]
) -> None:
    body, content_type = allowed.read(allowed.uri(files[ARTWORK]))
    font, font_type = allowed.read(allowed.uri(ARCHIVO_FONT))

    assert (body, content_type) == (files[ARTWORK].read_bytes(), "image/jpeg")
    assert (font[:4], font_type) == (b"wOF2", "font/woff2")


def test_uris_are_file_uris_of_the_real_paths(allowed: AllowList, files: dict[str, Path]) -> None:
    assert allowed.uri(files[ARTWORK]) == files[ARTWORK].as_uri()


def test_a_path_that_is_not_allowed_has_no_uri(allowed: AllowList, tmp_path: Path) -> None:
    with pytest.raises(RefusedResourceError):
        allowed.uri(tmp_path / "secret.jpg")


def test_notation_pdfs_are_never_served_to_the_layout(
    allowed: AllowList, files: dict[str, Path]
) -> None:
    with pytest.raises(RefusedResourceError):
        allowed.read(files[NOTATION].as_uri())


@pytest.mark.parametrize(
    "url",
    [
        "http://example.com/x.png",
        "https://example.com/font.woff2",
        "ftp://example.com/x.jpg",
        "data:image/png;base64,iVBORw0KGgo=",
        "data:text/css,body{color:red}",
        "data:text/html,<script>alert(1)</script>",
        "javascript:alert(1)",
        "cover.jpg",
        "/etc/passwd",
        "file:///etc/passwd",
        "file://server/share/cover.jpg",
        "file://localhost/etc/passwd",
    ],
)
def test_remote_data_relative_and_foreign_urls_are_refused(allowed: AllowList, url: str) -> None:
    with pytest.raises(RefusedResourceError):
        allowed.read(url)


def test_traversal_out_of_an_allowed_folder_is_refused(
    allowed: AllowList, files: dict[str, Path], tmp_path: Path
) -> None:
    artwork = files[ARTWORK].as_uri()
    sneaky = [
        artwork.replace("/cover.jpg", "/../../../../../secret.jpg"),
        artwork.replace("/auto/cover.jpg", "/auto/../auto/cover.jpg"),
        artwork.replace("/cover.jpg", "/%2E%2E/%2E%2E/%2E%2E/%2E%2E/%2E%2E/secret.jpg"),
        (tmp_path / "secret.jpg").as_uri(),
    ]
    for url in sneaky:
        with pytest.raises(RefusedResourceError):
            allowed.read(url)


def test_query_and_fragment_are_refused(allowed: AllowList, files: dict[str, Path]) -> None:
    for suffix in ("?x=1", "#frag"):
        with pytest.raises(RefusedResourceError):
            allowed.read(files[ARTWORK].as_uri() + suffix)


def _symlink(link: Path, target: Path) -> None:
    try:
        link.symlink_to(target)
    except OSError:
        pytest.skip("symlinks are not available here")


def test_a_symlink_swapped_in_after_the_check_is_refused(
    allowed: AllowList, files: dict[str, Path], tmp_path: Path
) -> None:
    artwork = files[ARTWORK]
    artwork.unlink()
    _symlink(artwork, tmp_path / "secret.jpg")

    with pytest.raises(RefusedResourceError):
        allowed.read(artwork.as_uri())


def test_a_symlinked_job_file_is_never_allowed(library: Path, tmp_path: Path) -> None:
    link = library / f"tracks/{TRACK}/artwork/user/cover.jpg"
    link.parent.mkdir(parents=True)
    _symlink(link, tmp_path / "secret.jpg")

    allowed = AllowList.for_job({f"tracks/{TRACK}/artwork/user/cover.jpg": link})

    with pytest.raises(RefusedResourceError):
        allowed.read(link.as_uri())


def test_a_missing_or_oversized_file_is_refused(allowed: AllowList, files: dict[str, Path]) -> None:
    artwork = files[ARTWORK]
    artwork.write_bytes(b"\xff" * (MAX_RESOURCE_BYTES + 1))
    with pytest.raises(RefusedResourceError):
        allowed.read(artwork.as_uri())
    artwork.unlink()
    with pytest.raises(RefusedResourceError):
        allowed.read(artwork.as_uri())


def test_only_known_resource_types_can_be_allowed(tmp_path: Path) -> None:
    script = tmp_path / "evil.js"
    script.write_text("alert(1)")

    with pytest.raises(RefusedResourceError):
        AllowList.of([script.resolve()])


def test_job_files_other_than_artwork_and_notation_are_ignored(library: Path) -> None:
    stem = library / f"tracks/{TRACK}/stems/vocals.flac"
    stem.parent.mkdir(parents=True)
    stem.write_bytes(b"fLaC")

    allowed = AllowList.for_job({f"tracks/{TRACK}/stems/vocals.flac": stem})

    with pytest.raises(RefusedResourceError):
        allowed.read(stem.as_uri())


# ------------------------------------------------------------------ the WeasyPrint fetcher


@needs_weasyprint
def test_the_fetcher_is_a_weasyprint_url_fetcher_limited_to_file_urls(allowed: AllowList) -> None:
    from weasyprint.urls import URLFetcher  # noqa: PLC0415 - needs Pango

    from stemstudio_worker.handouts.fetcher import AllowListFetcher  # noqa: PLC0415

    fetcher = AllowListFetcher(allowed)

    assert isinstance(fetcher, URLFetcher)
    assert fetcher._allowed_protocols == ("file",)


@needs_weasyprint
def test_the_fetcher_serves_allowed_files_and_records_refusals(
    allowed: AllowList, files: dict[str, Path]
) -> None:
    from stemstudio_worker.handouts.fetcher import AllowListFetcher  # noqa: PLC0415

    fetcher = AllowListFetcher(allowed)
    response = fetcher.fetch(files[ARTWORK].as_uri())

    with pytest.raises(RefusedResourceError):
        fetcher.fetch("https://example.com/x.png")
    assert response.read() == files[ARTWORK].read_bytes()
    assert response.content_type == "image/jpeg"
    assert fetcher.fetched == [files[ARTWORK].as_uri()]
    assert fetcher.refused == ["https://example.com/x.png"]


@needs_weasyprint
def test_hostile_markup_and_css_fetch_nothing_outside_the_allow_list(
    allowed: AllowList, files: dict[str, Path], tmp_path: Path
) -> None:
    import weasyprint  # noqa: PLC0415 - needs Pango

    from stemstudio_worker.handouts.fetcher import AllowListFetcher  # noqa: PLC0415

    secret = (tmp_path / "secret.jpg").as_uri()
    artwork = files[ARTWORK].as_uri()
    html = f"""<html><head><style>
      @import url("https://example.com/remote.css");
      @import url("{secret}");
      @font-face {{ font-family: X; src: url("http://example.com/x.woff2"); }}
      body {{ background: url("data:image/png;base64,iVBORw0KGgo="); font-family: X; }}
      p {{ background-image: url("{artwork}/../../../../../../secret.jpg"); }}
    </style><link rel="stylesheet" href="file:///etc/passwd"></head>
    <body><p>x</p><img src="https://example.com/x.png"><img src="{artwork}">
    <iframe src="https://example.com/"></iframe><object data="{secret}"></object></body></html>"""
    fetcher = AllowListFetcher(allowed)

    weasyprint.HTML(string=html, base_url=tmp_path.as_uri() + "/", url_fetcher=fetcher).write_pdf()

    assert fetcher.fetched == [artwork]
    assert "https://example.com/remote.css" in fetcher.refused
    assert secret in fetcher.refused
    assert all(url == artwork for url in fetcher.fetched)
