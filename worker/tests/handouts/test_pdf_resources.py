"""Tests of the resource whitelist of merged notation pages (020 security fixes)."""

from pathlib import Path
from typing import cast

import pypdf
import pytest
from pypdf.generic import (
    ArrayObject,
    DecodedStreamObject,
    DictionaryObject,
    NameObject,
    NumberObject,
    PdfObject,
    TextStringObject,
)

from stemstudio_worker.handouts import pdf_resources
from stemstudio_worker.handouts.notation import PypdfNotationReader
from stemstudio_worker.handouts.pdf_resources import ResourceLimitError, clean_resources
from stemstudio_worker.ports import NotationUnreadableError

from .documents import A4


def _page(resources: PdfObject | None) -> pypdf.PageObject:
    page = pypdf.PdfWriter().add_blank_page(*A4)
    del page[NameObject("/Resources")]
    if resources is not None:
        page[NameObject("/Resources")] = resources
    return page


def _plain(value: object) -> dict[str, object]:
    return {str(key): item for key, item in cast(DictionaryObject, value).items()}


def _dict(**keys: PdfObject) -> DictionaryObject:
    return DictionaryObject({NameObject(f"/{key}"): value for key, value in keys.items()})


def test_unknown_categories_and_keys_go_and_names_stay() -> None:
    hook = _dict(S=NameObject("/JavaScript"), JS=TextStringObject("x"))
    font = _dict(Type=NameObject("/Font"), BaseFont=NameObject("/Helvetica"), Hook=hook)
    page = _page(_dict(Font=_dict(F1=font), Hook=hook, ProcSet=ArrayObject([NameObject("/PDF")])))

    clean_resources(page)

    resources = cast(DictionaryObject, page["/Resources"])
    assert sorted(resources) == ["/Font", "/ProcSet"]
    font_seen = _plain(cast(DictionaryObject, resources["/Font"])["/F1"])
    assert font_seen == {"/Type": "/Font", "/BaseFont": "/Helvetica"}


def test_a_type3_font_keeps_its_glyph_names_and_cleans_its_own_resources() -> None:
    glyph = DecodedStreamObject()
    glyph.set_data(b"0 0 m")
    glyph[NameObject("/Hook")] = NumberObject(1)
    inner = _dict(XObject=_dict(Im1=_dict(Width=NumberObject(1), Hook=NumberObject(2))))
    font = _dict(Subtype=NameObject("/Type3"), CharProcs=_dict(a=glyph), Resources=inner)
    page = _page(_dict(Font=_dict(T3=font)))

    clean_resources(page)

    assert sorted(cast(DictionaryObject, font["/CharProcs"])) == ["/a"]
    assert "/Hook" not in glyph
    image = cast(DictionaryObject, cast(DictionaryObject, inner["/XObject"])["/Im1"])
    assert _plain(image) == {"/Width": 1}


ODD = [None, ArrayObject([NumberObject(1)]), _dict(Font=NumberObject(3)),
       _dict(ColorSpace=_dict(CS0=NameObject("/DeviceRGB")))]  # fmt: skip


@pytest.mark.parametrize("resources", ODD)
def test_odd_resources_are_left_as_they_are(resources: PdfObject | None) -> None:
    page = _page(resources)

    clean_resources(page)

    assert page.get("/Resources") == resources


def test_a_shared_object_is_visited_once() -> None:
    shared = _dict(Type=NameObject("/ExtGState"), CA=NumberObject(1))
    page = _page(_dict(ExtGState=_dict(A=shared, B=shared)))

    clean_resources(page)

    assert _plain(shared) == {"/Type": "/ExtGState", "/CA": 1}


def test_too_many_objects_below_the_resources_are_refused(monkeypatch: pytest.MonkeyPatch) -> None:
    monkeypatch.setattr(pdf_resources, "MAX_RESOURCE_OBJECTS", 3)
    page = _page(_dict(ExtGState=_dict(A=_dict(), B=_dict(), C=_dict())))

    with pytest.raises(ResourceLimitError):
        clean_resources(page)


def test_a_notation_page_with_too_many_resource_objects_is_unreadable(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    writer = pypdf.PdfWriter()
    writer.add_blank_page(*A4)[NameObject("/Resources")] = _dict(ExtGState=_dict(A=_dict()))
    path = tmp_path / "heavy.pdf"
    with path.open("wb") as handle:
        writer.write(handle)
    monkeypatch.setattr(pdf_resources, "MAX_RESOURCE_OBJECTS", 1)

    with pytest.raises(NotationUnreadableError):
        PypdfNotationReader().page_sizes(path)
