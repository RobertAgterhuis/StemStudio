"""Request bodies of ``createGeneration``: the JSON request (``params`` part or body) and files.

A restyle or voice conversion is ``multipart/form-data`` with a ``params`` part (JSON
``CreateGenerationRequest``, as a file or a plain field) and audio parts; a song (1.1.0) is an
``application/json`` body. Audio parts must be FLAC or WAV (``application/octet-stream`` is
accepted, the content decides) and at most ``UploadLimits.stem_bytes`` each.
"""

from dataclasses import dataclass
from pathlib import Path
from typing import Final

from pydantic import ValidationError
from starlette.datastructures import FormData, UploadFile
from starlette.requests import Request

from stemstudio_generator.api.problems import invalid_input, too_large, unsupported
from stemstudio_generator.config import MAX_SAMPLE_BYTES, MAX_STEM_BYTES
from stemstudio_generator.schemas import CREATE_GENERATION_REQUEST, CreateGenerationRequest

MISSING_PARAMS_TEXT: Final = "The params part is missing."
INVALID_JSON_TEXT: Final = "The params part is not valid JSON."
BODY_TYPE_TEXT: Final = "The body must be multipart/form-data or application/json."
AUDIO_TYPE_TEXT: Final = "Only FLAC and WAV stems are supported."

_AUDIO_TYPES: Final = {
    "audio/flac": ".flac", "audio/x-flac": ".flac", "audio/wav": ".wav", "audio/x-wav": ".wav",
    "audio/wave": ".wav", "application/octet-stream": "",
}  # fmt: skip
_SUFFIXES: Final = (".flac", ".wav")
_MAX_FORM_FILES: Final = 16
_MAX_FORM_FIELDS: Final = 4
_CHUNK: Final = 1 << 20
_MIB: Final = 1 << 20


@dataclass(frozen=True, slots=True)
class UploadLimits:
    """Size limits of uploaded parts in bytes.

    Attributes:
        stem_bytes: Largest ``stem``/``target``/``prompt``/``accompaniment`` part.
        sample_bytes: Largest voice-sample upload (1.1.0).
    """

    stem_bytes: int = MAX_STEM_BYTES
    sample_bytes: int = MAX_SAMPLE_BYTES


@dataclass(frozen=True, slots=True)
class GenerationUpload:
    """A parsed ``createGeneration`` request.

    Attributes:
        request: The validated JSON request.
        form: The multipart form (None for a JSON body); close it when done.
    """

    request: CreateGenerationRequest
    form: FormData | None


async def read_generation_upload(request: Request) -> GenerationUpload:
    """Parse the body by its content type (``400``/``415`` problems on errors)."""
    content_type = request.headers.get("content-type", "")
    if content_type.startswith("multipart/form-data"):
        form = await request.form(max_files=_MAX_FORM_FILES, max_fields=_MAX_FORM_FIELDS)
        try:
            return GenerationUpload(parse_request(await _params(form)), form)
        except BaseException:
            await form.close()
            raise
    if content_type.startswith("application/json"):
        return GenerationUpload(parse_request(await request.body()), None)
    raise unsupported(BODY_TYPE_TEXT)


def parse_request(raw: bytes | str) -> CreateGenerationRequest:
    """Validate the JSON request; the first error becomes the problem detail."""
    try:
        return CREATE_GENERATION_REQUEST.validate_json(raw)
    except ValidationError as error:
        raise invalid_input(validation_message(error)) from error


def validation_message(error: ValidationError) -> str:
    """Readable text of the first validation error (``loc: message``)."""
    first = error.errors()[0]
    if first["type"] == "json_invalid":
        return INVALID_JSON_TEXT
    message = str(first["msg"]).removeprefix("Value error, ")
    location = ".".join(str(part) for part in first["loc"])
    return f"{location}: {message}" if location else message


def audio_parts(form: FormData | None, name: str) -> list[UploadFile]:
    """The file parts called ``name`` (plain fields are ignored)."""
    if form is None:
        return []
    return [part for part in form.getlist(name) if isinstance(part, UploadFile)]


async def save_audio_parts(
    parts: list[UploadFile], dest_dir: Path, label: str, limit: int
) -> list[Path]:
    """Check type and size of each part and save it as ``<name>-<n><suffix>``.

    Raises:
        ProblemError: ``415`` for another type, ``413`` above ``limit``.
    """
    paths: list[Path] = []
    for number, part in enumerate(parts, start=1):
        suffix = audio_suffix(part)
        if part.size is not None and part.size > limit:
            raise too_large(f"{label.capitalize()} {number} is larger than {size_text(limit)}.")
        paths.append(await _save(part, dest_dir / f"{label}-{number}{suffix}"))
    return paths


def audio_suffix(part: UploadFile) -> str:
    """``.flac`` or ``.wav`` from the file name or the content type (``415`` otherwise)."""
    content_type = _media_type(part)
    if content_type not in _AUDIO_TYPES:
        raise unsupported(AUDIO_TYPE_TEXT)
    name_suffix = Path(part.filename or "").suffix.lower()
    if name_suffix in _SUFFIXES:
        return name_suffix
    return _AUDIO_TYPES[content_type] or ".wav"


def _media_type(part: UploadFile) -> str:
    """The part's media type without parameters, lower case (octet-stream when unset)."""
    raw = part.content_type or "application/octet-stream"
    return raw.split(";")[0].strip().lower()


def size_text(limit: int) -> str:
    """``300 MiB (314,572,800 bytes)``, or ``1,000 bytes`` for limits that are not whole MiB."""
    if limit % _MIB == 0:
        return f"{limit // _MIB} MiB ({limit:,} bytes)"
    return f"{limit:,} bytes"


async def _params(form: FormData) -> bytes | str:
    params = form.get("params")
    if params is None:
        raise invalid_input(MISSING_PARAMS_TEXT)
    if isinstance(params, UploadFile):
        return await params.read()
    return params


async def _save(part: UploadFile, path: Path) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    await part.seek(0)
    with path.open("wb") as sink:
        while chunk := await part.read(_CHUNK):
            sink.write(chunk)
    return path
