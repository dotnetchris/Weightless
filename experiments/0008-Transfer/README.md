# Transfer lab

Determine when a stored arithmetic candidate can replace its parent. The shared parent is commit `65fe4b9` and its pinned checked Add graph. Your lab owns `experiments/0008-Transfer/`. [Common handoff and publishing protocol](../../docs/LAB-HANDOFF.md)

V1 constructs a distinct checked graph for `(a + 1) + (b - 1)`. Decoding either wire format reproduces both parent counterexamples. For example, `(int.MaxValue, 0)` returns the maximum from the original and raises `OverflowException` in the candidate. The refuted unconditional relationship stays in the history. [V1 constructor](v1/TransferGraph.cs) · [Native counterexamples](evidence-v1/result.json)

**V2 assignment.** Keep the fixed transfer `k = 1`. Serialize both the original and the transferred graph. Evaluate the two intermediates in `Int64` on pure inputs. When both fit in `Int32`, select the decoded candidate. Otherwise select the decoded original. Compare the selected value or exception type with an independent compiled C# checked sum. Exercise both fallbacks, admitted endpoint values, normal sums, and admitted final overflows of both signs. Run a deterministic boundary grid and publish the two paths' counts, the named traces and the replayable graph bytes.

A changed return, lost or new overflow, wrong selection, or input outside the intended guard admitted by narrowing arithmetic defeats the experiment. State where the selection runs and where the graph executes. The existing Lean theorem models pure arithmetic and result-or-overflow observations. A proof of the graph interpreter or of a different observation contract would require additional work. A future version can move selection into a typed graph and check the correspondence. [Parent C# grid and Lean proof](../0005-GuardedAddition/README.md)

```powershell
dotnet run -c Release --project experiments/0008-Transfer/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0008-Transfer/v2 -- artifacts/labs
```
