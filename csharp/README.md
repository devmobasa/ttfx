# C# ttfx

An independent C# port of all 37 ttfx effects, with a shared animation engine,
Rust-compatible deterministic math and random draws, and native Unix terminal I/O.
It builds as a .NET 11 NativeAOT executable. The Rust implementation remains in
this repository's root; C# has its own solution and build settings in this folder.

TerminalTextEffects by ChrisBuilds supplies the original effects and engine design.
The MIT attribution is preserved in [LICENSE](LICENSE) and the root [NOTICE](../NOTICE).

## Build and run

Linux x64 requires the SDK pinned by [global.json](global.json), plus the native
compiler/linker prerequisites for .NET NativeAOT (Clang and zlib development headers).
From this folder:

```sh
dotnet build Ttfx.slnx -c Release
./tools/publish x86-64-v2
printf 'Hello, World!\n' | ./artifacts/linux-x64-v2/ttfx decrypt
./artifacts/linux-x64-v2/ttfx --help
./artifacts/linux-x64-v2/ttfx rings --help
```

`./tools/publish` without arguments publishes v2, v3, and v4 sequentially. Run only
a tier supported by your CPU. The portable x64 build is v2. Each artifact includes
its source revision; verification tools reject stale or dirty builds unless you
explicitly pass `--allow-stale` for development.

## Tests and compatibility

```sh
dotnet test --project tests/Ttfx.Tests -c Release
./tools/verify oracle --effect wipe --reference /path/to/pure-rust-0.4.0/ttfx
./tools/verify cli --reference /path/to/pure-rust-0.4.0/ttfx
./tools/verify pty --effect decrypt --reference /path/to/pure-rust-0.4.0/ttfx
```

The verification wrapper uses the shared harnesses in the parent checkout.
Repeat the oracle command for each effect; `--mode full` selects the full corpus.
`static` and `python` suites also require the Python reference checkout at
`../reference/tte`. Source pins and measurement details are in the
[benchmark report](benchmarks/results/2026-09-28/results.md).

Current evidence includes 26 unit tests; 93,964 + 93,964 full oracle cases against pure Rust 0.4.0 across v2/v4; CLI and Python comparisons; static terminal cases; and pacing/signal checks. All 37 benchmark outputs match Rust 0.3.3 at both sizes in all three C# tiers.

## Performance

At 200×50, the single best C# build (v2) reaches a geometric mean of
**1.31× ttfx 0.3.3 (Rust)**, with 33 of 37 effects faster. All effects
use the same build. [The root README](../README.md#c-performance-comparison)
has the result summary; the [full report](benchmarks/results/2026-09-28/results.md)
includes portable v2 and the larger-size checks. The portable-v2 gate
passes.

The renderer reuses row buffers and cached visuals, grows character storage by
capacity, updates active characters in deterministic order, and batches Unix
writes with `writev`. Math and RNG behavior preserve the reference frame sequence.
