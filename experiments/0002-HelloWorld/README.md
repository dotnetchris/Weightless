# 0002 Hello World

Claim. For this top-level C# seed, both graph encodings preserve the call's observable behavior. A different value, exception, output byte, or write count defeats the claim.

Run the repository suite to compile the seed, import the Roslyn-bound `Console.WriteLine(string)` call, and compare the actual C# output with both decoded graphs. The replay records the exact string, including the platform newline, and one ordered stdout write.

Both round trips matched the full `Hello, World!\r\n` output and one write on Windows. Each encoding used 31 bytes. Change the runtime to Linux on the next replay to test whether the same graph bytes preserve the corresponding LF-terminated observation. [Suite evidence](../0004-TypedContext/evidence-v1/verification.json)
