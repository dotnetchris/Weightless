# Weightless product settlement · November 24, 2026

**Weightless can carry a choice of behavior from source or a hand-built graph into saved bytes, reopen those bytes, and test the consequences at the caller.** Across the completed work, the definition of “same behavior” has grown from a returned integer to diagnostics, object identity, ordered effects and partially updated collections. The packing experiments reconstruct the same graph from fewer bytes. The rewrite and scheduling experiments identify which inputs admit a different graph under a given observation contract.

## One operation across three saved programs

The product replay starts with a stock row `(OnHand = 8, ReorderPoint = 4)` and an existing history `[90]`. It validates and adds two quantities, maps the result and threshold into the original row, then starts a projection from the mapped row's `OnHand`. Direct calls to the three C# reference methods provide the comparison. The decoded path reads the already published guard, mapping and history images. It records final input arrays, row identity on successful mapping, SKU, both fields, history and the result or fault details.

| Replayed input or single change | What the caller observed |
| --- | --- |
| `8 + 2`, threshold `3`, deltas `[3, 0, -2]` | The same row held `(10, 3)`. The history became `[90, 13, 13, 11]` and the workflow returned `11`. Substituting the image imported from the .NET helper reproduced this outcome. |
| Change only the source getters in the mapping image | The row held `(3, 10)`. The next program read its opening quantity as `3`, produced `[90, 6, 6, 4]`, and returned `4`. The binding choice affected a separately decoded consumer. |
| Change only the value appended in the history image | The workflow still returned `11` and the row still held `(10, 3)`. The caller's list became `[90, 3, 0, -2]`. |
| Both quantity arguments invalid, or checked sum overflows | Both faults occurred before mapping. The row and history kept their initial values. With `(-2, -3)`, exchanging guard order changed the diagnostic from `a = -2` to `b = -3`. |
| `15 + 2`, threshold `-2` | The setter rejected `value = -2`. The row held `(17, 4)` and history remained `[90]`. Exchanging the two assignments left the row at `(8, 4)` under the same exception. |
| Opening quantity `int.MaxValue - 1`, deltas `[1, 1]` | Mapping succeeded. The second projection addition overflowed. Immediate append left `[90, int.MaxValue]` and staging until completion left `[90]`. |

Seven baseline cases matched C#. Seven substitutions changed one component at a time, comprising one equivalent source form and six predicted behavioral differences. The three baseline images occupy **39 + 154 + 31 = 224 bytes** in their separate files. C# supplies the wiring, the object accessors and the list runtime. The 224-byte sum counts component storage only. The stock row records the accepted quantity and the history carries the projection. [Workflow implementation](../experiments/0013-ProductSettlement/v1/InventoryWorkflow.cs) · [Image hashes, cases and controls](../experiments/0013-ProductSettlement/evidence-v1/result.json)

## What each increment established

