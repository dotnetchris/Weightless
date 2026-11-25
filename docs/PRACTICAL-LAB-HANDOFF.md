# Practical behavior experiments

**Can a small decoded program preserve what a caller sees when work succeeds partway and then fails?** Use three everyday inventory operations to answer that question. Each agent has an ordinary C# implementation, executable expectations, its own native route to build, and a case that exposes a plausible wrong implementation.

| Agent brief and owned directory | Native research question | Decisive caller observation |
| --- | --- | --- |
| [0010 Add input guards](../experiments/0010-AddInputGuards/README.md) | Can explicit conditionals and the .NET throw helper lower to the same ordered guards? | With two invalid arguments the exception names the first. Two legal values of `-1` return `-2`. |
| [0011 Property mapping](../experiments/0011-PropertyMapping/README.md) | Can a two-property update preserve member binding, destination identity and setter order? | A rejected second assignment leaves the first change visible through the caller's existing object. |
| [0012 Balance history](../experiments/0012-BalanceHistory/README.md) | Can one serialized iteration over an array append the correct running balances to an existing list? | A later overflow preserves earlier appends, including duplicate balances. |

## Starting point

Use the Git tag `weightless-practical-v1-start` for all three agents. It adds these briefs and C# references to the completed v3 revision `68a323b3e79e23117f31e0db02bf72902a879ee2`. Assign one row per agent. Each owns its experiment directory and can finish with the other two new directories absent.

These commands run today from the repository root with the .NET 10 SDK. On SDK 10.0.401 the references passed 10 guarded input pairs through each C# form, four property updates, and six balance histories. These are the starting C# observations. Native equivalence, wire sizes and control results are for the receiving agents to establish.

```powershell
dotnet run -c Release --project experiments/0010-AddInputGuards/reference
dotnet run -c Release --project experiments/0011-PropertyMapping/reference
dotnet run -c Release --project experiments/0012-BalanceHistory/reference
```

Changing one C# implementation at a time confirmed that the references detect three mistakes. Rejecting `a == -1` failed the `(-1, -1)` case. Reversing the setters lost the first update when the second value was invalid. Staging the whole history before appending lost the first successful balance on late overflow. Each native brief adds a separate control that changes the stored program itself.

## Common implementation and evidence

1. Create the lab's `v1/` console project. Import `../../0006-Settlement/support-v1/LabRoute.props` as the earlier labs do. Link only the reference implementation file from your own `reference/` directory. Its `Program.cs` is the independent expectation checker. Compile or bind the reference as specified in the brief. Record which parts of the native plan came from source and which were built by hand. [Existing project example](../experiments/0008-Transfer/v3/Transfer.csproj) · [Roslyn binding examples](../src/Weightless/CSharpSeeds.cs)
2. Implement the smallest typed program that expresses the operation. The shared v1 graph already has checked Add and fixed-buffer access. Ordered argument throws, object properties, growable lists and iteration need route-local semantics. A small versioned format or envelope can carry them. Document the fields, host primitives and full encoded byte count, including any schema needed to interpret the program. The independent C# method supplies the expected behavior. The native evaluator must get its operative choices from the decoded plan. [Current wire model](GRAPH-FORMAT-V1.md)
3. Save the program image. Reopen it, verify structural equality and deterministic re-encoding, then execute it on fresh inputs. Compare both normal and exceptional observations. Change a **valid** encoded choice and require the predicted change in the actual value, exception details or caller-owned state. This addresses the earlier review finding that a route can report the intended branch while always executing the original body.
4. Use `LabRun.Run` with the route name in your brief and version `1`. Its envelope pins the shared checked Add seed. In the evidence, identify the actual source method(s), their file hash, the relevant predecessor experiment and any use of the seed in execution. For property mapping the seed is runner provenance only. Write deterministic inputs, expected and actual observations, controls, complete wires and any first disagreement under the route's staging directory. [Runner and publication behavior](../experiments/0006-Settlement/support-v1/LabRun.cs)
5. Retain the replayable outputs in your `evidence-v1/` and append the command and interpretation to your README. Use **tested** for agreement on the stated corpus, **refuted** for a concrete valid-input counterexample to the stated claim, and **failed** for a build, decode, oracle or control failure that prevents a verdict. A deliberately different candidate gets its own claim and witness. Successive experiments use sibling `v2/` and `evidence-v2/` directories.

Completion means a clean checkout can run each lab independently and regenerate its retained files. The combined replay may launch all three against the same output root. A later integration decision can use the three records to see which operations deserve shared graph support.

