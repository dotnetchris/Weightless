# Agent brief 0011 · Updating an existing stock row

Implement `property-mapping/v1`. Test whether a decoded mapping can copy two integer properties into the caller's existing object and preserve the behavior of its setters. The starting C# assigns `update.Units` to `row.OnHand`, then `update.AlertBelow` to `row.ReorderPoint`, then returns `row`. The second setter rejects a negative value before storing it. Use non-null instances of the two supplied sealed types. [Common handoff](../../docs/PRACTICAL-LAB-HANDOFF.md) · [C# model and mapping](reference/StockMapping.cs)

**Build.** Bind the two assignments to their concrete source getter and destination setter symbols. Serialize the receiver, member identities and assignment order. Implement a route-local typed accessor table for these members. Calling the real declared setters is a valid first experiment. Report that boundary and the schema that maps stored IDs to accessors. Use separate, equally initialized objects for C# and decoded executions. Hold a second reference to the destination before the call. Observe the returned identity on success, destination fields and untouched SKU through that reference on every outcome, plus exception type, parameter name and actual value on failure.

| Initial row | Update `(Units, AlertBelow)` | Required observation |
| --- | --- | --- |
| `SKU-42, 8, 4` | `(17, 3)` | The same row now contains `SKU-42, 17, 3`. |
| Same | `(0, 0)` or `(8, 4)` | Zero and idempotent assignments succeed and preserve identity. |
| Same | `(17, -2)` | The setter rejects `value = -2`. Through the caller's alias the row contains `SKU-42, 17, 4`. |

**Discriminate.** Swap the two encoded source-member bindings on `(17, 3)`. Both values are legal and the observed destination must become `3, 17`. Separately reverse the assignment records on `(17, -2)`. The same setter exception must leave `8, 4` visible. This second witness also defeats a candidate that validates the whole update before performing either write. Extend the corpus with two distinct destination instances and apply successive updates to one of them. Retain both final states to detect writes to a replacement object or the wrong receiver. [Reference checks](reference/Program.cs) · [Earlier effect-order experiment](../0009-EffectOrder/README.md)

**Replay.** Build and run the supplied reference first. Implement the second project, save the original and changed images, and publish the identity and partial-update evidence in the lab's `evidence-v1/`.

```powershell
dotnet run -c Release --project experiments/0011-PropertyMapping/reference
dotnet run -c Release --project experiments/0011-PropertyMapping/v1 -- artifacts/labs
```
