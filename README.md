# Weightless
A programming system where the human is no longer burdened with syntax, boilerplate, implementation details or even choosing the final execution architecture.  Intent has weight. Implementation doesn't have to.

## Settlement and independent labs

Packed checked Add enforces a nonnegative result in the same 19 bytes as unrestricted baseline Add. A native experiment replays that distinction across two operand orders. Three independently versioned labs build on the same saved graph to measure capacity, select arithmetic rewrites and test effect ordering. [Settlement report](docs/SETTLEMENT-2026-11-23.md) · [Agent handoff and replay commands](docs/LAB-HANDOFF.md)

## Thanksgiving sprint replay

Run from the repository root with the .NET 10 SDK.

```powershell
dotnet run -c Release --project src/Weightless -- suite artifacts/thanksgiving
```

The suite writes both encodings of six canonical graphs, launches a fresh process to decode and execute them against compiled C# references, and saves the size table and the raw Release timings. Roslyn binds the Add and top-level Hello World sources. The buffer and repeated-addition graphs are hand-built and checked against C#.

- [Experiment results](experiments/0004-TypedContext/evidence-v1/report.md) and [format specification](docs/GRAPH-FORMAT-V1.md)
- [Starting Addition experiment and baseline replay](experiments/0001-Addition/README.md)
- [Guarded Addition experiment and Lean proof](experiments/0005-GuardedAddition/README.md)
- [Versioned candidate and evidence records](experiments/semantic-cube-v1.json)
