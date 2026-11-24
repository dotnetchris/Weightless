# Agent brief 0010 · Ordered Add input guards

Implement `add-input-guards/v1`. Test whether two source forms lower to one typed plan that enforces the same public preconditions on checked Add. Guard `a` first, then `b`. Reject each value strictly below `-1`. Only after both pass, evaluate `checked(a + b)`. This extends the original Add with caller-facing argument validation. Experiment 0005 used a guard for a different purpose, choosing an arithmetic implementation. [Common handoff](../../docs/PRACTICAL-LAB-HANDOFF.md) · [Original Add](../0001-Addition/AddSeed.cs)

The supplied C# methods express the same intended contract using explicit `if` and `ArgumentOutOfRangeException.ThrowIfLessThan(a, -1)` followed by the same call for `b`. This is the first-party helper for the project's .NET 10 target. The explicit throw supplies `nameof(a)` or `nameof(b)` and the rejected value. Compare result or exception type, plus `ParamName` and `ActualValue` for argument failures. Localized message text and stack frames fall outside this contract. [C# methods](reference/GuardedAdd.cs) · [Microsoft API](https://learn.microsoft.com/en-us/dotnet/api/system.argumentoutofrangeexception.throwiflessthan?view=net-10.0) · [Analyzer rule CA1512](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1512)

**Build.** Bind each method body separately with the SDK's Roslyn assemblies. Recognize the signed comparison and explicit throw, and the resolved `System.ArgumentOutOfRangeException.ThrowIfLessThan<int>` call, including the effective parameter name. Lower both to ordered guards with an operand slot, signed lower bound and parameter name, followed by the checked seed body. Compare normalized plans and standalone wire bytes. Keep source provenance outside the comparison. Changing a bound in one source must change its imported plan.

| Input | Required outcome | What it exposes |
| --- | --- | --- |
| `(-1, -1)` | Return `-2` | Inclusive lower bound and unrestricted result |
| `(-2, -3)` | Range exception, `a`, actual value `-2` | Which guard runs first |
| `(0, -2)` | Range exception, `b`, actual value `-2` | Second operand and diagnostics |
| `(int.MinValue, -1)` | Range exception for `a` | Guard precedes an addition that would overflow |
| `(int.MaxValue, 1)` | `OverflowException` | Admitted operands retain checked arithmetic |

**Discriminate.** Run both C# forms and both independently imported images on the Cartesian square of `{int.MinValue, -2, -1, 0, 1, int.MaxValue}`. Reopen a variant with only the first bound changed to `0`. On `(-1, 0)` it must now reject `a`. Reverse the serialized guard order on `(-2, -3)` and require `b` with actual value `-3`. A reported guard ID alone cannot satisfy either control. Retain the first discrepancy, including parameter metadata. [Runnable reference checks](reference/Program.cs)

**Replay.** The first project is supplied. Implement the second and publish its decoded controls and evidence under `0010-AddInputGuards/evidence-v1/`.

```powershell
dotnet run -c Release --project experiments/0010-AddInputGuards/reference
dotnet run -c Release --project experiments/0010-AddInputGuards/v1 -- artifacts/labs
```
