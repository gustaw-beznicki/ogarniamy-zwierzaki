# AI agent and 10xDevs workflow

This document contains the course-oriented guidance that supports the concise, project-specific rules in `@AGENTS.md`.

## Context chain

Use the following chain to onboard an agent after scaffolding the project:

```text
(/10x-init -> /10x-shape -> /10x-prd -> /10x-tech-stack-selector -> /10x-scaffold-adapter -> /10x-bootstrapper) -> /10x-agents-md -> /10x-rule-review -> /10x-lesson
```

Re-run an earlier stage only when its source artifact needs to change. The next stage should consume the artifact produced by the previous one.

## Skill routing

| Skill | Use it when |
| --- | --- |
| `/10x-agents-md` | The scaffold exists but the repository lacks concise, project-specific agent instructions. |
| `/10x-rule-review <path>` | An AI rules file needs a read-only review of length, embedded snippets, precision, redundancy, and ordering. |
| `/10x-lesson [seed]` | A recurring failure mode should be recorded in `context/foundation/lessons.md`. |
| `/10x-init` through `/10x-bootstrapper` | An earlier foundation, stack, adapter, or scaffold artifact genuinely needs to change. |

The canonical instructions for each skill live in its `SKILL.md` under `.agents/skills/`. Do not duplicate those instructions in `AGENTS.md`.

## What belongs in an AI rules file

Before adding a rule, ask whether an agent could infer it from public documentation, framework defaults, repository configuration, or `README.md`.

Keep:

- project-specific invariants and failure modes;
- non-obvious naming, layout, import, and workflow conventions;
- exact stop conditions for destructive or irreversible work;
- references to canonical repository files.

Do not copy:

- framework documentation or tutorials;
- configuration already enforced by type checkers, linters, or build tools;
- setup instructions already maintained in `README.md`;
- vague intentions such as “write clean code” or “follow best practices.”

Turn a vague intention into behavior that can be checked against a diff. Keep a rule only when it prevents a repeated failure or records a project decision that cannot be inferred from the code.

## Calibrating a new rule

1. Ask the agent to perform the relevant pattern several times without the proposed rule.
2. Record concrete convention violations.
3. Add a short rule at the narrowest applicable scope.
4. Repeat the task in a fresh session and compare the result.

If the agent already follows the convention reliably, the rule is unnecessary. Test structural changes one at a time so a behavior change can be attributed to a specific edit.

## Scope and hierarchy

Keep critical security, data-loss, irreversible-action, and stop/ask rules near the top of each rules file. Put subsystem-specific rules in a nested `AGENTS.md` beside the code they govern when the root file would otherwise accumulate local detail.

Use one canonical source for each rule. Tool-specific instruction files should be thin adapters rather than copies of the root rules. Local automatic memory is not a substitute for team rules committed to the repository.

## Hooks and procedural workflows

Use hooks only for deterministic checks backed by commands that exist in repository configuration. Keep multi-step procedures such as reviews, release checklists, and deployments in skills or dedicated documentation.

Do not configure a lint hook until the repository has a lint command. When such a command is added, reference the exact command instead of describing it as a “quick lint.”

## Foundation artifacts

- `@context/foundation/prd.md` — product requirements and guardrails
- `@context/foundation/tech-stack.md` — stack and deployment direction
- `@context/foundation/lessons.md` — append-only record of recurring lessons

Never edit `context/archive/`. Archived changes are immutable; open a new change instead.
