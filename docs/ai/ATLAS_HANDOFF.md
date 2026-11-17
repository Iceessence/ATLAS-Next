# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-001 — independent Windows foundation and native policy review |
| State | PENDING_INDEPENDENT_REVIEW |
| Author | GPT-6.2 Astra |
| Next writer | Gemini Hydrogen, or another independent auditor explicitly designated by the owner. Only the designated reviewer writes this handoff during review. |
| New repo | C:\ATLAS\ATLAS-Next, standalone main, no legacy Git parent |
| Empty baseline | 81866215b829ec1a7c016971d93e3cf7f7ed20f1 |
| Training source | d14e993720b65b3a5db882b686c0d918634debe4 |
| Product and test candidate | **7d319bc33be94c747f15fbc6f52d873379b51282**. Later documentation/evidence commit identifies this product without changing its code. |
| Source | desktop/ host, window, registry pins, CPU inference, journal and broker. assets/ released weights and file records. lab/ experiments, datasets and integrity tests. checks/ behavioral and parity checks. tools/ portable bundling, UIA and read-only guest inspection. |
| Author evidence | 21 C# checks and 3 Python tests passed, reproducible native checkpoint, actual package/receipt, forced-restart and negative evidence. [Commands and receipts](../TEST_EVIDENCE.md) |
| Approval | Initial standalone development authorized. Native operational promotion, production release, secret-evaluator provisioning and any guest privileges require separate action. |

## Reviewer assignment

1. Compare candidate to HEAD and verify subsequent changes touch only documentation and evidence. Use a fresh disposable clone and profile. Check the packaged hash and source dependency graph. Confirm the legacy repo remains read-only.
2. Review actual routing, scope and file verification. Try out-of-scope requests, unknown topics, exact IDs, changed records, changed model bytes, conflicting retries, second writer, stop-all and restart. Verify changed permissions cannot follow a changed model proposal.
3. Run all four Windows commands in the [README](../../README.md), plus Python integrity tests and held-out evaluation. Reproduce training in a disposable clone. Compare source and data hashes, repeated templates and label provenance. Record failures even if headline accuracy agrees. If qualified to do so, test the unsigned ZIP on a clean disconnected Windows machine with no installed .NET desktop runtime.
4. Independently inspect recovery semantics and genuine local UI receipts. Include OS build, exact source revision, version and time, pass/fail/skip counts, SHA-256 hashes, actual limits and reviewer identity. Confirm UI only conveys the host's decision and native model remains in shadow.
5. Leave automation unavailable pending owner-provided dedicated guest image and permission for **read-only inspection of that named test guest**. Active isolation testing, VM provisioning, system policy changes, trusted export channels and any privileged service need separate, specific authorization. Keep hidden evaluator inputs under a distinct custodian/identity before any real qualification.
6. Record ACCEPTED, ACCEPTED_WITH_LIMITATIONS or REPAIRS_REQUIRED, scope of evidence, unresolved failures, next owner/agent and exact next assignment in this file. Refresh the owner report. Author testing is complete; independent acceptance is outstanding.
