"""Engine adapters behind ``MusicEngine``, ``VoiceConverter`` and ``PitchTracker``.

``profiles`` maps the gate's ``GpuNeed`` to how an engine loads, lists the model files and the
take labels; ``fake`` holds the deterministic fakes of CI and E2E. The real adapters import
their model packages lazily (only the GPU images have them).
"""
