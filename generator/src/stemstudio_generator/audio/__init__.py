"""Audio helpers of the generator.

Stem summing, range cut, levels, labelled FLAC takes, the voice-conversion remix and the
voice-sample normalisation. Imports only ``ports``, ``schemas`` and ``config`` of the package
(dependency rule in ``ports.py``); no torch, so the helpers run in the fake image and in CI.
"""