| Experiment | Independent contribution and limit |
| --- | --- |
| **0001 · Addition** | Checked and wrapping addition survived baseline serialization. Transferring a constant between operands introduced two intermediate-overflow counterexamples even when the original sum fit. [Inputs and outcomes](../experiments/0001-Addition/checked-transfer-v1.json) |
| **0002 · Hello World** | A bound top-level call reproduced the text, platform newline and exactly one stdout write. Both encodings used 31 bytes on the recorded fixture. [Output contract](../experiments/0002-HelloWorld/README.md) |
| **0003 · Buffer** | A branch and helper call preserved a successful write before a subsequent out-of-bounds load. The exception and the changed buffer were both observable. [Six C# cases](../experiments/0003-Buffer/README.md) |
| **0004 · Typed context** | Omitting one redundant type byte per Add shrank 64 wrapping Adds from 334 to 270 bytes. On a single checked Add, that byte paid for the nonnegative-result predicate. The original measured decode-and-execute ranges overlap. [Sizes and timings](../experiments/0004-TypedContext/evidence-v1/report.md) |
| **0005 · Guarded arithmetic** | Widening both intermediate checks and falling back to the original expression matched C# on 1,331 grid points and six named probes. Of 773 admitted grid cases, 110 still overflowed in the final sum. Lean proved result-or-overflow equivalence in its pure arithmetic model. [C# and Lean evidence](../experiments/0005-GuardedAddition/README.md) |
| **0006 · Native alternatives** | Starting from stored bytes, two operand orders × two contracts × two codecs yielded eight images and 56 reference matches. A separate control ensures fresh copies preserve buffer aliases across arguments. [Eight images and observations](../experiments/0006-Settlement/evidence-v1/result.json) |
| **0007 · Capacity** | V1 found contract break-even at one Add. V2 measured the count and reference-width transitions while preserving the one-byte-per-Add rule. V3 packed a checked run, winning at two Adds (22 → 18 bytes) and encoding 4,095 checked Adds plus Return in 21 bytes versus 20,364 for packed v1. The 61-byte mixed buffer fixture tied. Decoding expands the logical operations. [Versioned results](../experiments/0007-Capacity/README.md) |
| **0008 · Transfer** | V1 reproduced the checked counterexamples in native graphs. V2 dispatched in the host at fixed `k = 1`. V3 serialized both tests, targets and arms in 74 baseline or 70 packed bytes. Per codec 100 of 121 grid pairs took the transfer, including 20 admitted final overflows. [Selector and outcomes](../experiments/0008-Transfer/README.md) |
| **0009 · Effect order** | V1 isolated an aliased-cell return difference and a write visible only in the trace. V2 admitted 406 of 448 schedules under return, exception type, writes and buffer contents. V3 added the faulting operation and admitted 286. Of those, 224 stopped at arithmetic overflow before reaching memory. [Guards and counterexamples](../experiments/0009-EffectOrder/README.md) |
| **0010 · Input guards** | Roslyn independently lowered explicit conditionals and the resolved .NET helper to identical 39-byte images. On 36 pairs per form, the bound, check order, rejected parameter and actual value matched C#. The two guards and framing add 21 bytes to the 18-byte checked body. [Source bindings and controls](../experiments/0010-AddInputGuards/evidence-v1/result.json) |
| **0011 · Property mapping** | Four independent calls and four successive updates across two rows matched C#, including identity and partial writes. The 154-byte image spends 140 bytes on receiver and member schema and 14 on header and program. The route calls the real CLR accessors, so setter validation is supplied by the host type. [Bindings and row states](../experiments/0011-PropertyMapping/evidence-v1/result.json) |
| **0012 · Balance history** | A single 31-byte image (13 bytes for the traversal and framing plus 18 for checked Add) matched seven input cases up to 257 deltas. Duplicates, an existing list prefix and writes before overflow survived. A late-overflow witness refuted committing the whole batch as an equivalent implementation. The route-local traversal was hand-built. [Inputs and staged-commit counterexample](../experiments/0012-BalanceHistory/evidence-v1/result.json) |
| **0013 · Product settlement** | Reusing the practical images in one host-composed operation demonstrated a downstream consequence of a stored getter binding and preserved the distinct failure boundaries above. Nine additional arithmetic checks distinguish which selector arm actually executed and whether adjacent Adds retained their individual overflow policies. [Settlement record](../experiments/0013-ProductSettlement/evidence-v1/result.json) |

## Verification and review findings

The submitted lab work was checkpointed at `40ed20a682d73c9e269e6cb0243b5e91b5057529` before review. A fresh export on Windows x64, SDK 10.0.401 and .NET 10.0.12 reproduced **47 retained files across all 13 submitted versioned routes**. That includes all **13 files from the three practical labs**. The original corpus separately reproduced all 12 wire images, 62 C# comparisons and 16 malformed-image rejections. The original rewrite observations, guarded arithmetic grid and Lean 4.8.0 theorem also replayed.

Two earlier checks could pass despite an incorrect implementation. The new settlement retains discriminating observations. A checked Add followed by a wrapping Add from `int.MaxValue - 1`, incrementing by one twice, must return `int.MinValue`. For Transfer on `(int.MaxValue, 0)` the original returns `int.MaxValue` and the alternative overflows. Changing the stored branch targets, shift, or operand independently must produce the corresponding overflow in both codecs. The v3 routes now distinguish a failed decoder or control from evidence refuting a behavioral claim. [Executable review controls](../experiments/0013-ProductSettlement/v1/PriorControls.cs)

Six isolated changes checked the review itself. Always executing the original arm, hard-coding the selector guard, and merging Adds with different overflow policies each made the product settlement exit with `failed`. Independently breaking one control in each of the three older v3 harnesses also produced `failed`. Restoring the implementations reproduced all 19 v3 evidence files and the new settlement record byte for byte.

The evidence supports exact storage savings on the measured graphs, a theorem about the modeled pure arithmetic, and observed equivalence or distinction on the specified runtime cases. The practical formats have separate decoders and restricted vocabularies. A general C# compiler, a serialized orchestrator for the inventory workflow, and a runtime speed advantage remain open. The next independent labs test two-sided argument validation, optional property updates and importing a loop from C#. [Core wire specification](GRAPH-FORMAT-V1.md) · [Independent v2 handoff](PRACTICAL-LAB-HANDOFF.md#independent-v2-handoff)

## Replay the settlement

Run from the repository root with the .NET 10 SDK. The first command loads the retained component images and runs the composition and independent controls. The second regenerates the original timed corpus. Component generation commands are in the two lab handoffs. [Core labs](LAB-HANDOFF.md#v3-results-and-replay) · [Practical labs](PRACTICAL-LAB-HANDOFF.md#v1-results-and-replay)

```powershell
dotnet run -c Release --project experiments/0013-ProductSettlement/v1 -- artifacts/labs
dotnet run -c Release --project src/Weightless -- suite artifacts/thanksgiving
```
