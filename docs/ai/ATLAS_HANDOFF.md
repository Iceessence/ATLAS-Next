# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-008 conversational code inspection, rich C#/Python parameter types, and implicit-using cross-file call binding |
| State | IN_PROGRESS |
| Next writer | Gemini Argon Orchestrator |
| Product author | Gemini Argon Orchestrator (implementing NEXT-008 after accepting Sol's NEXT-007) |
| Accepted baseline | NEXT-007 product `2011be05e61a7c7107bce3496cb0bf072e262341`, independent review `docs/evidence/independent-review-next007.json` (`ACCEPTED_WITH_LIMITATIONS`) |
| Preregistered public protocol | To be frozen before NEXT-008 product code edits (`lab/code-conversation-protocol.md`, `lab/code_conversation.py`) |
| Windows PowerShell 5.1 ZIP | NEXT-007 baseline verified (`44db16f05d8c5b88734f5c2d5342f611bdecd4a75370929f97c2b82284228c35`); fresh NEXT-008 ZIP to be produced |
| PowerShell 7 ZIP | NEXT-007 baseline verified (`f88664f6b9a0119f9f7760f523f4aa10853c5602f69b313dbe4b3775902265b0`); fresh NEXT-008 ZIP to be produced |
| Model status | Desktop router `6b9b23fc…` advisory, project specialist `5c476024…` lab-only, both shadow and byte-identical to NEXT-007 |

## Argon Phase B — NEXT-008 Implementation Scope (Preregister before code edits)

1. **Implicit global usings for C# cross-file call binding:** Include standard .NET implicit global usings (`System`, `System.Collections.Generic`, `System.IO`, `System.Linq`) in `ProjectQuestions.ReferenceContext` so imported project files without explicit file-top `using` directives bind accurately across files while preserving Roslyn safety boundaries.
2. **Rich C# & Python parameter selectors:** Support generic (`Name<T1,T2>`), nullable (`T?`), and 1D array (`T[]`) C# parameter types in `ParameterFilter` and `SourceDeclarations.MethodParameters`, and extend Python positional parameter counting to handle optional `: Type` annotations and simple defaults (`None`, `True`, `False`, numbers, single-line strings, `()`, `[]`), while keeping keyword-only (`*`), variadics (`*args`/`**kwargs`), and Python redefinitions withholding cleanly.
3. **Conversational follow-ups on selected completed definitions:** Add cited follow-ups for `What are its parameters?` (`code.parameters`), `What is its return type?` (`code.return`), and contextual `Where is it called?` / `Show its callers.` (`code.calls` anchored via `ParentTaskId` and `Dependencies` with active-revision and project isolation checks).
4. **Validation & Handback:** Freeze preregistered protocol and ordinary-search baseline before product changes, pass all existing regressions (`185` Windows checks + `30+` new checks, `12` integrity tests, `31` public controls, NEXT-005/006/007 replays, NEXT-008 multi-turn comparator & real-source outcome ledger), verify both PowerShell 5.1 and PowerShell 7.6.9 WPF packages cleanly, freeze product separately from evidence, and transition handoff to `PENDING_INDEPENDENT_REVIEW` for GPT-6.2 Sol Extra High.

[Owner report](ATLAS_OWNER_REPORT.md) · [Independent NEXT-007 receipt](../evidence/independent-review-next007.json) · [Reciprocal review protocol](ATLAS_REVIEW_PROTOCOL.md) · [Roadmap](../ROADMAP.md)
