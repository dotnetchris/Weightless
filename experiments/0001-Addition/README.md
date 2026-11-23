# 0001 Addition

## Original C# rewrite

`CheckRewrite.cs` and `checked-transfer-v1.json` record the starting experiment. The two intermediate overflow witnesses refute transferring a constant between operands in checked `Int32` arithmetic for all inputs. The wrapping expressions agree on the four recorded inputs.

## Baseline graph replay

Claim. For the four recorded pairs and both overflow policies, a fresh process can decode the baseline Add bytes and recover the canonical graph and the C# original's value or exception type. A graph difference, byte change on re-encoding, or observation mismatch defeats the claim.

From the repository root with the .NET 10 SDK, run the two commands in order.

```powershell
dotnet run -c Release --project src/Weightless -- emit-add artifacts/addition
dotnet run -c Release --project src/Weightless -- verify-add artifacts/addition experiments/0001-Addition/checked-transfer-v1.json
```

The verifier also accepts the checked-in `experiments/0001-Addition/baseline-v1` directory as its first argument.

The baseline files are 19 bytes each. A fresh process on SDK 10.0.401 and .NET 10.0.12 recovered both overflow policies and the full graph, produced identical bytes on re-encoding, and matched all eight observations. The four pairs include a final checked overflow and both inputs that break the separate transfer candidate. See [comparison.json](baseline-v1/comparison.json).

The next experiment adds a widened guard to the previously refuted candidate and targets both ends of `Int32`, including cases where the final checked sum still overflows. [0005 Guarded Addition](../0005-GuardedAddition/README.md)