## V1 results and replay

Each v1 route built and ran in its own source snapshot of `weightless-practical-v1-start` at `1ac9d19`, with the other two practical experiment directories absent. Each route verified its own C# reference before the native run. The concurrent Release replay with SDK 10.0.401 reproduced all 13 saved evidence files byte for byte.

An intentional Add Input Guards output conflict preserved that route's earlier files while Property Mapping and Balance History published into the same output root. Source digests in the evidence use UTF-8 with LF line endings so they can be compared across checkout settings.

| Experiment | Result | Complete image |
| --- | --- | ---: |
| [0010 Add input guards](../experiments/0010-AddInputGuards/README.md#v1-results) | Both C# forms lowered to identical bytes and passed all 72 reference comparisons. Changing a stored bound or guard order changed the actual thrown diagnostic. | 39 bytes |
| [0011 Property mapping](../experiments/0011-PropertyMapping/README.md#v1-results) | Both getter binding and setter order changed observed row state in the controls. The original plan kept the first write visible after the second setter failed. | 154 bytes, including 140 for schema |
| [0012 Balance history](../experiments/0012-BalanceHistory/README.md#v1-results) | One image matched all seven cases, including 257 deltas. The staged-commit counterexample lost a successful append on late overflow. | 31 bytes |

Run any route from the repository root. Each publishes evidence under its own route and version beneath the output root.

```powershell
dotnet run -c Release --project experiments/0010-AddInputGuards/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0011-PropertyMapping/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0012-BalanceHistory/v1 -- artifacts/labs
```

## Independent v2 handoff

Each new encoding brief below also needs a same-behavior size comparison as a primary result. For the two-sided guard and the optional map, encode the extended behavior on both sides before computing a saving. The earlier one-sided guard and unconditional map measure the cost of changing the contract. For the loop import, exact reproduction of the v1 file is a measured tie. Report baseline and candidate bytes, signed savings and percentage alongside the caller observations. [Measurement guidance](../CONTRIBUTING.md#what-a-reviewer-needs) · [Worked comparison](../experiments/0014-PracticalImageSize/README.md)

All three labs can start from the revision containing `0013-ProductSettlement/evidence-v1/result.json` with SHA-256 `E342C57C9CD24CE057E0665EB75CBFBF595309406B6AFFB2190897F396558B70`. Replay `product-settlement/v1` once to see how each v1 image affects an inventory workflow. Each lab owns the `v2/` and `evidence-v2/` siblings of its own experiment, uses its own compiled C# reference, and can publish to its existing route name at version 2 without a sibling lab's output. Retain the regenerated images, the component byte counts, the reference and decoded observations, and a valid changed image with a predicted observable difference. [Product report and replay](PRODUCT-SETTLEMENT-2026-11-24.md#replay-the-settlement)

| Lab | Question and implementation | Decisive observations |
| --- | --- | --- |
| **0010 · Two-sided adjustment guard** | Validate `a >= -1`, then `-1 <= b <= 100`, then return `checked(a + b)`. Bind the explicit conditionals and the corresponding first-party lower- and upper-bound throw helpers. Measure the extra bytes for the added bound relative to 39. | Test each boundary and its neighbors, both operands invalid, and `int.MaxValue + 1` after legal guards. Record the rejected parameter and actual value. Changing only the encoded upper bound from 100 to 99 must turn `(0,100)` from return `100` into a fault on `b`. |
| **0011 · Optional property update** | Apply nullable `Units` and `AlertBelow` to the existing row in that order. An absent value skips its setter and a present zero writes zero. Preserve the returned row identity. Separate schema cost from the cost of conditional assignment. | Run absent, zero, both present and rejected-second-setter cases against a prepopulated row, including successive calls on one row. With starting `(8,4)`, `(17,-2)` must leave `(17,4)` at the exception. Reversing only the stored assignments must leave `(8,4)`. |
| **0012 · Import the balance-history loop** | Bind the checked-in C# reference's array `foreach`, checked addition and `List<int>.Add` call. Generate the existing 31-byte image from the bound operations. Compile a second source that appends the delta and import it through the same entry point. | Check both imports against their own compiled C# reference. Opening at `10` with history `[90]` and deltas `[3,0,-2]` must return `11` in both, with histories `[90,13,13,11]` and `[90,3,0,-2]`. Recheck empty and late-overflow inputs. Report whether the canonical source reproduces the v1 image byte for byte. |
