# Thanksgiving corpus results

Run from the repository root with the .NET 10 SDK.

```powershell
dotnet run -c Release --project src/Weightless -- suite artifacts/thanksgiving
```

Measured in Release with SDK 10.0.401, .NET 10.0.12, and Intel(R) Core(TM) 5 210H (12 logical processors). The source fingerprint and host settings are in `timings.json`.

A fresh process verified 12 fixture/codec pairs and 62 C# comparisons. The decoder and its re-encoding ran in that process using files written by the suite. See `verification.json` for the input vectors, output, effects, postcondition discriminators, and malformed wire cases.

| Fixture | Nodes | Baseline bytes | Packed bytes | Baseline bytes/node | Packed bytes/node | Graph, bytes, C# |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| add-checked | 2 | 19 | 18 | 9.50 | 9.00 | Pass |
| add-wrapping | 2 | 19 | 18 | 9.50 | 9.00 | Pass |
| add-constrained | 2 | 20 | 19 | 10.00 | 9.50 | Pass |
| hello-world | 3 | 31 | 31 | 10.33 | 10.33 | Pass |
| buffer-call | 8 | 62 | 61 | 7.75 | 7.62 | Pass |
| repeated-addition | 65 | 334 | 270 | 5.14 | 4.15 | Pass |

Packed/constrained Add (19 bytes) fits within baseline/plain Add (19 bytes). Each codec uses `00` for plain and `01 01` for the required nonnegative result. The same input `(-1, 0)` returns `-1` in the plain graph and produces `PostconditionViolationException` in the constrained graph.

Times are median nanoseconds per operation with [min, max] across 9 batches of 2000 operations after 3000 warm-up operations per row and stage. Execution captures values or exceptions, output, ordered effects, and final memory in both codecs. The harness prepares inputs outside each timed batch.

| Fixture | Codec | Encode | Decode + validate | Execute | Decode + execute |
| --- | --- | ---: | ---: | ---: | ---: |
| add-checked | Baseline | 155 [111, 826] | 298 [175, 1119] | 536 [481, 932] | 834 [697, 2159] |
| add-checked | Packed | 146 [134, 770] | 336 [172, 1023] | 591 [466, 1278] | 872 [711, 2095] |
| add-wrapping | Baseline | 256 [138, 758] | 208 [171, 696] | 189 [121, 999] | 414 [339, 2073] |
| add-wrapping | Packed | 142 [124, 465] | 218 [195, 1254] | 154 [114, 887] | 422 [332, 1699] |
| add-constrained | Baseline | 136 [112, 1383] | 209 [184, 1211] | 874 [829, 2286] | 1294 [1143, 2779] |
| add-constrained | Packed | 148 [125, 767] | 226 [189, 1591] | 944 [831, 1648] | 1209 [1104, 3088] |
| hello-world | Baseline | 181 [137, 746] | 275 [205, 931] | 249 [159, 562] | 477 [360, 1578] |
| hello-world | Packed | 158 [133, 244] | 221 [212, 894] | 170 [149, 644] | 429 [394, 1481] |
| buffer-call | Baseline | 890 [635, 5408] | 989 [912, 4003] | 2036 [1804, 3103] | 3378 [2944, 7998] |
| buffer-call | Packed | 787 [633, 1065] | 926 [813, 4339] | 2056 [1843, 3420] | 3108 [2892, 8122] |
| repeated-addition | Baseline | 2315 [2063, 11515] | 3625 [3147, 10937] | 597 [527, 2246] | 4104 [3783, 18445] |
| repeated-addition | Packed | 2170 [1876, 10578] | 3674 [3395, 14771] | 574 [466, 2218] | 4665 [4173, 19750] |
