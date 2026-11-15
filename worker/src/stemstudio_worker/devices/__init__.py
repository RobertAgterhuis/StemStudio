"""The GPU device policy of the worker (stem-separation agent, feature 003, ADR-012).

Only the composition root (``main.py``) imports this package; ``separation``, ``detail``,
``jobs`` and ``api`` receive the ``ports.DeviceRunner`` it builds.
"""
