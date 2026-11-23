# 0005 Guarded Addition

Parent experiment. [0001 Addition](../0001-Addition/README.md) found two checked intermediate overflows.

Claim. For pure `Int32` operands, use the transfer only when widened `a + k` and `b - k` both fit in `Int32`. A different return value or exception type, including a lost final overflow, defeats the claim. When the guard fails, evaluate the original.

```powershell
dotnet run --file ./experiments/0005-GuardedAddition/CheckGuard.cs
Push-Location ./experiments/0005-GuardedAddition
lean --version
lean ./CheckedTransfer.lean
Pop-Location
```

The C# probe enumerates a Cartesian product near both endpoints and around zero. The Lean model explicitly treats each checked operation as either an `Int32`-range value or overflow and evaluates the left intermediate first. The guard assumes pure values and observes the value or overflow only.

On SDK 10.0.401 and .NET 10.0.12, all 1,331 grid cases and six named probes agreed. Of 773 admitted grid cases, 110 still overflowed during the final sum. The two original witnesses took the original expression through the fallback. [C# observations](guard-v1.json)

The Windows sandbox returned `NuGet.targets(198,5): error : Access to the path is denied.` on the first `dotnet run --file` attempt. Setting the child shell's `TEMP` and `TMP` to an existing directory under the ignored `artifacts/` folder allowed the original and guarded probes to run.

Lean 4.8.0 kernel-checked the result-or-overflow theorem and the `Int64` range of the widened intermediates for `Int32` inputs. The proof models pure values and the overflow observation. The C# replay exercises the implementation of that guard. [Checker output](lean-check.txt) · [Pinned Lean release](https://github.com/leanprover/lean4/releases/tag/v4.8.0)

For the next attempt, change the observation contract to include the source location of an overflow. Both expressions can still throw the same exception type from different addition sites.
