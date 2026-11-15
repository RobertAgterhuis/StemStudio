"""Check that stored detail stems sum back to their parents (quickstart D3, SC-002).

Usage (from the repository root)::

    uv run --project worker python worker/scripts/check_reconstruction.py <trackDir>

``<trackDir>`` is a backend track directory (``<DataDir>/tracks/<id>``) or a worker cache entry
(``cache/by-audio/<sha>``). Exit code 0 = every split parent passes, 1 = a parent fails,
2 = usage error.
"""

import sys

from stemstudio_worker.detail.verify import main

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
