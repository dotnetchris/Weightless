# Weightless parallel lab handoff

Start all three investigations at Git commit `65fe4b9bafc4a1593789ab7d3168e83d76d60e70`. That commit contains the settlement report, corrected input copier, common runner, all three v1 prototypes and their evidence. Each lab derives its graphs from the same 19-byte checked Add. An agent can run its control and publish its next version using only the common checkout and its own experiment directory. [Settlement report](SETTLEMENT-2026-11-23.md) · [Checked Add bytes](../experiments/0001-Addition/baseline-v1/add-checked.wlb)

| Agent | Question to settle | Owns | First control |
| --- | --- | --- | --- |
| Capacity | Does the recovered-byte budget survive the first reference-width transition? | [0007 brief](../experiments/0007-Capacity/README.md) | Zero, one and two Adds cross the break-even point. |
| Transfer | Can an input guard select between two independently serialized arithmetic graphs and preserve checked C# outcomes? | [0008 brief](../experiments/0008-Transfer/README.md) | Two saved inputs defeat unconditional transfer. |
| Effect order | Under which memory and failure conditions may a read move across a write? | [0009 brief](../experiments/0009-EffectOrder/README.md) | A shared cell changes the return. A failed read exposes an otherwise invisible write. |

## Running a branch

Run commands from the repository root with the .NET 10 SDK. The following replays the common control and the three v1 routes. The last three commands are independent and may run concurrently. No service or package feed is needed by the experiment code.

```powershell
dotnet run -c Release --project experiments/0006-Settlement/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0007-Capacity/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0008-Transfer/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0009-EffectOrder/v1 -- artifacts/labs
```

Each route verifies the frozen seed digest and structure before execution. The runner records its route version, parent graph ID and SHA-256, claim, verdict, check status and evidence. A refutation with passing checks means the predicted counterexample reproduced. Graph generation, validation and observation run locally with the same source files used by the main interpreter. [Runner and record format](../experiments/0006-Settlement/support-v1/LabRun.cs)

## Publishing a sibling version

1. Add a `v2/` project in the lab's directory. Import the same `support-v1/LabRoute.props` as v1. Link any v1 graph constructor the new experiment uses. Pass the existing lab name and version `2` to `LabRun.Run`. The project path selects the route. No central registration or sibling build is required.
2. Record the prior route, observation contract, independent expectation, actual measurements and verdict. Write additional candidate wires or counterexamples to the staging directory supplied by the runner. Keep hypotheses that failed visible alongside the later result.
3. Replay with `dotnet run -c Release --project experiments/<experiment>/v2 -- artifacts/labs`. The runner publishes every file together to `artifacts/labs/0006-v1/<lab>/v2/`. Identical reruns succeed. Different content under an existing version yields an error and preserves the earlier output.
4. Copy the small, replayable evidence set to the lab's `evidence-v2/` and record the exact command and verdict in its README. Subsequent work uses `v3/` and `evidence-v3/` with the same convention. A future cross-lab experiment can cite the contributing route versions and hashes explicitly.
