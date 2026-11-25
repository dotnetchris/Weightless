# Working on Weightless

Weightless asks how much of a program's implementation can vary while its promises to the caller still hold. A smaller image matters only if the decoded program keeps those promises. An input that breaks a proposed equivalence is a research result worth keeping. The next milestone is tenfold compaction on programs held out until after the vocabulary is frozen, counting the cost of the vocabulary itself. [Research goals](GOALS.md)

## Start from a clone

From the repository root with the .NET 10 SDK, replay the inventory workflow. This exercises three saved programs together and compares their results, faults, and caller-owned state with C#.

```sh
dotnet run -c Release --project experiments/0013-ProductSettlement/v1 -- artifacts/labs
```

Expect `product-settlement/v1 tested (checks passed)`. Compare the generated `artifacts/labs/0006-v1/product-settlement/v1/result.json` with the retained record. In the `append-delta` case, both versions return 11 and the caller sees different lists. [Retained record](experiments/0013-ProductSettlement/evidence-v1/result.json) · [What each experiment established](docs/PRODUCT-SETTLEMENT-2026-11-24.md)

For an open investigation, the practical handoff gives independent starting points for two-sided guards, optional property updates, and importing a balance-history loop. Pick the brief that fits the task. Each has a parent version and a case that can defeat its prediction. External contributions add experiments through pull requests. Preserve earlier work and offer improvements as new comparisons. [Lab briefs](docs/PRACTICAL-LAB-HANDOFF.md#independent-v2-handoff) · [Contribution workflow](CONTRIBUTING.md)

## Session context and privacy

The clone contains enough public context to do the work. When choosing between a first-run introduction and a shorter continuation, an agent may query one optional repository-local Git preference.

```sh
git config --local --get weightless.continuity '^established$'
```

An `established` result means the operator marked this local repository as familiar. Start with the current task and its evidence. An absent setting (exit 1) is normal for a fresh clone. Offer the public starting point above when relevant. An operator can opt in with `git config --local weightless.continuity established`. Linked worktrees of one local repository share this preference.

Anyone can set the preference. It conveys no identity, repository permission, or exception to the pull-request workflow. Use the host's authenticated permissions for privileged actions. Avoid inferring identity from Git authors, remotes, machine paths, or details someone could learn by cloning the project.

Use additional context only when the operator explicitly supplies it for this session. Keep private briefs outside the checkout. Public evidence and PRs should use repository-relative paths, public revision or image hashes, and reproducible inputs. If a private observation suggests an experiment, construct a public example that tests the same claim.
