# Weightless graph wire formats, version 1

One file contains one graph. `WLB` is the binary baseline and `WLP` is the experimental typed-context codec. Each has a distinct header and the same graph model.

## File and function layout

Fields are consecutive with no alignment or padding. Single-byte tags use the unsigned values below. Counts, indices, references, and string byte lengths use the shortest unsigned base-128 representation. Each byte carries seven bits with the least significant group first. Bit 7 is set on every byte except the last.

| Order | Field | Encoding |
| --- | --- | --- |
| 1 | Format and version | Baseline `57 4C 42 01` or packed `57 4C 50 01` |
| 2 | Entry postcondition | Discriminator `00` for unrestricted or `01` followed by predicate `01` for required `result >= 0` |
| 3 | Function count | 1–64 |
| 4 | Functions | Exactly the declared count, in ID order |

A function starts with its single-byte return type, followed by the parameter count (0–32), that many single-byte parameter types, the instruction count (1–4096), and that many instructions. Function IDs are zero-based and function 0 is the entry point. A mandatory result postcondition requires an `Int32` entry return.

| Type tag | Meaning |
| --- | --- |
| `00` | Void |
| `01` | Signed `Int32` |
| `02` | Boolean |
| `03` | Non-null Unicode text |
| `04` | Non-null mutable, fixed-length `Int32` buffer |

An instruction has a one-byte opcode, its result type where present, then its payload. The baseline writes the result type on every instruction. The packed codec writes it on every opcode except Add.

| Opcode | Instruction | Result type | Payload in order |
| --- | --- | --- | --- |
| `01` | Literal | Explicit `Int32`, Boolean, or text | `Int32` is four bytes in little-endian two's complement. Boolean is `00` or `01`. Text has a byte count of 0–65,535 followed by that many bytes of strict UTF-8. |
| `02` | Add | `Int32` | Overflow byte (`00` wrapping, `01` checked), left reference, right reference |
| `03` | Branch | Void | Boolean condition reference, true target, false target |
| `04` | Call | Explicit callee return type | Function ID, argument count, argument references in parameter order |
| `05` | Load | `Int32` | Buffer reference, index reference |
| `06` | Store | Void | Buffer reference, index reference, `Int32` value reference |
| `07` | WriteLine | Void | Text reference |
| `08` | Return | Void | `0` for a void return, otherwise the returned value reference plus 1 |

Parameter slots use their zero-based parameter positions. Instruction `i` defines slot `parameterCount + i` if it produces a value. Branch targets use zero-based instruction positions. Effects and instructions execute in graph order along the chosen branch, with calls evaluated to completion at the call site. Operands must already be defined on every incoming path. Void slots cannot be referenced. Branches go forward, calls target later function IDs, every instruction must be reachable from its function entry, and all paths must return. V1 rejects loops, recursion, invalid references, type mismatches, unknown tags, overlong integers, truncation, and trailing bytes.

## Packed inference rule

The packed Add decoder reads the overflow byte and two references, looks up the operand types already decoded in this function, and accepts the implicit result tag `01` exactly when both operands have type `01`. Full graph validation then checks availability along every control path. Literals and calls retain their result tags because their types are not determined by the opcode and operand types. All other instructions retain the baseline form.

Each well-typed Add saves one byte. Both headers occupy four bytes. There are no context tables, escape markers, dictionaries, or padding. The complete contract discriminator and predicate are charged to both formats.

## Observations

Checked Add reports `OverflowException` for a mathematical sum outside `Int32`. Wrapping Add reduces the sum modulo 2³² and interprets the result as signed. Load and Store index existing buffers using CLR array bounds. An invalid index reports `IndexOutOfRangeException`. A successful store records the buffer identity, index and value. Earlier writes remain observable when a later instruction fails. Buffer identities follow first appearance among the entry arguments, including aliases. WriteLine emits the literal text followed by the runtime's newline as one stdout write.

The interpreter enforces the entry postcondition after a normal return. A negative result under predicate `01` reports `PostconditionViolationException`. The observable outcome includes the return value or exception type, stdout, ordered effects, and final buffer contents.
