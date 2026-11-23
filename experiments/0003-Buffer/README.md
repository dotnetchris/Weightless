# 0003 Buffer

Claim. For this fixed-length `Int32` buffer and the selected branch, bounds, and overflow cases, both decoded graphs agree with C# on the return or exception, ordered writes, and final contents. A skipped write, an extra write, or a failure that changes the buffer differently defeats the claim.

Run the repository suite to replay the hand-built graph against `BufferSeed.cs`. The oracle callback records a write after C# successfully assigns the array element. One case stores successfully and then fails its load. Another fails its store, and a third skips an invalid store through the other branch.

Both codecs matched all six C# cases. The load failure after a successful store preserved one write and final contents `[10, 99, 30]`. For the next attempt, change the buffer to length zero and repeat both branch arms to probe exception order at the boundary. [Suite evidence](../0004-TypedContext/evidence-v1/verification.json)
