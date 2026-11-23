# Capacity lab

Measure how much enforceable meaning fits in the original wire budget. The shared parent is commit `65fe4b9` and its pinned checked Add graph. Your lab owns `experiments/0007-Capacity/`. [Common handoff and publishing protocol](../../docs/LAB-HANDOFF.md)

V1 places the break-even point precisely. With no Add, packed/constrained takes 15 bytes against 14 for baseline/plain. With one Add the sizes tie at 19. With two Adds they are 23 and 24. In each case `(-1, 0)` returns a negative result on the plain graph and fails the required predicate on the constrained graph. [V1 graph builder](v1/AddChain.cs) · [V1 evidence](evidence-v1/result.json)

**V2 assignment.** Test whether `packed/constrained − baseline/plain = 1 − AddCount` still holds when counts and slot references first require two bytes. Use the actual v1 codec. Include the zero-Add control, the original 64-Add size regime and consecutive chain lengths on both sides of slot 128. Identify which encoded count or reference first widens. Record all four standalone sizes, including framing, for every length. Decode each form, re-encode it and compare its observations with a checked C# loop, including a negative result, a normal result and an arithmetic overflow where an Add executes.

A wrong delta, a lost negative-result distinction, a mismatched overflow, or a graph or byte round-trip mismatch defeats the claim on that fixture. Publish the first failing count and input if one appears. Keep the graph semantics and both v1 wire formats fixed while measuring this question. A later lab version can try a different encoding and charge its full decoding metadata.

```powershell
dotnet run -c Release --project experiments/0007-Capacity/v1 -- artifacts/labs
dotnet run -c Release --project experiments/0007-Capacity/v2 -- artifacts/labs
```

## V2 results

The byte budget held at all 12 sampled chain lengths, through the largest v1 function of 4,095 Adds and one Return. Each of the 48 wire images reconstructed the source graph and re-encoded identically. Six inputs per image gave 288 full observation matches with the checked C# loop. On the zero-Add control, an input such as `(int.MaxValue, 1)` returned its first argument. The same input overflowed once the chain executed an Add. [Experiment source](v2/Program.cs) · [Measurements and inputs](evidence-v2/result.json)

| Adds | Baseline plain | Packed plain | Baseline constrained | Packed constrained | First field to reach 128 at this length |
| ---: | ---: | ---: | ---: | ---: | --- |
| 125 | 639 | 514 | 640 | 515 | All fields still fit in one byte |
| 126 | 645 | 519 | 646 | 520 | Return stores the result reference plus one |
| 127 | 651 | 524 | 652 | 525 | The instruction count includes the final Return |
| 128 | 657 | 529 | 658 | 530 | The last Add reads accumulator slot 128 |

Each of those three fields grew from `7F` to `80 01` on its own step. Both codecs pay these width increases. At 128 Adds the packed graph carried the enforced contract in 530 bytes against 657 bytes for unrestricted baseline, recovering 127 bytes after paying for the predicate. The evidence includes the measured field bytes for each length. [Full size table](evidence-v2/result.json)

**Next experiment.** A v3 route could encode an entire run of identical checked Adds in a single record. Measure the first run length at which the new form beats packed v1 after charging the wire header, run count, operand references and escape for literal instructions. Require reconstruction of the same graph, including its contract and checked overflow behavior. A mixed-operation control should reveal the cost when a run cannot form.

## V3 experiment

A record for two or more consecutive checked Adds with the same right operand and the previous result as the next left operand should win at two Adds. With one-byte operands, a run should cost four bytes including its tag and count, while two ordinary Adds cost eight. Ordinary records keep their packed v1 layout. A shorter winning length, a missing saving at two, an incorrect expansion, or a changed C# observation defeats the prediction.

```powershell
dotnet run -c Release --project experiments/0007-Capacity/v3 -- artifacts/labs
```

**Tested.** The first saving was at **two Adds**, from **22 to 18 bytes** for the complete plain wire. The constrained form went from **23 to 19 bytes**. Zero and one Add tied packed v1. The measurements include the version header, contract, function signature, logical instruction count, records and Return.

| Checked Adds | Packed v1 plain | Run codec plain | Packed v1 constrained | Run codec constrained |
| ---: | ---: | ---: | ---: | ---: |
| 0 | 14 | 14 | 15 | 15 |
| 1 | 18 | 18 | 19 | 19 |
| 2 | 22 | 18 | 23 | 19 |
| 64 | 270 | 18 | 271 | 19 |
| 128 | 529 | 21 | 530 | 22 |
| 4,095 | 20,364 | 21 | 20,365 | 22 |

All 338 observations matched compiled C# across both encodings, including checked overflow, a negative postcondition failure and a store that survived a later bounds failure. The mixed buffer/call graph stayed at **61 bytes** because it has only one Add. A second mixed graph with a literal separating two eligible runs shrank from **36 to 28 bytes**. Each decoded graph reconstructed its own source and both encodings re-encoded byte for byte. Eight malformed-image probes were rejected. [Measurements and failure probes](evidence-v3/result.json) · [Saved wires](evidence-v3/)

The route uses header `57 4C 50 02`. The new opcode `09` stores the run count, first left slot and shared right slot as shortest unsigned base-128 integers. The two-Add record is `09 02 00 01`. The function count, logical instruction count and references retain their packed v1 meanings. Ordinary instructions keep their original bytes. The decoder expands the run into checked Adds before invoking the shared v1 parser and graph validator, and checks that the resulting graph reproduces the version 2 byte stream. [Codec](v3/RunPackedCodec.cs)
