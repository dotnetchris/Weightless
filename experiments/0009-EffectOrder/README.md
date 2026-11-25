# Effect order lab

Find when an execution schedule can change while every observable effect agrees. The shared parent is commit `65fe4b9` and its pinned checked Add graph. Your lab owns `experiments/0009-EffectOrder/`. [Common handoff and publishing protocol](../../docs/LAB-HANDOFF.md)

V1 calls the seed as a helper, then performs a store and a load. The alternative exchanges the two memory operations. Distinct in-bounds arrays supply a control. An aliased cell separates the return values. A successful idempotent store followed by a failing load supplies the stronger witness. Both executions end with identical buffers and the same exception type, while exactly one records a write. [Both graphs](v1/EffectGraphs.cs) · [Full v1 observations](evidence-v1/result.json)

**V2 assignment.** Propose a predicate on inputs that admits a change of schedule. Check it against C# implementations of both orders and both decoded graphs. Use distinct equal buffers, aliases at the same and at different indices, unchanged and changed writes, empty buffers, failed reads and writes, and checked overflow before either memory operation. Give each execution fresh buffers that preserve the original identity relationships. Compare the return or exception type, stdout, ordered write trace and final contents by buffer identity.

One admitted disagreement refutes soundness on the corpus. Record replayable witnesses, how many equivalent cases the guard admits and how many it leaves on the original schedule. Distinguish a sufficient guard from one that also classified every measured pair correctly. A future version can change the observation contract to include a fault location or add a second write and see which permissions survive.

```powershell
dotnet run -c Release --project experiments/0009-EffectOrder/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0009-EffectOrder/v2 -- artifacts/labs
```

## V2 result

**The guard admitted 406 of 448 inputs.** Every admission preserved the complete observation, and each of the 42 declined inputs distinguished the two schedules. The guard runs in the host against the initial, single-threaded buffer state. It admits a reorder if checked Add overflows before memory access, or if the write index is invalid. After a valid sum and write index, it requires a valid read and either different cells or a same-cell write of the existing value. [Guard and C# references](v2/Program.cs)

| First applicable condition | Inputs | Decision | Observable reason |
| --- | ---: | --- | --- |
| Sum overflows | 224 | Admit | Both executions stop before either memory operation. |
| Write index is invalid | 160 | Admit | Both executions raise the bounds exception without a successful write. The contract observes the exception type. |
| Write is valid and read index is invalid | 40 | Decline | Only store-first records a write, including when the value already equals the destination. |
| Both accesses are valid and name different cells | 20 | Admit | Both return the original source value and record the same write. |
| Same cell, unchanged value | 2 | Admit | Both return the stored value and record one write. |
| Same cell, changed value | 2 | Decline | The two reads observe different values. |

The Release replay crosses seven buffer layouts, four checked additions, and four choices for each index. Each C# reference clones its own starting buffers and logs an assignment after it succeeds. Both wire formats matched the corresponding C# schedule in all 1,792 comparisons. The selected decoded schedule matched store-first C# in all 896 comparisons. These counts settle the measured grid under the recorded observation contract. The evidence includes the input matrix, ten named witnesses with their complete observations, and all four replayable graph files. [V2 evidence](evidence-v2/result.json)

**V3 assignment.** Extend the observation with the stable operation name of the fault (`checked-add`, `store` or `load`). Keep the v2 grid and both schedules. Predict that the 120 normal-sum cases in which both indices are invalid will now disagree on fault site. Refine the invalid-write admission to require a valid read in those cases. Test for exactly those 120 newly distinguished pairs, 286 remaining admissions, and zero admitted differences in values, faults, writes or final buffers. Retain the first input that defeats any of those predictions.

## V3 experiment

Use the same decoded schedules and 448 input pairs. The stronger observation should distinguish the 120 normal sums where both accesses would fault. On those inputs the store-first graph faults at `store` and the load-first graph faults at `load`. The narrowed guard should admit 286 pairs. A newly distinguished pair outside that group, a different count, or a differing observation under the refined guard defeats the prediction.

```powershell
dotnet run -c Release --project experiments/0009-EffectOrder/v3 -- artifacts/labs
```

**Tested.** Adding the faulting operation distinguished **120** previously equal pairs. The refined guard admitted **286 of 448** pairs, down from **406** under the v2 contract. Every admission preserved the stronger observation. The 120 pairs came from 60 combinations of layout and two invalid indices, each with two non-overflowing sums. All 1,792 decoded-schedule comparisons and 896 selected-schedule comparisons matched the independent C# references. [Counts and retained counterexample](evidence-v3/result.json)

The first counterexample to the v2 guard under the stronger contract uses a shared buffer `[5, 7]`, `checked(2 + 3)`, and two indices of `-1`. Both schedules raise `IndexOutOfRangeException`, record no write and keep the buffer intact. Store-first names `store` as the fault site and load-first names `load`. The v3 guard declines the reorder.

The schedule wires reproduced v2 byte for byte. The route derives the fault site by running growing prefixes of the decoded schedule on fresh alias-preserving copies. The first faulting prefix must reproduce the full outcome, including any write that occurred before the fault. This probe applies to the deterministic, straight-line v2 schedules on local buffers. The separate C# references mark each operation immediately before executing it. [Fault-site probe](v3/FaultSiteProbe.cs) · [Saved schedule wires](evidence-v3/)
