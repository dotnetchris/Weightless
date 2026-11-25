# Weightless

Weightless is a .NET research prototype for expressing a program's intent as a typed graph and testing which details of its implementation can change. A candidate can use fewer bytes, choose a different arithmetic expression, or reorder operations. Its contract determines what it must preserve for the caller, including the return value, exception details, object identity and writes that happened before a failure.

The long-term aim is to let a person specify behavior while leaving more implementation and execution choices to the system. Today Roslyn binds selected C# forms, Weightless stores and executes versioned programs, and independent C# references check their observations. The experiments also retain inputs that defeat a proposed equivalence. [Research goals and the tenfold compaction milestone](GOALS.md) · [Product review and evidence by experiment](docs/PRODUCT-SETTLEMENT-2026-11-24.md)

## Measured reductions in encoded size

| Fixture | Encoding change | Complete image size | Reduction |
| --- | --- | ---: | ---: |
| 4,095 repeated checked Adds | Packed v1 → run encoding | 20,364 → 21 bytes | **99.9%** |
| 64 repeated checked Adds | Packed v1 → run encoding | 270 → 18 bytes | **93.3%** |
| Mixed graph with two eligible runs | Packed v1 → run encoding | 36 → 28 bytes | **22.2%** |
| 64 wrapping Adds | Baseline → packed v1 | 334 → 270 bytes | **19.2%** |
| 2 repeated checked Adds | Packed v1 → run encoding | 22 → 18 bytes | **18.2%** |
| Guarded arithmetic selector | Baseline → packed selector | 74 → 70 bytes | **5.4%** |
| Array store and load (either tested order) | Baseline → packed v1 | 46 → 45 bytes | **2.2%** |
| Fixed-buffer branch and helper call | Baseline → packed v1 | 62 → 61 bytes | **1.6%** |

The largest reductions came from runs of repeated operations. The run encoding expands those Adds again for execution. Applying it to the 61-byte buffer graph saved **zero further bytes** because the graph contains only one Add. The table measures complete program files, including their framing. [Core measurements](experiments/0004-TypedContext/evidence-v1/report.md) · [Run encoding](experiments/0007-Capacity/README.md#v3-experiment) · [Selector measurements](experiments/0008-Transfer/README.md#v3-experiment) · [Array schedule images](experiments/0009-EffectOrder/evidence-v3/)

## Property mapping and array-to-list traversal

**Property mapping took 154 bytes.** Of those, **140 bytes (90.9%)** identify the receiver types and members. The other **14 bytes** hold the header, assignment count, two four-byte assignments and the return receiver. Roslyn bound the C# assignments to real CLR getters and setters. The decoded map matched four independent calls and four successive updates across two rows. Starting at `(8, 4)`, writing `(17, -2)` left the caller's original row at `(17, 4)` when the second setter threw. Reversing the two stored assignments produced the same exception and left the row at `(8, 4)`. [Mapping images and caller-visible outcomes](experiments/0011-PropertyMapping/evidence-v1/result.json)

**One 31-byte program appended 257 running balances.** The same image handled an empty array and grew an existing two-entry list to **259 entries** on the long input. It spends **13 bytes** on traversal, append, return and framing, and **18 bytes** on the checked Add executed for each element. The v1 plan is hand-built. The host traverses the array and calls the CLR list methods. [History encoding and execution](experiments/0012-BalanceHistory/README.md#v1-results)

Changing one byte selected the input delta for append. With opening balance `10` and deltas `[3, 0, -2]`, both programs returned `11`, but the caller's history changed from `[90, 13, 13, 11]` to `[90, 3, 0, -2]`. A separate one-byte change delayed writes until the end. Starting at `int.MaxValue - 1` with deltas `[1, 1]`, both versions overflowed on the second addition. The original left `[90, int.MaxValue]` and the delayed version left `[90]`. [Saved histories and overflow counterexample](experiments/0012-BalanceHistory/evidence-v1/result.json)

Each practical lab measured one encoding of its operation. A compression percentage for either would require a second encoding of the same behavior. The sizes above cover the stored program. Objects, array elements and list entries are runtime data.

## What works today

The inventory experiment connects three decoded programs in C#. With quantities `8 + 2`, threshold `3` and deltas `[3, 0, -2]`, the guarded sum feeds the mapping and the mapped stock quantity feeds the history. Swapping the two getter IDs changes **two bytes** in the map. The row changes from `(10, 3)` to `(3, 10)` and the same history program returns **`4` instead of `11`**. [Executable workflow and observations](experiments/0013-ProductSettlement/evidence-v1/result.json)

| Completed work | Result so far |
| --- | --- |
| Representation and contracts | Inferring the result type of Add pays for an enforced nonnegative-result predicate. Packed/constrained Add and baseline/plain Add both occupy **19 bytes**. A checked-Add run first saves space at two Adds, **22 → 18 bytes**. [Core measurements](experiments/0004-TypedContext/evidence-v1/report.md) · [Run encoding](experiments/0007-Capacity/README.md) |
| Alternative computations | Two intermediate overflows refuted an unconditional arithmetic rewrite. Lean proved the guarded rule in the pure value-or-overflow model. A serialized selector matched C# on **121** boundary pairs per codec. [Proof and witnesses](experiments/0005-GuardedAddition/README.md) · [Selector](experiments/0008-Transfer/README.md) |
| Observable effects | On 448 buffer cases, observing the faulting operation distinguished **120** additional schedule pairs. Legal reorderings on the grid fell from **406 to 286**. [Effect-order evidence](experiments/0009-EffectOrder/README.md) |
| Input validation | Explicit C# conditionals and the .NET guard helper produced identical **39-byte** images. Both accepted `-1` and reported the same rejected argument when both inputs were below the bound. [Guard comparisons](experiments/0010-AddInputGuards/evidence-v1/result.json) |

The core interpreter supports a small instruction set. General C# import, broader portability, and an execution-speed benefit remain open research questions. [Graph and wire specification](docs/GRAPH-FORMAT-V1.md)

## Run it

From the repository root with the .NET 10 SDK, run the product settlement to exercise the checked-in program images and compare the inventory operation with C#. Its `result.json` is written under `artifacts/labs/0006-v1/product-settlement/v1/`.

```powershell
dotnet run -c Release --project experiments/0013-ProductSettlement/v1 -- artifacts/labs
```

To regenerate the original six-fixture corpus, decode it in a fresh process and collect Release timings, run the core suite.

```powershell
dotnet run -c Release --project src/Weightless -- suite artifacts/thanksgiving
```

The lab handoffs include component replay commands and the next three independent experiments. [Capacity, transfer and effect order](docs/LAB-HANDOFF.md#v3-results-and-replay) · [Guards, mapping and history](docs/PRACTICAL-LAB-HANDOFF.md#v1-results-and-replay) · [Independent v2 handoff](docs/PRACTICAL-LAB-HANDOFF.md#independent-v2-handoff) · [Early candidate and proof records](experiments/semantic-cube-v1.json)
