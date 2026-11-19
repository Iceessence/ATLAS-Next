# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-008R bounded repairs to C# call binding, Python declaration spans, and outcome ledger request integrity |
| State | REPAIRS_REQUIRED |
| Next writer | Gemini Argon Orchestrator |
| Independent reviewer | GPT-6.2 Sol |
| Reviewed product | `80c09b1b928c68e08b05bf0770cbc3781f86a81f` |
| Reviewed handoff and evidence | `be832027391883313c93d5d3e3bb26e6364aa430` and `b058e997fac26fe4c9be3f6300eed7431a4e39bf` |
| Frozen comparison | `8585ca05ef4f9632010b78d869fadef58bcdc45c` and pre-product correction `3b03b1e152ebf688513f2006e316ddab0c3294a4` |
| Last accepted baseline | NEXT-007 product `2011be05e61a7c7107bce3496cb0bf072e262341`, Argon's verdict `59bd350c35f17d6bc6ae29539afb2aa22c1cc909` (`ACCEPTED_WITH_LIMITATIONS`) |
| Next feature milestone | NEXT-009 deferred until independent acceptance |

## Argon repair assignment

1. **C# call binding.** The frozen review independently compiled a valid SDK project with implicit usings disabled. The injected `System.Linq` import made a different extension method win, changed the outer call's argument type, and caused ATLAS to cite the call under the wrong overload. Require an explicit or authorized source for compilation assumptions, or withhold a verified relationship when implicit-import context changes the binding. Cover both explicit and selected-definition call queries. Add an enabled/disabled differential regression with competing extension methods, and preserve positive calls whose declaration and binding are established by source evidence. Document any new withholds in the original public replay.
2. **Python declarations and annotation spans.** Independent AST comparison found a valid return annotation truncated at an inner colon and a continued annotation whose delivered span included the leading continuation. Two malformed parameter lists also completed arity lookups and follow-ups. Make supported declarations and their spans grammar-aware within the existing offline subset. Withhold when the exact annotation or a valid declaration cannot be established. Add regressions for a valid return expression containing a top-level colon, explicit line continuations, a required positional parameter following a default, reserved keywords as parameter names, and supported annotated/defaulted declarations. An invalid declaration must not authorize a selected follow-up. Keep imported code as text.
3. **Ledger request integrity.** The validator accepted a report and ledger whose matching content and digest hid an unrelated delivered task request. Check each actual receipt request against the exact preregistered turn and validate its operation and parent/dependency linkage. Bind summaries to the verified turns. Add negative checks for altered and swapped turn requests with a matching report digest. Retain existing eligibility and source provenance checks.

## Verification and return

Work from the frozen NEXT-008 product for repairs. Preserve the preregistered comparator, historical verdicts and accepted NEXT-007 regressions. Keep scope to the three blocking changes and their regression tests. Run the same clean Windows build, Windows/Python tests, frozen model evaluations, NEXT-005/006/007/008 public replays and both PowerShell package and real WPF verifications from clean checkouts. Record exact results and any changed capability boundaries. Prior private custody manifests were unavailable to Sol in this review. Leave any newly available historical suite access with the independent evaluator and outside the worktree.

Commit product and evidence separately. When ready, set the handoff to `PENDING_INDEPENDENT_REVIEW` for Sol with the exact repaired revision, evidence revision, and ZIP digests. Sol will replay the evaluator-held cases independently. If accepted, Sol selects and implements bounded NEXT-009 in that activation. Use the owner-relay protocol. Keep host execution disabled, network offline, native models shadow-only, and Windows privileges, legacy ATLAS, PLATZ Engine and FORGE outside the work. Work stays local.

[Independent NEXT-008 review](../evidence/independent-review-next008.json) · [Owner report](ATLAS_OWNER_REPORT.md) · [Reciprocal review protocol](ATLAS_REVIEW_PROTOCOL.md) · [Argon's accepted NEXT-007 decision](../evidence/independent-review-next007.json) · [Roadmap](../ROADMAP.md)
