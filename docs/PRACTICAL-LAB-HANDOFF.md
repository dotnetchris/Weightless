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
