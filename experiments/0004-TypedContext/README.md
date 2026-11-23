# 0004 Experimental typed-context codec

Claim. On this versioned corpus, omitting an addition result tag when both decoded operand types are `Int32` preserves the graph and observations and recovers exactly one byte per addition. A graph or observation mismatch, a noncanonical re-encoding, or a different byte count defeats the claim.

From the repository root with the .NET 10 SDK, run:

```powershell
dotnet run -c Release --project src/Weightless -- suite artifacts/thanksgiving
```

The suite writes both wire encodings, then launches a fresh verifier process. See [the format specification](../../docs/GRAPH-FORMAT-V1.md) for the fixed inference rule, field layout, and validity constraints.

Each codec passed six graph comparisons. Together they passed 62 C# comparisons and 16 malformed-input probes. The 64-addition chain fell from 334 to 270 bytes. Hello World stayed at 31 bytes because it has no addition tag to omit. For the checked Add pair, the plain and constrained baseline sizes were 19 and 20 bytes. The packed sizes were 18 and 19 bytes. At 19 bytes, packed/constrained fits exactly within baseline/plain. [Size and timing tables](evidence-v1/report.md) · [Raw verification](evidence-v1/verification.json) · [Raw timings](evidence-v1/timings.json) · [Roslyn provenance](evidence-v1/provenance.json)

The timing batches show enough spread that small differences are inconclusive. Execution uses the same interpreter on equal graphs and the harness uses the same input schedule on both sides. For a next attempt, increase the repeated arithmetic count past the operand-reference boundary at slot 128. That changes the reference width while leaving the one-byte rule per addition fixed.

The original LittleBits primitive and executable vectors remain unavailable. Replaying an original vector is still the first step in testing transfer from LittleBits.
