# Weightless parallel lab handoff

**Start all three v3 investigations at the local Git tag `weightless-labs-v3-start`.** It contains this handoff and the three measured v2 routes from merge `2a1f278b7bf54f31c8d21d8603f002b26f6f6566`. Each agent can replay its control and publish into its own directory immediately. Every route starts from the same 19-byte checked Add with its verified SHA-256. [Settlement report](SETTLEMENT-2026-11-23.md) · [Checked Add bytes](../experiments/0001-Addition/baseline-v1/add-checked.wlb)

| Agent and owned directory | V3 assignment | What decides it | Independent v2 head |
| --- | --- | --- | --- |
| [0007 Capacity](../experiments/0007-Capacity/README.md) | Encode a run of repeated checked Adds in one record. | Locate the first net saving over packed v1 after charging all metadata. Recover each graph and observation, including the mixed-operation control. | `codex/lab-capacity-v2` · `92ec024` |
| [0008 Transfer](../experiments/0008-Transfer/README.md) | Express guarded selection inside a serialized typed program. | Recover the correct branch and result on all 121 v2 pairs, preserve final overflows, and measure the complete encoded selector. | `codex/lab-transfer-v2` · `69eabb6` |
| [0009 Effect order](../experiments/0009-EffectOrder/README.md) | Add the faulting operation to the observation contract. | Test the prediction of 120 newly distinguishable pairs and 286 surviving admissions. Retain the first counterexample to either count or to guard soundness. | `codex/lab-effects-v2` · `3283162` |

The original code checkpoint is `65fe4b9bafc4a1593789ab7d3168e83d76d60e70`. It contains the corrected input copier, common runner, settlement report and all v1 controls. The initial documented handoff at `0d2ba853f7ae16a4268b37b4917ea705f5e7d9f4` is the direct parent of all three v2 heads. Each head builds and runs with only its own v2 present. Future experiments that need a different codec or observation can put that variation inside their versioned project while continuing to read the frozen parent.

## Running a branch

Run commands from the repository root with the .NET 10 SDK. The following replays the common control and the three v2 routes. The last three commands are independent and may run concurrently. To reproduce a lab's first controls, use its `v1` project in the same command.

```powershell
dotnet run -c Release --project experiments/0006-Settlement/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0007-Capacity/v2 -- artifacts/labs
dotnet run -c Release --project experiments/0008-Transfer/v2 -- artifacts/labs
dotnet run -c Release --project experiments/0009-EffectOrder/v2 -- artifacts/labs
```

Each route verifies the frozen seed digest and structure before execution. The runner records its route version, parent graph ID and SHA-256, claim, verdict, check status and evidence. A refutation with passing checks means the predicted counterexample reproduced. Graph generation, validation and observation run locally with the same source files used by the main interpreter. [Runner and record format](../experiments/0006-Settlement/support-v1/LabRun.cs)

## Publishing a sibling version

1. Fork the common starting revision above and add a `v3/` project in the lab's directory. Import the same `support-v1/LabRoute.props` as v2. Link the existing graph constructors the experiment uses. Pass the existing lab name and version `3` to `LabRun.Run`. The project path selects the route. No central registration or sibling build is required.
2. Record the prior route, observation contract, independent expectation, actual measurements and verdict. Write additional candidate wires or counterexamples to the staging directory supplied by the runner. Keep hypotheses that failed visible alongside the later result.
3. Replay with `dotnet run -c Release --project experiments/<experiment>/v3 -- artifacts/labs`. The runner publishes every file together to `artifacts/labs/0006-v1/<lab>/v3/`. Identical reruns succeed. Different content under an existing version yields an error and preserves the earlier output.
4. Copy the small, replayable evidence set to the lab's `evidence-v3/` and record the exact command and verdict in its README. Later versions follow the same convention. A cross-lab experiment can cite the contributing route versions and hashes explicitly.

## V3 results and replay

Each route started in its own source snapshot of `weightless-labs-v3-start` at `d17fb0f`. Each snapshot contained the common runner and checked Add seed, the frozen native sources, and one lab. Capacity also used the earlier Buffer C# oracle. Each lab reproduced its own v2 evidence byte for byte and then built and ran its v3 project with the sibling directories absent.

The concurrent Release replay with SDK 10.0.401 reproduced all 19 saved v3 files byte for byte. A separate publication test seeded conflicting Capacity evidence in a scratch output root. Capacity preserved the earlier version while Transfer and Effect Order each published their full evidence to that same root.

| Route | Result | Evidence |
| --- | --- | --- |
| Capacity v3 | The first saving was at **two Adds**, from **22 to 18 bytes** for the complete plain wire. The buffer control stayed at 61 bytes. All 338 C# comparisons matched. | [Capacity measurements](../experiments/0007-Capacity/README.md#v3-experiment) |
| Transfer v3 | Both serialized selectors matched all **121** branch and outcome pairs, including final overflows. The complete baseline and packed programs use **74 and 70 bytes**. | [Transfer measurements](../experiments/0008-Transfer/README.md#v3-experiment) |
| Effect order v3 | Adding the fault site distinguished **120** additional pairs. The refined guard admitted **286 of 448** and preserved every admitted observation. | [Effect-order measurements](../experiments/0009-EffectOrder/README.md#v3-experiment) |

Run any of these commands from the repository root. Each publishes to its own `v3/` evidence directory beneath the output root.

```powershell
dotnet run -c Release --project experiments/0007-Capacity/v3 -- artifacts/labs
dotnet run -c Release --project experiments/0008-Transfer/v3 -- artifacts/labs
dotnet run -c Release --project experiments/0009-EffectOrder/v3 -- artifacts/labs
```
