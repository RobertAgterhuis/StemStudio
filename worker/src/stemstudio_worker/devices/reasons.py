"""Fallback reason texts of single model steps (data-model.md 2.3); numbers have no separators."""


def not_enough_memory(label: str, need_mib: int, free_mib: int) -> str:
    """The step did not fit into the free GPU memory and ran on the CPU."""
    return (
        f"Not enough free GPU memory for {label} (needs {need_mib} MiB, {free_mib} MiB free); "
        "ran on the CPU."
    )


def out_of_memory(label: str) -> str:
    """The GPU attempt ran out of memory and the step was repeated on the CPU."""
    return f"The GPU ran out of memory during {label}; the step was repeated on the CPU."
