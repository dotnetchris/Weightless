# Weightless

Weightless is a .NET research prototype for expressing a program's intent as a typed graph and testing which details of its implementation can change. A candidate can use fewer bytes, choose a different arithmetic expression, or reorder operations. Its contract determines what it must preserve for the caller, including the return value, exception details, object identity and writes that happened before a failure.

The long-term aim is to let a person specify behavior while leaving more implementation and execution choices to the system. Today Roslyn binds selected C# forms, Weightless stores and executes versioned programs, and independent C# references check their observations. The experiments also retain inputs that defeat a proposed equivalence. [Product review and evidence by experiment](docs/PRODUCT-SETTLEMENT-2026-11-24.md)

## What works today

The inventory experiment loads three previously saved programs. The first validates and adds two quantities. The second writes the total and an alert threshold into an existing stock row. C# passes the row's resulting quantity to the third program, which appends projected balances to the caller's history. With quantities `8 + 2`, threshold `3` and deltas `[3, 0, -2]`, the caller sees row `(10, 3)`, history `[90, 13, 13, 11]` and return value `11`. If the threshold is invalid, the caller still sees the first property write. A later projection overflow likewise preserves every earlier successful append. [Executable workflow and observations](experiments/0013-ProductSettlement/evidence-v1/result.json)

| Completed work | Result so far |
| --- | --- |
| Representation and contracts | Inferring the result type of Add pays for an enforced nonnegative-result predicate. Packed/constrained Add and baseline/plain Add both occupy **19 bytes**. A checked-Add run first saves space at two Adds, **22 → 18 bytes**. [Core measurements](experiments/0004-TypedContext/evidence-v1/report.md) · [Run encoding](experiments/0007-Capacity/README.md) |
| Alternative computations | Two intermediate overflows refuted an unconditional arithmetic rewrite. Lean proved the guarded rule in the pure value-or-overflow model. A serialized selector matched C# on **121** boundary pairs per codec. [Proof and witnesses](experiments/0005-GuardedAddition/README.md) · [Selector](experiments/0008-Transfer/README.md) |
| Observable effects | On 448 buffer cases, observing the faulting operation distinguished **120** additional schedule pairs. Legal reorderings on the grid fell from **406 to 286**. [Effect-order evidence](experiments/0009-EffectOrder/README.md) |
| Practical C# behavior | Two guard spellings produced one **39-byte** image. A **154-byte** property map preserved identity and partial updates. A **31-byte** traversal handled arrays from empty to 257 elements in the tested cases. [Three independent results](docs/PRACTICAL-LAB-HANDOFF.md#v1-results-and-replay) |

The core interpreter supports a small instruction set. Property access uses declared CLR getters and setters. The history route uses host iteration and list operations with a decoded checked Add at each step. General C# import, broader portability, and an execution-speed benefit remain open research questions. [Graph and wire specification](docs/GRAPH-FORMAT-V1.md)

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
