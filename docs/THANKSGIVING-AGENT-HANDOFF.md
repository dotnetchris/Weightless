# Weightless Thanksgiving research sprint
**Initial agent handoff · November 23–29, 2026**

By Sunday, November 29, leave a small C#/.NET solution that encodes and decodes the same typed program graph two ways, runs both decoded graphs, and reports what the packed encoding buys and costs. An experimental encoding that grows every fixture can meet the sprint goal. The deliverable in that case is a reproducible measurement and an explanation of where the bytes went.

## Pick up here

The checkout began with a README and three research documents. This handoff adds a runnable C# reference experiment and its [recorded observations](../experiments/0001-Addition/checked-transfer-v1.json). The native graph, codecs, interpreter and benchmark are still open. Run the experiment from the Weightless repository root with the .NET 10 SDK.

```powershell
dotnet run --file ./experiments/0001-Addition/CheckRewrite.cs
```

**One claim is already settled.** For pure inputs, can checked C# `a + b` always become `(a + k) + (b - k)`? On .NET 10.0.12, with `a = int.MaxValue, b = 0, k = 1`, the original returned `2147483647`. The candidate threw `OverflowException` at `a + k`. A second witness, `a = 0, b = int.MinValue, k = 1`, broke the other intermediate. Under wrapping arithmetic, the two expressions agreed on all four recorded inputs. That last observation covers four inputs. The two checked witnesses refute the universal checked rewrite. [Experiment source](../experiments/0001-Addition/CheckRewrite.cs)

**First implementation session.** Give `Add(int a, int b)` two typed parameters, an addition node with an explicit overflow policy, and a return. Write the baseline bytes to disk. Start a second process, decode the file, execute it, and compare its result or exception type with the C# observations above. Save the fixture, bytes, exact command and comparison even if the first run disagrees. That slice gives the next agent something small enough to break.

## One model and two encodings

```text
Computational intent
        |
Canonical typed graph G, including its contract
        |                              |
   baseline encoder               packed encoder
        |                              |
   baseline bytes                 packed bytes
        |                              |
   baseline decoder               packed decoder
        |                              |
        +------ equal to G ------------+
                       |
             shared .NET interpreter
                       |
          value or exception, output, memory effects
```

The canonical model fixes the meaning of **one** program. Distinct graphs may implement equivalent programs under a stated contract. Keep those graphs distinct during codec tests. Substituting an equivalent graph belongs in a separate rewrite experiment. Both decoders must reconstruct typed operands, branch targets, evaluation order, overflow behavior, effects and any extra contract fields. Native graph IDs survive without Roslyn objects or encoder-local state.

| Component | Sprint responsibility |
| --- | --- |
| Roslyn | Extract the bound operation, types, conversions, checked context and call target from the C# seeds. Save source provenance. Hand-built fixtures can exercise the codec before the importer exists. |
| Weightless | Own the graph and its validation rules. Generate alternative computational structures as separately identified candidates. |
| Lean 4 | Kernel-check a scoped equivalence when a model is available. Save the theorem, assumptions, toolchain and successful checker output. The supplied Lean example uses mathematical integers. A C# claim needs a matching overflow and observation model. |
| Execution harness | Run the C# reference and the shared interpreter on identical inputs. Capture returns, exceptions and ordered effects. Time encoding, decoding and execution separately. |
| Semantic cube | Begin with a versioned file of candidate and relationship records. For each rule store the graphs, contract, evidence path and status such as proposed, tested, proved, refuted or superseded. A proof timeout leaves the claim open. |

## Four milestones

### 1. Define the graph and establish the oracle

Cover constants, arithmetic, conditional branches, function parameters and calls, collections, and basic memory operations. Use fixed-length integer buffers for the first memory model, with indexed load/store, explicit bounds behavior and observable writes. Construct fixtures in C#. A parser and surface syntax can wait.

Keep Generation Zero's two fixtures. **0001 Addition** exercises operand binding and checked versus wrapping arithmetic. **0002 Hello World** uses the actual top-level C# `Console.WriteLine("Hello, World!");`. Capture standard output including its newline and check exactly one write in the interpreter's effect trace. Add a third fixture with a helper that conditionally stores to a buffer and returns a loaded element. Exercise both branch arms, a bounds failure and the state of the buffer after failure. Save independent C# expectations for each.

### 2. Test whether packing transfers

