# Agent brief 0012 · Appending running balances

Implement `balance-history/v1`. Test whether one decoded program can traverse a runtime-length `int[]`, maintain a checked accumulator and append every successful balance to the caller's existing `List<int>`. An empty input returns the opening balance. Zero and negative deltas are ordinary data. This adds a growable collection and iteration to the earlier fixed-buffer work. [Common handoff](../../docs/PRACTICAL-LAB-HANDOFF.md) · [C# reference](reference/BalanceHistory.cs) · [Fixed-buffer predecessor](../0003-Buffer/README.md)

**Build.** Model array traversal, checked Add, append and return explicitly. A route-local bounded iteration over the input length is sufficient. Use the same saved program on every array length. The v1 graph supports forward branches and fixed buffers, so keep the iteration semantics in this lab. Report how the plan was constructed and which operations are provided by the host. Capture result or exception, the complete ordered contents and count of the caller's list, and the final input array. Compare fresh inputs for each execution and hold a reference to the original list.

| Opening, deltas, initial history | Required observation |
| --- | --- |
| `10, [], [90]` | Return `10`, history `[90]`. |
| `10, [3, 0, -2], [90]` | Return `11`, history `[90, 13, 13, 11]`. |
| `int.MaxValue, [1], [90]` | Overflow on the first delta, history `[90]`. |
| `int.MaxValue - 1, [1, 1], [90]` | Overflow on the second delta, history `[90, int.MaxValue]`. |
| `int.MinValue + 1, [-1, -1], []` | Overflow on the second delta, history `[int.MinValue]`. |

**Discriminate.** Encode a valid variant that appends the input delta instead of the updated accumulator. On the ordinary three-element case it still returns `11`, but the caller must see `[90, 3, 0, -2]`. Also test a candidate that stages every new balance in a temporary list and commits with `AddRange` only after the entire traversal succeeds. The late-overflow case must distinguish the staged and incremental programs even though their exception types agree. Retain that counterexample. Run at least one longer deterministic array through the original image to check that length is read at execution time. [Runnable reference checks](reference/Program.cs)

**Replay.** The reference project is supplied. Implement the second project and retain the images, observation vectors and first staged-commit counterexample in the lab's `evidence-v1/`.

```powershell
dotnet run -c Release --project experiments/0012-BalanceHistory/reference
dotnet run -c Release --project experiments/0012-BalanceHistory/v1 -- artifacts/labs
```
