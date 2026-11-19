# NEXT-021 Native Learning and Knowledge Foundry

Frozen product `7adf742aedc15997a8e81fc40240e64412a7fe59`. Preregistration `ea2bac830121f310ab4d5292275a2e380732ea17`. Verify the installed master with `python -B tools/check_master_plan.py`, build `dotnet build checks -c Release --configfile NuGet.Config`, then run `python -B lab/foundry.py acquire` in a fresh checkout. Use `python -B lab/foundry.py knowledge --project pycparser` to inspect the pinned facts and declaration evidence.

The public dataset contains 56 observed project queries, 28 separately marked authored safety controls and 504 actual actions across 14 distinct families. We measured a raw question, explicit source abstention and four typed source actions for each task. The author used local named distribution METADATA, RECORD, license files and Python declarations, with CPython AST as the code oracle. Source text excerpts are declaration projections with untouched header text apart from leading scope indentation and framing a stub body. The underlying library code stayed unexecuted. Train and acquisition examples have an explicit scoped grant. The protected partition remains external and inaccessible to the training path.

| Partition | Tasks | Families | Correct typed deliveries | Correct withhold observations | False abstentions | Incorrect counterfactual deliveries |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Train | 36 | 6 | 24 | 24 | 48 | 120 |
| Validation | 12 | 2 | 8 | 8 | 16 | 40 |
| Public evaluation | 24 | 4 | 16 | 16 | 32 | 80 |
| Acquisition | 12 | 2 | 8 | 8 | 16 | 40 |

The recorded incorrect counterfactuals came from deliberately asking a different typed question. They failed the original task oracle and never counted as successful answers to the original question. All 56 raw answerable questions withheld, including the new intake for reportlab and pdfplumber. Typed reads returned all 56 expected facts or declaration identifiers. The read-only knowledge index records the exact evidence, receipt digest, source revision and two witnessed procedure types. Capture produced no quarantined rows. Negative gate tests exercised quarantining duplicate and unenrolled tasks, request/oracle swapping, missing or changed sources, ineligibility after revocation, source text injection and overlap. The small corpus uses exact five-token Jaccard as a conservative substitute for approximate LSH, and compares request shingles plus source digests across splits. The results apply to these audit projections. [Frozen digests](foundry-manifest.json)

NR-016-1 now uses the same canonical family identity in acquisition and evaluator scoring. NR-020-1 now supports a paired `--master` and `--master-sha` bound to the previous specialist digest. A synthetic author-only external-path smoke executed the confidence, linear and original Master gates. No protected suite or report was accessed.
