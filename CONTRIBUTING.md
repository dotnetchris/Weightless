# Contributing experiments

External contributions to Weightless are **additive experiments submitted by pull request**. Work on a branch in your fork or a topic branch you can push. Open a PR targeting `main`. Direct pushes to `main` are outside the external contribution workflow.

Start with one question, a public baseline, and an observation that could prove your prediction wrong. The existing briefs are starting points. New questions and counterexamples are welcome. [Research goals](GOALS.md) · [Agent quickstart](AGENTS.md) · [Independent lab briefs](docs/PRACTICAL-LAB-HANDOFF.md#independent-v2-handoff)

## Give the proposal its own experiment

- For a variation on an existing lab, add a sibling version such as `v2/` with its own `evidence-v2/`. For a different question, add a new directory under `experiments/`. Put the experiment note and replay command in the new files.
- Reuse the parent code and retained inputs as the control. Keep the proposed implementation in the new experiment. Preserve existing source, contracts, lab versions, and evidence so another person can replay both sides from the same checkout.
- Offer improvements you discover. For example, the property-mapping image spends 140 of its 154 bytes on schema. A proposal to share that schema could add a sibling encoder, charge the shared bytes once across several maps, and replay the rejected second setter to check the partial write. The existing 154-byte image remains the control. If the improvement is still an idea, offer the hypothesis and its deciding case in the PR as a follow-up experiment. [Mapping baseline](experiments/0011-PropertyMapping/README.md#v1-results)

## What a reviewer needs

In the PR, identify the parent revision or route, state what the caller can observe, and give the command to run from the repository root. Include the relevant saved program or counterexample and the expected and actual observations. [Existing replay and evidence conventions](docs/LAB-HANDOFF.md#publishing-a-sibling-version)

For experiments that encode a program, **report the change in complete encoded size alongside the behavior verdict** in the retained result and the experiment summary. Name the baseline and candidate, execute both under the same caller contract, and report both file sizes, bytes saved (`baseline − candidate`), percentage saved relative to baseline, and whether the candidate shrank, tied, or grew. Include framing and bindings, declare the common host, and charge any added shared vocabulary or decoder extension once per collection. Count each deployed program once. Changed-behavior controls have their own results. If a reference only supplies expected behavior, add a replayable second encoding to establish the size comparison. [Practical image comparison](experiments/0014-PracticalImageSize/README.md)

Report **tested** for agreement on the stated inputs, **refuted** for a concrete counterexample, and **failed** when the harness or a control prevents a verdict. For a proof, name the model and its assumptions. An experiment that grows the image or exposes an incorrect optimization can answer the question it set out to test.
