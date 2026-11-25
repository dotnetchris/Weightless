# Transfer lab

Determine when a stored arithmetic candidate can replace its parent. The shared parent is commit `65fe4b9` and its pinned checked Add graph. Your lab owns `experiments/0008-Transfer/`. [Common handoff and publishing protocol](../../docs/LAB-HANDOFF.md)

V1 constructs a distinct checked graph for `(a + 1) + (b - 1)`. Decoding either wire format reproduces both parent counterexamples. For example, `(int.MaxValue, 0)` returns the maximum from the original and raises `OverflowException` in the candidate. The refuted unconditional relationship stays in the history. [V1 constructor](v1/TransferGraph.cs) · [Native counterexamples](evidence-v1/result.json)

**V2 assignment.** Keep the fixed transfer `k = 1`. Serialize both the original and the transferred graph. Evaluate the two intermediates in `Int64` on pure inputs. When both fit in `Int32`, select the decoded candidate. Otherwise select the decoded original. Compare the selected value or exception type with an independent compiled C# checked sum. Exercise both fallbacks, admitted endpoint values, normal sums, and admitted final overflows of both signs. Run a deterministic boundary grid and publish the two paths' counts, the named traces and the replayable graph bytes.

A changed return, lost or new overflow, wrong selection, or input outside the intended guard admitted by narrowing arithmetic defeats the experiment. State where the selection runs and where the graph executes. The existing Lean theorem models pure arithmetic and result-or-overflow observations. A proof of the graph interpreter or of a different observation contract would require additional work. A future version can move selection into a typed graph and check the correspondence. [Parent C# grid and Lean proof](../0005-GuardedAddition/README.md)

```powershell
dotnet run -c Release --project experiments/0008-Transfer/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0008-Transfer/v2 -- artifacts/labs
```

## V2 results

**Tested.** Each codec passed 121 grid pairs and nine named probes. The C# host computed both widened intermediates and selected a decoded graph. For this fixed transfer, `a == int.MaxValue || b == int.MinValue` specifies exactly when either intermediate fails to fit. This gave an independent check on the identity of the graph handed to the interpreter. Every returned integer and exception type matched the compiled C# expression `checked(a + b)`. The nine named probes also occur in the grid. [Inputs, selected paths, outcomes and counts](evidence-v2/result.json)

| Route selected | Grid pairs per codec | Observed behavior |
| --- | ---: | --- |
| Transferred | 100 | 80 returned normally. Ten sums exceeded the upper limit and ten crossed the lower limit. All 20 produced `OverflowException`. |
| Original | 21 | Both v1 counterexamples took this path, returning `int.MaxValue` and `int.MinValue`. With both intermediates out of range, `(int.MaxValue, int.MinValue)` returned `-1`. |

The original uses 19 baseline bytes and 18 packed bytes. The specialized transfer uses 41 and 38. All four images reconstructed their own graphs and re-encoded byte for byte. The selector runs in host C# and contributes no bytes to those wire sizes. This experiment measures outcome agreement for already evaluated operands. The existing Lean theorem covers the arithmetic model under value-or-overflow observation. Interpreter correspondence and the execution cost of dispatch are separate questions. [Saved wires](evidence-v2/) · [Model theorem](../0005-GuardedAddition/CheckedTransfer.lean)

**Next experiment.** Build a v3 candidate that encodes the same selection using typed range tests and a branch. Run the same 121 pairs through both decoders, compare the executed branch and outcome against the independent v2 oracle, and charge the complete branched graph to each wire format. Either a wrong branch or a lost final overflow defeats that candidate.

## V3 experiment

For the same 121 pure `Int32` pairs at `k = 1`, a decoded selector should route to the original exactly when `a == int.MaxValue` or `b == int.MinValue`, and its outcome should match compiled `checked(a + b)`, including final overflow. An incorrect executed arm or outcome defeats the claim. Both wire sizes include the range tests, branch, and both arithmetic bodies.

```powershell
dotnet run -c Release --project experiments/0008-Transfer/v3 -- artifacts/labs
```

**Tested.** Both decoders recovered the complete selector and re-encoded it byte for byte. On all **121** grid pairs per codec, the executed branch and C# outcome matched. The transfer ran on **100** pairs and the original ran on **21**. All **20** final overflows in the transferred arm and **10** in the original arm were preserved per codec. On `(0, 1)`, swapping the branch targets changed the traced arm while both executions returned `1`. [Branch traces and named probes](evidence-v3/result.json)

| Wire | Native arm table | Selector and framing | Complete |
| --- | ---: | ---: | ---: |
| Baseline selector | 54 bytes | 20 bytes | **74 bytes** |
| Packed selector | 50 bytes | 20 bytes | **70 bytes** |

The program starts with `WSB 01` for baseline or `WSP 01` for packed, where `01` is a single version byte. Each of two `01` opcodes reads a typed `Int32` argument, adds a stored little-endian `Int32` shift in widened arithmetic, and yields a Boolean fit test. The `02` branch stores the two target arm IDs and takes the first target when both tests pass. The following byte count frames a single native v1 graph containing both pure arms. Argument indices, arm IDs and the byte count use shortest unsigned base-128 integers. The route evaluator runs those decoded tests and the branch, and the shared interpreter executes the selected native body. [Codec and evaluator](v3/SelectorCodec.cs) · [Saved selector wires](evidence-v3/)
