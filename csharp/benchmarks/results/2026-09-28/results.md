# C# / Python / ttfx 0.3.3 (Rust) measurements

At 200×50, C# v2 reaches **1.31×** ttfx 0.3.3 (Rust) as a geometric mean over all 37 effects and is faster on **33/37**. The best C# tier is selected once by the overall result and used for every effect. Portable v2 is shown beside it.

Linux on a 16-core processor, with each implementation pinned to one core and one worker thread. Seed 1, complete animations, pacing disabled. Rust and C# use seven measured repetitions after two warmups. Python uses three measured repetitions from a separate batch on the same machine and input. The implementations were not all timed together.

Rust ttfx 0.3.3 is pinned to `54d21f0`; C# is .NET 11 NativeAOT; Python TerminalTextEffects is pinned to `7a91dd9`. No asm or fx is used. Python keeps its native RNG and ordering, so its frames may differ; its speed comparison is contextual. For matrix and thunderstorm, Python uses a clock-only adapter and Rust/C# use their virtual clock.

All **222** complete compiled-output checks (37 effects × 2 canvas sizes × 3 C# tiers) matched Rust 0.3.3 before timing. At 400×100, the Rust output hashes are reused from a verified prior run of the identical pinned Rust binary and identical input; fresh C# outputs are compared against them. Timings are whole-process elapsed milliseconds and each ratio is reference time divided by C# time. Values below 1× favor Rust.

## 200x50

All 37 effects. The two size results are separate.

| Effect | Python ms | ttfx 0.3.3 (Rust) ms | C# ms | vs Rust | vs Python | C# v2 ms | v2 vs Rust |
|---|---:|---:|---:|---:|---:|---:|---:|
| beams | 6,868.50 | 264.50 | 124.98 | 2.12× | 55.0× | 124.98 | 2.12× |
| binarypath | 19,198.53 | 816.06 | 760.31 | 1.07× | 25.3× | 760.31 | 1.07× |
| blackhole | 11,795.93 | 397.96 | 398.51 | 1.00× | 29.6× | 398.51 | 1.00× |
| bouncyballs | 9,290.29 | 380.45 | 319.14 | 1.19× | 29.1× | 319.14 | 1.19× |
| bubbles | 14,308.26 | 563.87 | 516.47 | 1.09× | 27.7× | 516.47 | 1.09× |
| burn | 12,803.73 | 393.85 | 225.92 | 1.74× | 56.7× | 225.92 | 1.74× |
| colorshift | 5,807.19 | 346.30 | 220.77 | 1.57× | 26.3× | 220.77 | 1.57× |
| crumble | 7,708.96 | 309.96 | 278.96 | 1.11× | 27.6× | 278.96 | 1.11× |
| decrypt | 9,669.64 | 488.31 | 312.65 | 1.56× | 30.9× | 312.65 | 1.56× |
| errorcorrect | 7,873.58 | 328.77 | 223.19 | 1.47× | 35.3× | 223.19 | 1.47× |
| expand | 3,513.25 | 113.86 | 92.57 | 1.23× | 38.0× | 92.57 | 1.23× |
| fireworks | 16,263.62 | 343.62 | 311.65 | 1.10× | 52.2× | 311.65 | 1.10× |
| highlight | 1,359.95 | 50.12 | 36.77 | 1.36× | 37.0× | 36.77 | 1.36× |
| laseretch | 18,438.05 | 617.48 | 599.62 | 1.03× | 30.7× | 599.62 | 1.03× |
| matrix | 24,580.00 | 190.10 | 175.61 | 1.08× | 140.0× | 175.61 | 1.08× |
| middleout | 2,505.38 | 92.43 | 67.05 | 1.38× | 37.4× | 67.05 | 1.38× |
| orbittingvolley | 2,098.19 | 74.42 | 87.05 | 0.85× | 24.1× | 87.05 | 0.85× |
| overflow | 2,046.52 | 115.17 | 90.84 | 1.27× | 22.5× | 90.84 | 1.27× |
| pour | 7,186.09 | 281.85 | 246.51 | 1.14× | 29.2× | 246.51 | 1.14× |
| print | 8,873.90 | 359.57 | 233.85 | 1.54× | 37.9× | 233.85 | 1.54× |
| rain | 5,458.89 | 223.43 | 207.28 | 1.08× | 26.3× | 207.28 | 1.08× |
| randomsequence | 1,124.78 | 53.54 | 33.82 | 1.58× | 33.3× | 33.82 | 1.58× |
| rings | 12,290.51 | 637.26 | 649.15 | 0.98× | 18.9× | 649.15 | 0.98× |
| scattered | 3,981.45 | 132.13 | 106.11 | 1.25× | 37.5× | 106.11 | 1.25× |
| slice | 2,731.42 | 65.03 | 55.08 | 1.18× | 49.6× | 55.08 | 1.18× |
| slide | 2,557.26 | 98.24 | 59.97 | 1.64× | 42.6× | 59.97 | 1.64× |
| smoke | 3,493.56 | 210.67 | 145.64 | 1.45× | 24.0× | 145.64 | 1.45× |
| spotlights | 10,727.02 | 300.78 | 229.24 | 1.31× | 46.8× | 229.24 | 1.31× |
| spray | 3,182.08 | 148.42 | 156.25 | 0.95× | 20.4× | 156.25 | 0.95× |
| swarm | 16,917.87 | 782.12 | 766.14 | 1.02× | 22.1× | 766.14 | 1.02× |
| sweep | 1,553.63 | 81.35 | 54.99 | 1.48× | 28.3× | 54.99 | 1.48× |
| synthgrid | 2,516.38 | 147.40 | 86.21 | 1.71× | 29.2× | 86.21 | 1.71× |
| thunderstorm | 4,991.54 | 316.38 | 194.39 | 1.63× | 25.7× | 194.39 | 1.63× |
| unstable | 4,878.91 | 188.04 | 149.29 | 1.26× | 32.7× | 149.29 | 1.26× |
| vhstape | 6,490.20 | 289.53 | 250.16 | 1.16× | 25.9× | 250.16 | 1.16× |
| waves | 9,303.08 | 562.12 | 286.26 | 1.96× | 32.5× | 286.26 | 1.96× |
| wipe | 1,246.57 | 51.57 | 25.85 | 2.00× | 48.2× | 25.85 | 2.00× |
| **Geometric mean (37 timed effects)** | | | | **1.31×** | | | **1.31×** |

## 400x100

All 37 effects checked for output; only the six gate effects timed. The two size results are separate.

| Effect | Python ms | ttfx 0.3.3 (Rust) ms | C# ms | vs Rust | vs Python | C# v2 ms | v2 vs Rust |
|---|---:|---:|---:|---:|---:|---:|---:|
| binarypath | — | 20,317.81 | 9,983.81 | 2.04× | — | 9,983.81 | 2.04× |
| burn | — | 4,083.68 | 2,581.99 | 1.58× | — | 2,581.99 | 1.58× |
| decrypt | — | 5,811.26 | 3,076.52 | 1.89× | — | 3,076.52 | 1.89× |
| rings | — | 2,789.03 | 2,955.51 | 0.94× | — | 2,955.51 | 0.94× |
| slide | — | 680.93 | 415.52 | 1.64× | — | 415.52 | 1.64× |
| spotlights | — | 1,824.40 | 1,132.39 | 1.61× | — | 1,132.39 | 1.61× |
| **Geometric mean (6 timed effects)** | | | | **1.57×** | | | **1.57×** |

## Validation and limits

Full oracle against pure Rust 0.4.0: **93,964 v2 + 93,964 v4 cases**, zero failures. Unit tests: 26/26. CLI comparisons: 1,642/1,642 per tier. Python parity: 354/354; static terminal: 154/154. Real-terminal checks covered output, pacing, SIGINT and SIGTERM.

The five-effect portable-v2 gate passes: at 200×50 its geometric mean is 1.42× and rings is 0.98×. At 400×100, the five-effect mean is 1.49× and rings is 0.94×. Each of the six effects, including binarypath, must be at least 0.90× at both sizes.

A prototype incremental grid preserved benchmark output but slowed all nine tested effects (for example, laseretch from 598 to 1,122 ms), so it was discarded. The measured build retains row rendering with batched Unix writes. The best C# tier may vary on another CPU. This active desktop is not a dedicated benchmark machine; small differences around 1× are uncertain.

[Individual timings, binary/output hashes and environment](samples.json) · [Source hashes](source-hashes.json) · [Validation summary](validation.json)

From `csharp/`, after publishing all tiers and preparing pinned references:

```sh
python3 benchmarks/compare_cli.py \
  --rust /path/to/ttfx-0.3.3 \
  --python-reference /path/to/terminaltexteffects \
  --cpu 8 --sizes 200x50 --repeats 7 --warmups 2 \
  --output benchmarks/results/local-run
```

Choose an available core. This command times Python as well as the compiled binaries and can take a long time.
