# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | Independent review of Sol's NEXT-008R literal repair and NEXT-009 offline research recheck |
| State | PENDING_INDEPENDENT_REVIEW |
| Next writer | Gemini Argon Orchestrator |
| Current product author | GPT-6.2 Sol |
| Last independently accepted baseline | NEXT-007 product `2011be05e61a7c7107bce3496cb0bf072e262341`, decision `59bd350c35f17d6bc6ae29539afb2aa22c1cc909` |
| NEXT-008R provisional repair | Product `112f9fb071698b5d6b256184f3debda54dd9e305`, author evidence `3110a931b80ac15f9e14708c91cf68f4302f7d38`, `AUTHOR_VERIFIED_PENDING_INDEPENDENT_REVIEW` |
| NEXT-009 provisional feature | Product `f4db48313eefe6df27639d4a17c114cee6224d75`, author evidence `793ebbdf8e3dfc5cba8eb3957dbb616d884c8a5a`, `PENDING_INDEPENDENT_REVIEW` |
| NEXT-009 preregistration | `d260b5a47f1c010d0ae7015180259219d58ebdd6` |
| Independent decision on earlier Argon product | Sol reviewed `298db33c18f8f888f162569bf2b9bd44434a4a6b` and issued `REPAIRS_REQUIRED` at `b3f250a2ddeab87e4323a521f9ec796f3271fbeb`. [Independent receipt](../evidence/independent-review-next008r-followup.json) |
| Offline distribution | Four named ZIPs in `out/NEXT-008R-Sol` and `out/NEXT-009`. [Sizes, exact SHA-256 and inventories](../evidence/next-009/package-inventory.txt) |

## Argon phase A — Separate independent reviews

1. **Sol's NEXT-008R repair.** Check the exact frozen product and Sol-authored diff independently. Compare fresh and custody-held Python headers against CPython 3.12 syntax without executing source. Exercise raw versus plain text and bytes, recognized escapes, hex widths, Unicode bounds, unsupported named escapes, unqualified and arity-qualified definitions, parameter and return spans, and withholding through rejected parents. Replay the earlier literal, numeric, operator, continuation, scope and task integrity cases. Verify C# direct and contextual extension binding and matching-report-digest ledger negatives. Run the full clean Windows/Python public suites and both shell packages. Record a separate verdict on Sol's repair. Sol's prior independent reviews cover the earlier Argon-authored feature and fixes. [Repair author evidence](../TEST_EVIDENCE.md#sol-next-008r-author-verification)
2. **Sol's NEXT-009 feature.** Review the preregistration and frozen product separately. In a clean checkout run `dotnet build checks -c Release --configfile NuGet.Config`, the checks EXE, `python -B lab/test_integrity.py`, public factual and code replays, and the frozen shadow evaluations described in the test evidence. Run `python -B lab/research_recheck.py out/search.json` and `python -B lab/research_recheck_host.py out/recheck.json`. Verify the exact current values and claim changes independently. Challenge unavailable, foreign and substituted parents, reopened history, changed evidence after review, irrelevant changes, same-value new revisions, conflicting and expired sources, unsupported comparisons and project isolation. Compare conclusions, changed keys, citation paths, spans, digests, false abstentions and wrong deliveries. Verify both named packages and fresh builds under Windows PowerShell 5.1 and PowerShell 7.6.9 using `tools/package.ps1` and `tools/verify-package.ps1`. [Preregistered protocol](../../lab/research-recheck-protocol.md) · [NEXT-009 author evidence](../TEST_EVIDENCE.md#next-009-author-verification)
3. **Record independent results separately.** Include distinct commits and receipts for the two verdicts and update the canonical handoff and owner report. The provisional repair is a dependency for NEXT-009. Acceptance of NEXT-009 requires independent acceptance of that dependency. Keep evaluator inputs and expectations outside Git. Both native models stay in shadow mode, host execution stays disabled, and work remains offline and local.

## Argon phase B — After both candidates are accepted

Select and implement one useful small enhancement within the roadmap and current owner authority, with a measurable local outcome and separate product and evidence commits. Prioritize practical coding, offline research or independently evaluable native improvement where evidence qualifies. Return the candidate to Sol for independent review and continued primary development. If either review requires repairs, assign only the necessary work to Sol. Relay the next prompt through the owner. [Sol-led protocol](ATLAS_REVIEW_PROTOCOL.md) · [Roadmap](../ROADMAP.md) · [Owner report](ATLAS_OWNER_REPORT.md)
