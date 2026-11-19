# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-008 independent review of conversational code inspection, rich C#/Python parameter types, implicit-using call binding, and multi-turn real-source outcome ledger |
| State | PENDING_INDEPENDENT_REVIEW |
| Next writer | GPT-6.2 Sol Extra High |
| Product author | Gemini Argon Orchestrator |
| Frozen product | `80c09b1b928c68e08b05bf0770cbc3781f86a81f` |
| Author evidence | `b058e997fac26fe4c9be3f6300eed7431a4e39bf` |
| Preregistered public protocol | `8585ca05ef4f9632010b78d869fadef58bcdc45c` (with constructed control fix `3b03b1e152ebf688513f2006e316ddab0c3294a4`), committed before product code |
| Accepted baseline | NEXT-007 product `2011be05e61a7c7107bce3496cb0bf072e262341`, Argon's Phase A decision `59bd350c35f17d6bc6ae29539afb2aa22c1cc909` (`ACCEPTED_WITH_LIMITATIONS`) |
| Windows PowerShell 5.1 ZIP | `out/NEXT-008/ATLAS-Next-win-x64-ps51.zip`, SHA-256 `3d895c9d743bb1194ad0a3457f3493033f445f21f34fa10a58e6ea9cca386a8c` (`95,143,578` bytes) |
| PowerShell 7 ZIP | `out/NEXT-008/ATLAS-Next-win-x64.zip`, SHA-256 `05d6aed90d6857f3346887ff5d558ad758148d8aec2a3634d0cc2010ab86342f` (`112,794,966` bytes) |
| Model status | Desktop router `6b9b23fc…` advisory, project specialist `5c476024…` lab-only, both shadow and byte-identical to NEXT-007 |

## Sol Phase A — Independent NEXT-008 review

1. Verify Git history, preregistration chronology (`8585ca0` -> `3b03b1e` strictly preceding `80c09b1`), single-writer separation, and clean-clone reproduction of `80c09b1b928c68e08b05bf0770cbc3781f86a81f`. Rerun `dotnet build checks -c Release --configfile NuGet.Config`, `AtlasNext.Checks.exe` (`213/213`), `lab/test_integrity.py` (`12/12`), `lab/outcomes.py` (`31/31`), `lab/project_audit.py` (`10/10`), `lab/evaluate.py`, `OPENBLAS_NUM_THREADS=1 lab/project_specialist.py evaluate`, NEXT-005/006/007 host replays (`8/8`, `13/13`, `17/17`), `lab/code_conversation.py search`, `lab/code_conversation_host.py` + `--verify-only` (`14/14`, `6/6` real-source ledger + negative mutations), and both Windows PowerShell 5.1 and PowerShell 7.6.9 `tools/package.ps1` + `tools/verify-package.ps1` workflows against the shipped `out/NEXT-008/*.zip` packages and fresh clean builds. [Evidence](../TEST_EVIDENCE.md) · [Protocol](../../lab/code-conversation-protocol.md) · [Ledger](../evidence/next-008/eligible-outcomes.json)
2. Replay prior protected factual and code suites in evaluator custody without modifying or importing private labels into the development tree. Challenge NEXT-008 with fresh unfamiliar C# and Python sources covering generic/nullable/array overloads (`IReadOnlyList<T>`, `Guid?`, `T[]`), whitespace normalization, implicit-using cross-file calls vs shadowed/modifier/multi-dimensional exclusions, Python annotated/default positional arity vs keyword-only/`*args`/call-default withholding, multi-turn `What are its parameters?`, `What is its return type?`, and `Where is it called?` follow-ups, `ParentTaskId`/`Dependencies` tampering, secondary-file staleness, revision superseding, and project switches.
3. Commit a separate independent review receipt, owner report update, and canonical handoff locally on `main` (`ACCEPTED`, `ACCEPTED_WITH_LIMITATIONS`, or `REPAIRS_REQUIRED`). If repairs are required, assign bounded fixes to Argon and do NOT start NEXT-009.

## Sol Phase B — Upon independent acceptance, implement NEXT-009 in the same activation

After independently accepting NEXT-008, select the bounded NEXT-009 vertical slice grounded in `docs/ROADMAP.md`, owner priorities, and your review findings (for example: expanding Option C eligible real-source outcome qualification / offline specialist calibration over real coding/research outcomes, OR bounded local research contradiction/freshness synthesis & cross-file symbol navigation depth while keeping host execution disabled, network offline unless an isolated boundary is independently verified, and legacy ATLAS / PLATZ Engine / FORGE untouched). Preregister before code changes, freeze product separately from evidence, verify both PS 5.1 and PS 7.6.9 packages, hand back to Gemini Argon Orchestrator via `PENDING_INDEPENDENT_REVIEW`, and return a short copy-ready prompt.

[Reciprocal review protocol](ATLAS_REVIEW_PROTOCOL.md) · [Owner report](ATLAS_OWNER_REPORT.md) · [Argon's NEXT-007 decision](../evidence/independent-review-next007.json) · [Hydrogen's NEXT-006 decision](../evidence/independent-review-next006.json) · [Roadmap](../ROADMAP.md)
