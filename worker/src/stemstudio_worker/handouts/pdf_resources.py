"""The resources a merged notation page may carry: drawing keys only (020 security fixes).

pypdf's merge copies the page's ``/Resources`` and everything they reference. A hostile notation
PDF can hang any object from them (a JavaScript action, an embedded file, a reference XObject to
another file); inert in the handout, but more than page content. :func:`clean_resources` keeps
only the resource categories a content stream can use and, in every dictionary reachable from
them, only the keys of ``DRAWING_KEYS`` (fonts, images, forms, colour spaces, patterns,
shadings, graphics states, functions, optional content). The names inside a category (``/F1``,
``/X1``) and a Type 3 font's ``/CharProcs`` are names the content stream uses, so they stay.
"""

from typing import Final

from pypdf import PageObject
from pypdf.generic import ArrayObject, DictionaryObject, IndirectObject, NameObject, PdfObject

RESOURCE_CATEGORIES: Final = frozenset({
    "/ExtGState", "/ColorSpace", "/Pattern", "/Shading", "/XObject", "/Font", "/Properties",
    "/ProcSet",
})  # fmt: skip
"""The categories of a resource dictionary (ISO 32000-1, 7.8.3)."""

_COMMON = {"/Type", "/Subtype", "/S", "/Name", "/Length", "/Filter", "/DecodeParms"}
_FONT = {
    "/BaseFont", "/FirstChar", "/LastChar", "/Widths", "/FontDescriptor", "/Encoding",
    "/ToUnicode", "/DescendantFonts", "/CIDSystemInfo", "/Registry", "/Ordering", "/Supplement",
    "/DW", "/W", "/DW2", "/W2", "/CIDToGIDMap", "/FontBBox", "/FontMatrix", "/CharProcs",
    "/Resources", "/BaseEncoding", "/Differences",
    "/FontName", "/FontFamily", "/FontStretch", "/FontWeight", "/Flags", "/ItalicAngle",
    "/Ascent", "/Descent", "/Leading", "/CapHeight", "/XHeight", "/StemV", "/StemH",
    "/AvgWidth", "/MaxWidth", "/MissingWidth", "/FontFile", "/FontFile2", "/FontFile3",
    "/CharSet", "/Style", "/Panose", "/Lang", "/FD", "/CIDSet", "/Length1", "/Length2", "/Length3",
}  # fmt: skip
_XOBJECT = {
    "/Width", "/Height", "/ColorSpace", "/BitsPerComponent", "/Intent", "/ImageMask", "/Mask",
    "/Decode", "/Interpolate", "/SMask", "/SMaskInData", "/Matte", "/FormType", "/BBox",
    "/Matrix", "/Group", "/OC", "/CS", "/I", "/K",
}  # fmt: skip
_GRAPHICS = {
    "/LW", "/LC", "/LJ", "/ML", "/D", "/RI", "/OP", "/op", "/OPM", "/Font", "/BG", "/BG2", "/UCR",
    "/UCR2", "/TR", "/TR2", "/HT", "/FL", "/SM", "/SA", "/BM", "/CA", "/ca", "/AIS", "/TK",
    "/UseBlackPtComp", "/G", "/BC",
    "/FunctionType", "/Domain", "/Range", "/Size", "/BitsPerSample", "/Order", "/Encode", "/C0",
    "/C1", "/N", "/Functions", "/Bounds",
    "/ShadingType", "/Background", "/AntiAlias", "/Coords", "/Extend", "/Function",
    "/BitsPerCoordinate", "/BitsPerFlag", "/VerticesPerRow",
    "/PatternType", "/PaintType", "/TilingType", "/XStep", "/YStep", "/Shading", "/ExtGState",
    "/Alternate", "/WhitePoint", "/BlackPoint", "/Gamma",
    "/HalftoneType", "/Frequency", "/Angle", "/SpotFunction", "/AccurateScreens",
    "/TransferFunction", "/Width2", "/Height2", "/Xsquare", "/Ysquare",
    "/Predictor", "/Colors", "/Columns", "/EarlyChange", "/EndOfLine", "/EncodedByteAlign",
    "/Rows", "/EndOfBlock", "/BlackIs1", "/DamagedRowsBeforeError", "/JBIG2Globals",
    "/ColorTransform",
    "/OCGs", "/P", "/VE", "/Usage",
}  # fmt: skip
DRAWING_KEYS: Final = frozenset(_COMMON | _FONT | _XOBJECT | _GRAPHICS)
"""Keys kept in a dictionary below the resources. Not among them: actions (``/A``, ``/AA``,
``/JS``), files (``/F``, ``/EF``, ``/Ref``), ``/Metadata``, ``/PieceInfo``, structure keys."""

MAX_RESOURCE_OBJECTS: Final = 50_000
"""Most dictionaries and arrays below one page's resources."""

_NAME_MAPS: Final = (RESOURCE_CATEGORIES - {"/ProcSet"}) | {"/CharProcs"}


class ResourceLimitError(ValueError):
    """A page's resources reference more than ``MAX_RESOURCE_OBJECTS`` objects."""


def clean_resources(page: PageObject) -> None:
    """Strip ``page``'s resources (in place) to what its content can draw.

    Raises:
        ResourceLimitError: More than ``MAX_RESOURCE_OBJECTS`` objects below the resources.
    """
    if "/Resources" in page:
        _Cleaner().resources(page["/Resources"])


class _Cleaner:
    """One walk over a resource graph: each object once, at most ``MAX_RESOURCE_OBJECTS``."""

    def __init__(self) -> None:
        self._seen: set[int] = set()

    def resources(self, value: PdfObject) -> None:
        """A resource dictionary: its categories, each a map of names to resources."""
        resources = self._enter(value)
        if isinstance(resources, DictionaryObject):
            _keep(resources, RESOURCE_CATEGORIES)
            for key, item in resources.items():
                self._child(key, item, in_resources=True)

    def _names(self, value: PdfObject) -> None:
        names = self._enter(value)
        if isinstance(names, DictionaryObject):
            for item in names.values():
                self._value(item)

    def _value(self, value: PdfObject) -> None:
        target = self._enter(value)
        if isinstance(target, ArrayObject):
            for item in target:
                self._value(item)
        elif isinstance(target, DictionaryObject):
            _keep(target, DRAWING_KEYS)
            for key, item in target.items():
                self._child(key, item, in_resources=False)

    def _child(self, key: str, item: PdfObject, *, in_resources: bool) -> None:
        if key == "/Resources" and not in_resources:
            self.resources(item)
        elif key in _NAME_MAPS and (in_resources or key == "/CharProcs"):
            self._names(item)
        else:
            self._value(item)

    def _enter(self, value: PdfObject) -> PdfObject | None:
        """The object behind ``value``; None for one already seen or not a container."""
        target = value.get_object() if isinstance(value, IndirectObject) else value
        if not isinstance(target, ArrayObject | DictionaryObject) or id(target) in self._seen:
            return None
        self._seen.add(id(target))
        if len(self._seen) > MAX_RESOURCE_OBJECTS:
            msg = "The resources of a notation page reference too many objects"
            raise ResourceLimitError(msg)
        return target


def _keep(dictionary: DictionaryObject, allowed: frozenset[str]) -> None:
    for key in [key for key in dictionary if key not in allowed]:
        del dictionary[NameObject(key)]
