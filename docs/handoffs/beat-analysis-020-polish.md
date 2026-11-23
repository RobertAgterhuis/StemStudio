# beat-analysis 020: HO polish (T103)

| Task | Commit | Result |
|---|---|---|
| T103 | `49a6e160` | `worker/tests/insights/test_timing.py` (`@pytest.mark.integration`) |

**SC-001 measured** (2026-11-23, reference CPU Intel Core Ultra 9 285K, 24 logical CPUs, CPU only):

| Run | Stems | Song | Analysis time |
|---|---|---|---|
| BLAS default threads | 6 standard + 12 detail (drums 6, vocals 3, other 3) | 5 min synthetic, FLAC 24-bit stereo | **14.7 s** |
| 4 threads (`STEMSTUDIO_INSIGHTS_THREADS` default) | same | same | **12.8 s** |

Budget 30 s: met, so no refactor request. Run it with:
`uv run pytest tests/insights/test_timing.py -m integration -s`.
