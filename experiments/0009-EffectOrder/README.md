# Effect order lab

Find when an execution schedule can change while every observable effect agrees. The shared parent is commit `65fe4b9` and its pinned checked Add graph. Your lab owns `experiments/0009-EffectOrder/`. [Common handoff and publishing protocol](../../docs/LAB-HANDOFF.md)

V1 calls the seed as a helper, then performs a store and a load. The alternative exchanges the two memory operations. Distinct in-bounds arrays supply a control. An aliased cell separates the return values. A successful idempotent store followed by a failing load supplies the stronger witness. Both executions end with identical buffers and the same exception type, while exactly one records a write. [Both graphs](v1/EffectGraphs.cs) · [Full v1 observations](evidence-v1/result.json)

**V2 assignment.** Propose a predicate on inputs that admits a change of schedule. Check it against C# implementations of both orders and both decoded graphs. Use distinct equal buffers, aliases at the same and at different indices, unchanged and changed writes, empty buffers, failed reads and writes, and checked overflow before either memory operation. Give each execution fresh buffers that preserve the original identity relationships. Compare the return or exception type, stdout, ordered write trace and final contents by buffer identity.

One admitted disagreement refutes soundness on the corpus. Record replayable witnesses, how many equivalent cases the guard admits and how many it leaves on the original schedule. Distinguish a sufficient guard from one that also classified every measured pair correctly. A future version can change the observation contract to include a fault location or add a second write and see which permissions survive.

```powershell
dotnet run -c Release --project experiments/0009-EffectOrder/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0009-EffectOrder/v2 -- artifacts/labs
```
