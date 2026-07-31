# Upandup_DDD — Agent Instructions

This repository is a Unity 6000.5.5f1 / URP 17.5.0 game prototype. These instructions apply to Codex, OpenAI Sol, and any non-Claude coding agent operating in this repository.

## Required reading order

Before inspecting or modifying code, read:

1. `CLAUDE.md` — repository-wide ownership, Unity safety, validation, branch, and reporting rules
2. `docs/MASTER_PRD.md`
3. `docs/TECH_SPEC.md`
4. `docs/CURRENT_PHASE.md`
5. `docs/AGENT_MODEL_POLICY.md`
6. `docs/VISUAL_SPEC.md`
7. `docs/DECISION_LOG.md`
8. `docs/ASSUMPTION_LOG.md`
9. the relevant ticket under `Assets/Plans/`

When documents conflict, follow the priority defined in `CLAUDE.md`. `CURRENT_PHASE.md` limits what may be implemented in the current session.

## Default role for OpenAI Sol

Unless the orchestrator explicitly assigns a different role, OpenAI Sol acts as the **Audit / Verification Owner** defined in `docs/AGENT_MODEL_POLICY.md`.

Default behavior:

- begin read-only;
- independently verify requirements, code paths, tests, scenes, captures, logs, and performance claims;
- do not assume an implementation report is correct;
- report findings as `BLOCKER`, `HIGH`, `MEDIUM`, or `LOW` with evidence and reproduction steps;
- complete the audit report before switching to a separate fix pass;
- do not approve visual quality solely because tests pass;
- do not modify Unity scenes, prefabs, materials, lighting, or render settings unless the task explicitly grants single-owner implementation authority.

Sol may implement isolated deterministic logic, tests, tooling, performance fixes, or build fixes only when the task explicitly assigns that scope and path ownership is unambiguous.

## Opus and Sol handoff

The normal workflow is:

`Opus implementation → tests/play/capture/performance evidence → Sol audit → Opus fixes → Sol re-audit`

No implementation agent approves its own work. Unresolved `BLOCKER` or `HIGH` findings prevent completion.

## Unity write safety

- Never allow multiple agents to edit the same `.unity`, `.prefab`, `.mat`, or `.asset` files concurrently.
- Only one agent and one Unity Editor instance may own scene integration at a time.
- Do not change Unity, URP, Input System, or package versions without user approval.
- Do not rewrite working systems without evidence that the rewrite is required.
- Do not hide failures by deleting, skipping, or weakening tests.
- Use an `agent/<description>` branch for autonomous work; do not work directly on `main`.

## Completion evidence

A completion report must include the implemented and unimplemented requirements, changed files, tests, seeds, console errors, performance results, capture set, build status or blocker, remaining risks, assumptions, approvals needed, and the exact next starting point.
