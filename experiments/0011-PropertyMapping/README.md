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

## V1 results

**Tested.** The decoded program matched all four independent C# calls and all four steps of a two-row session. Successful calls returned the original object. On `(17, -2)`, the setter rejected `value = -2` and the caller's alias still saw `SKU-42, 17, 4`. The session ended with `SKU-42, 21, 1` and `SKU-7, 22, 5` after successive updates and a rejection on the first row. [Identity and caller-state evidence](evidence-v1/result.json)

Swapping the getter IDs made the decoded program write `3, 17` on `(17, 3)`. Reversing the assignments on `(17, -2)` produced the same setter exception with the row still at `8, 4`. Both changed images recovered their intended plans and re-encoded identically. [Saved images](evidence-v1/)

The `WPM` format uses a four-byte header ending in version byte `01`. It stores the receiver type table and the member schema using one-byte counts and length-prefixed UTF-8 names. Receiver slots 0 and 1 identify `StockUpdate` and `StockRow`. Member IDs 0 through 3 identify `Units`, `AlertBelow`, `OnHand` and `ReorderPoint` in schema order. Each schema member stores its receiver slot, access kind (`01` get or `02` set) and name. After the schema, the assignment count precedes records of four bytes each for source slot, getter ID, destination slot and setter ID. The final byte is the receiver to return. The complete image is **154 bytes**, including **140 bytes** of schema. Roslyn supplies the bindings and order. The decoder validates the declared schema and the typed accessor table invokes the real CLR properties, including the setter's validation. The shared checked Add seed authenticates the experiment's starting point. [Binder](v1/MappingBinding.cs) · [Wire and accessor table](v1/MappingCodec.cs)
