# Capacity lab

Measure how much enforceable meaning fits in the original wire budget. The shared parent is commit `65fe4b9` and its pinned checked Add graph. Your lab owns `experiments/0007-Capacity/`. [Common handoff and publishing protocol](../../docs/LAB-HANDOFF.md)

V1 places the break-even point precisely. With no Add, packed/constrained takes 15 bytes against 14 for baseline/plain. With one Add the sizes tie at 19. With two Adds they are 23 and 24. In each case `(-1, 0)` returns a negative result on the plain graph and fails the required predicate on the constrained graph. [V1 graph builder](v1/AddChain.cs) · [V1 evidence](evidence-v1/result.json)

**V2 assignment.** Test whether `packed/constrained − baseline/plain = 1 − AddCount` still holds when counts and slot references first require two bytes. Use the actual v1 codec. Include the zero-Add control, the original 64-Add size regime and consecutive chain lengths on both sides of slot 128. Identify which encoded count or reference first widens. Record all four standalone sizes, including framing, for every length. Decode each form, re-encode it and compare its observations with a checked C# loop, including a negative result, a normal result and an arithmetic overflow where an Add executes.

A wrong delta, a lost negative-result distinction, a mismatched overflow, or a graph or byte round-trip mismatch defeats the claim on that fixture. Publish the first failing count and input if one appears. Keep the graph semantics and both v1 wire formats fixed while measuring this question. A later lab version can try a different encoding and charge its full decoding metadata.

```powershell
dotnet run -c Release --project experiments/0007-Capacity/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0007-Capacity/v2 -- artifacts/labs
```
