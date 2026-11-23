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
