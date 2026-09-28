# CLI benchmarks

[Measurements](2026-09-28/results.md) cover all 37 effects at 200×50, with
portable-v2 results, raw samples, source hashes and full oracle evidence.
Output parity also covers all 37 effects at 400×100; the six gate effects are
timed separately at that size.

The runner in `../compare_cli.py` verifies compiled output before timing,
rotates execution order, records samples and medians, and chooses one C# CPU
tier for all effects by geometric mean. Python retains its native RNG and is
contextual; `../python_clock_cli.py` virtualizes only the clock for matrix and
thunderstorm. Use a new output directory whenever binaries change.
