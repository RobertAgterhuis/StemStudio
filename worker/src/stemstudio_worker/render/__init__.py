"""Render jobs of feature 007: path guard, block-wise mixing, clip protection and encoders.

Factories for the composition root (``main.create_app``, stem-separation): build one
``LibraryReader`` with ``create_library_reader`` (its ``status()`` feeds ``Health.library``) and
pass it to ``create_render_executor`` so both share the same library root.
"""

from stemstudio_worker.config import WorkerSettings
from stemstudio_worker.ports import LibraryReader, RenderExecutor
from stemstudio_worker.render.executor import BlockRenderExecutor
from stemstudio_worker.render.paths import MountedLibrary

__all__ = ["create_library_reader", "create_render_executor"]


def create_library_reader(settings: WorkerSettings) -> LibraryReader:
    """Build the path guard over ``settings.render.library_dir`` (read-only, may be unset)."""
    return MountedLibrary(settings.render.library_dir)


def create_render_executor(
    settings: WorkerSettings, library: LibraryReader | None = None
) -> RenderExecutor:
    """Build the production render executor.

    Args:
        settings: Worker settings; ``render.block_seconds`` sets the block length.
        library: The library reader to use; None builds one from ``settings``.

    Returns:
        A ``BlockRenderExecutor``.
    """
    reader = library if library is not None else create_library_reader(settings)
    return BlockRenderExecutor(reader, settings.render.block_seconds)
