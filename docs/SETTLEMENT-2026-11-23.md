# What Weightless demonstrated on November 23

**Checked Add can carry an enforceable extra requirement in the same 19-byte budget.** The packed decoder recovers the Add result type from its two typed operands. The encoder spends the recovered byte on a mandatory nonnegative result. On `(-1, 0)`, the unrestricted graph returns `-1` and the constrained graph raises `PostconditionViolationException`. On `(int.MaxValue, 1)`, checked arithmetic raises `OverflowException` before either graph can return. The byte saving, the extra meaning and the order of enforcement are independently observable. [Wire specification](GRAPH-FORMAT-V1.md) · [Contract probes and observations](../experiments/0004-TypedContext/evidence-v1/verification.json)

## What reproduced

I replayed revision `f9a46e728410d55c018e0fdd41c68883685b0de0` on Windows x64 with SDK 10.0.401 and .NET 10.0.12. All 12 wire images of the six fixtures matched the checked-in files byte for byte. A fresh verifier reproduced the graphs, deterministic re-encodings, all 62 compiled C# comparisons and all 16 expected rejections. Roslyn supplied the checked and wrapping Add bindings and the Hello World call. The buffer and repeated-addition graphs were hand-built and compared with compiled C#. [Corpus and provenance](../experiments/0004-TypedContext/README.md)

| Standalone graph | Baseline bytes | Packed bytes | Recovered bytes |
| --- | ---: | ---: | ---: |
| Checked Add | 19 | 18 | 1 |
| Checked Add with mandatory nonnegative result | 20 | 19 | 1 |
| Wrapping Add | 19 | 18 | 1 |
| Hello World | 31 | 31 | 0 |
| Buffer and helper call | 62 | 61 | 1 |
| 64 wrapping additions | 334 | 270 | 64 |

V1 removes exactly one tag per Add and changes no other field width between codecs. The 64-Add chain recovered 19.2% of its baseline size. Hello World provides the zero-Add control. The promise concerns storage. Both encodings execute through the same interpreter, and the recorded timing ranges overlap. A runtime speedup and transfer of the original LittleBits primitive remain open questions. The repository has no original LittleBits test vector. [Raw timings and measurement conditions](../experiments/0004-TypedContext/evidence-v1/report.md)

The original unguarded checked transfer, `(a + k) + (b - k)`, still fails on both saved intermediate-overflow witnesses. Its guarded C# successor reproduced the saved 1,331-case grid and six named probes. The widened guard admitted 773 grid cases, including 110 in which the final sum correctly overflowed. Lean 4.8.0 rechecked the theorem for pure values observed as an integer or overflow, along with the range lemma for the widened intermediates. The theorem covers that arithmetic model. The correspondence to the running C# and graph implementations has experimental evidence. [Counterexamples](../experiments/0001-Addition/checked-transfer-v1.json) · [Grid and theorem](../experiments/0005-GuardedAddition/README.md) · [Versioned relationships](../experiments/semantic-cube-v1.json)

## What the native prototype adds

Experiment 0006 decodes the checked-in Add seed, swaps its two operand references to make a structurally distinct graph, then varies the postcondition and the codec independently. Two orders × two contracts × two codecs produced eight wire images. Each reconstructed its own graph and re-encoded identically. All 56 observations matched a compiled checked C# reference across seven pure inputs. Both operand orders achieved the 19-byte result. The run reads native graph bytes and uses the existing graph validator and interpreter. It requires no Roslyn extraction at execution time. Automatically discovering or selecting a universally interchangeable implementation remains an open step. [Prototype](../experiments/0006-Settlement/v1/Program.cs) · [Exact bytes, inputs and outcomes](../experiments/0006-Settlement/evidence-v1/result.json)

Three small experiments established starting controls for independent investigations.

| Experiment | Settled observation | Evidence |
| --- | --- | --- |
| 0007 Capacity | Across chains of zero, one and two Adds, packed/constrained cost one byte more than, exactly as much as, and one byte less than baseline/plain. The negative witness triggered the contract at every length. | [Four measured sizes per chain](../experiments/0007-Capacity/evidence-v1/result.json) |
| 0008 Transfer | A native graph with literal `+1` and `-1` reproduced both original checked-transfer refutations through both codecs. The counterexamples survived translation out of C#. | [Candidate bytes and cases](../experiments/0008-Transfer/evidence-v1/result.json) |
| 0009 Effect order | Moving a load across a store changed the return from 5 to 7 when the two arguments named the same cell. In a separate case both schedules raised the same bounds exception and left identical memory. Only the store-first graph recorded a write. | [Both schedules and full observations](../experiments/0009-EffectOrder/evidence-v1/result.json) |

The effects probe exposed a defect in the replay harness. Copying each argument separately split aliases, so a fixture intended to read and write the same array could silently test two different arrays. The copier now clones each distinct buffer once per execution. A control tests a shared array, distinct equal arrays, distinct empty arrays and isolation across runs. The original corpus also passed with the correction. [Input copier](../src/Weightless/Fixtures.cs) · [Isolation control](../experiments/0006-Settlement/v1/Program.cs)

## Replay

Run from the repository root with the .NET 10 SDK. The first command rebuilds the original measurements. The second reproduces the native eight-image experiment.

```powershell
dotnet run -c Release --project src/Weightless -- suite artifacts/sprint-review
dotnet run -c Release --project experiments/0006-Settlement/v1 -- artifacts/labs
```

The common parent is the 19-byte `add.checked.plain` graph, SHA-256 `F29EB79B426681794A0A7BDC22C47CF38B9792C93ADF96326CD3964563877B34`. Each experiment verifies the digest and decoded structure before deriving a candidate. Each publishes its own evidence under `OUTPUT/0006-v1/<lab>/vN/`. A rerun checks every published file for byte identity. Distinct route versions can coexist under one output root. [Seed bytes](../experiments/0001-Addition/baseline-v1/add-checked.wlb) · [Runner](../experiments/0006-Settlement/support-v1/LabRun.cs)