The supplied material describes LittleBits reclaiming space in redundant numerical matrices. The Weightless checkout contains no implementation, executable vectors or primitive specification. If the owner supplies a repository and revision, extract the primitive and replay an original encode/decode vector before adapting it. Record what context the decoder needs and how it obtains it. Until then use the label **experimental typed-context codec**. Its results measure redundancy in typed graphs. Transfer of the original primitive remains open.

Use a compact, fully specified binary baseline with explicit tags and operand references. For the first packed format, omit a result-type tag only where the opcode and already decoded operand types uniquely determine it. Keep an explicit form for ambiguous cases. Freeze the rule before timing. Test repeated arithmetic, mixed operations and small graphs where framing may exceed any savings. Charge version headers, context tables, escape markers, padding and any per-program metadata to the encoding that needs them. If a dictionary is shared, report standalone size and its amortized cost with the reuse count.

### 3. Spend recovered bytes on a distinguishable contract

Use two otherwise identical `Add` graphs. Give one a mandatory postcondition, `result >= 0`. Leave the other unrestricted. For `Add(-1, 0)`, the unrestricted graph returns `-1` and the constrained graph must report a postcondition violation. A positive result passes both. Implement the contract once in the shared interpreter.

Encode both graphs with both codecs. For each codec show that decoding recovers each field and that re-encoding yields identical bytes in a fresh process. For this one-bit contract, exhaust both states, show the stored discriminator and the decoder's inverse for each, and reject an invalid or truncated form. A decoder that silently drops the postcondition fails even if all positive samples pass.

Publish four byte counts covering baseline/plain, packed/plain, baseline/constrained and packed/constrained. Compare codecs on equal contracts. **Does packed/constrained fit within baseline/plain on this fixture, including overhead?** The two contract values and their observable difference are the expressiveness result. The byte counts decide whether reclaimed space paid for the distinction.

### 4. Execute and benchmark

Use the same interpreter for graphs decoded by either codec. For each fixture, publish graph equality, deterministic bytes, C# versus interpreter observations, standalone bytes and bytes per node. Time decode/validation, execution of an already decoded graph, and decode-plus-execute. Record encode time separately. Compare the same program and contract on every timing row.

Run Release builds with a fixed corpus and seed. Record revision, SDK/runtime, machine, warm-up, iteration counts and raw timings from repeated batches. Consume results so the work is observable. Report medians and spread alongside the samples. A size gain with a decode penalty is a valid finding. An execution-time difference between identical decoded graphs warrants checking benchmark noise and harness asymmetry before attributing it to packing.

## How each agent closes a session

Place each experiment under `experiments/<id>-<name>/`. Before running it, write one sentence stating the claim, the conditions where it should hold, and the observation that would defeat it. Close the session with three items.

- **Something to replay.** Source or fixture, contract, exact command, relevant versions and the raw output or failing input. Store essential evidence alongside the fixture because the repository ignores `artifacts/`.
- **A verdict with a boundary.** Say what was observed, what it settles and what remains open. Distinguish a sample agreement, a checked proof and a concrete refutation. Keep earlier records when a later generation revises a claim.
- **One next attempt to disprove it.** Name the parent experiment and change one condition. For the checked-add witness, try the guarded rewrite only when both widened intermediate values fit in `Int32`. Attack the guard near both limits, including cases where the final sum overflows. Keep original evaluation order and pure operands for this claim.

If a toolchain or importer blocks a session, save the exact failing command and error, then advance an independent fixture, decoder test or counterexample. Sunday’s handoff should let a fresh agent run one command from a clean checkout, regenerate both encodings and the comparison table, and locate every disagreement. Keep the table explicit about any missing measurement. A wider generator, a custom language or a self-generating cube can follow an experiment that justifies it.

## Source notes

- [Project Weightless](Project%20Weightless.docx) supplies the numerical-packing analogy and the distinction between representation, meaning, execution and observation. Its capacity graphic is conceptual.
- [LEAN Lang](LEAN%20Lang.docx) proposes verifying arithmetic relationships over mathematical integers. The proof script is identified as illustrative. Large candidate searches are a later option.
- [Agent retrospective, November 20](Weightless_Agent_Retrospective_2026-11-20.pdf), especially §§4–6 and 9–10, supplies the Generation Zero fixtures, observable-effect contracts and the practice of retaining counterexamples.
- The supplied architecture sketch fixes the fork and rejoin in the diagram above. The sprint brief sets the four milestones and Sunday acceptance target.
