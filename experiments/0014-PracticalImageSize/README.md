# Complete image size for the practical programs

Does storing each complete v1 program as a separate zlib stream save bytes while preserving its behavior? The baseline is the retained program passed to the v1 decoder. The candidate is the entire file written by the .NET 10 `ZLibStream` encoder at `CompressionLevel.SmallestSize`, including the zlib header and checksum. The same rule applies to every file.

| Program | Baseline bytes | Candidate bytes | Bytes saved | Percent saved | Outcome |
| --- | ---: | ---: | ---: | ---: | --- |
| Ordered Add preconditions | 39 | 40 | −1 | −2.56% | Grew |
| Two-property map | 154 | 117 | 37 | 24.03% | Shrank |
| Array-to-list balance history | 31 | 35 | −4 | −12.90% | Grew |
| **Three independent files** | **224** | **192** | **32** | **14.29%** | **Shrank** |

Bytes saved equal baseline minus candidate. The percentage uses the baseline as its denominator. The aggregate counts one deployed file per program. The guard helper produces the same v1 file as the explicit conditionals and is exercised as a control. Both formats assume the existing .NET runtime, v1 interpreters and CLR bindings. This comparison uses the runtime's built-in zlib support, with zero added dictionary, vocabulary or decoder-extension bytes. It measures storage of these known fixtures. C# supplies the behavior oracle. [Files, hashes, sizes and observations](evidence-v1/result.json)

**Behavior verdict: tested.** The replay reopens all ten candidate streams, including the alternate programs, and checks the recovered bytes against their v1 files. It runs the seven inventory cases and seven controls on each representation. Reversing the mapped assignments changes the row visible after the second setter rejects its value. Delaying history appends loses the first successful write on late overflow. Both encodings reproduce these distinctions. [Inventory oracle and controls](../0013-ProductSettlement/v1/InventoryWorkflow.cs)

## Replay

Run from the repository root with the .NET 10 SDK. The retained candidate images were generated with SDK 10.0.401.

```sh
dotnet run -c Release --project experiments/0014-PracticalImageSize/v1 -- artifacts/labs
```

The command prints the per-program and aggregate measurements and publishes the streams and result under `artifacts/labs/0006-v1/practical-image-size/v1/`. Expect `practical-image-size/v1 tested (checks passed)`. The original inputs are the checked-in files under the [guard](../0010-AddInputGuards/evidence-v1/), [mapping](../0011-PropertyMapping/evidence-v1/) and [history](../0012-BalanceHistory/evidence-v1/) experiments.
