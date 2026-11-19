# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-008R follow-up repair for Python declaration expression grammar |
| State | REPAIRS_REQUIRED |
| Next writer | Gemini Argon Orchestrator |
| Independent reviewer | GPT-6.2 Sol |
| Reviewed repair product | `9ff586699629678d3342f7f9be3eaf69ec175bf6` |
| Reviewed handoff and author evidence | `b9d3cdb04773267de983eec79ed26d0bfd633c4e` and `51cb0a66d241583e9029011a3fa8210107633dd7` |
| Frozen comparison | `8585ca05ef4f9632010b78d869fadef58bcdc45c` and pre-product correction `3b03b1e152ebf688513f2006e316ddab0c3294a4` |
| Last accepted baseline | NEXT-007 product `2011be05e61a7c7107bce3496cb0bf072e262341`, Argon's verdict `59bd350c35f17d6bc6ae29539afb2aa22c1cc909` (`ACCEPTED_WITH_LIMITATIONS`) |
| Next feature milestone | NEXT-009 deferred until independent acceptance |

## Argon assignment — Remaining NEXT-008R repair

**Fix Python declaration expression validation in `SourceDeclarations.cs`.** The prior lambda, continuation, parameter-ordering and reserved-name cases passed. Four fresh evaluator-held headers rejected by CPython's syntax parser still produced verified arity-qualified and unqualified definitions, plus verified parameters and return annotations. The expression scanner accepts malformed numeric tokens and operators illegal in expressions. The issue affects defaults and both parameter and return annotations. Faithful citation offsets do not establish that a declaration is syntactically valid. [Independent review receipt](../evidence/independent-review-next008r.json)

1. Establish syntax using a bounded, explicit grammar and token rules for the supported Python declaration subset, or rely on a non-executing offline syntax parser that honors the same scope. Withhold when syntax cannot be established. Treat a rejected declaration as ineligible to act as a selected parent. Preserve valid annotated/defaulted declarations and exact annotation spans, including supported continuations and lambda colons. Address the grammar classes across inputs.
2. Add positive and negative regressions through import, submit, and finish. Exercise invalid numeric literals and illegal operator forms in defaults and parameter and return annotations. Verify typed and unqualified definition withholding and withholding on both selected follow-ups. Maintain prior Python compatibility and the independently verified C# binding and ledger request checks.
3. Reproduce the clean Windows and Python suites, public NEXT-005/006/007/008 replays with the original oracles, and both PowerShell 5.1 and 7.6.9 packaged WPF workflows. Record any changed supported syntax, exact results, and ZIP digests. Preserve accepted NEXT-007 and the evidence of the two verified repairs. Evaluator sources and expectations stay outside Git. [Review commands and results](../TEST_EVIDENCE.md#next-008r-independent-review)

Freeze and commit the scoped repair and evidence separately. Set the canonical handoff to `PENDING_INDEPENDENT_REVIEW` for Sol with the exact revisions. After independent acceptance, Sol selects and implements bounded NEXT-009 in the same activation. Keep host execution disabled, network offline and native models shadow-only. Windows privileges, legacy ATLAS, PLATZ Engine and FORGE remain outside scope. Work stays local.

[Owner report](ATLAS_OWNER_REPORT.md) · [Reciprocal review protocol](ATLAS_REVIEW_PROTOCOL.md) · [Earlier NEXT-008 decision](../evidence/independent-review-next008.json)
