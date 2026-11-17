# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-001R independent re-review |
| State | PENDING_INDEPENDENT_REVIEW |
| Next writer | Gemini Hydrogen |
| Implementation candidate | `87acbccf36d8c5e1916db84c1e4a2f2f122d3d14` on local `main` in `C:\ATLAS\ATLAS-Next` |
| Review baseline | `14e4c016581f7e4c7f1e49756925c1cd5f83d107`, Hydrogen’s NEXT-001 verdict |
| Canonical package | `out/ATLAS-Next-win-x64.zip`, SHA-256 `9aad994a249b3fc92cdfaa9c6b0fbb217f15de6ea9b48bd7d167f8c3e4d11281` |
| Windows PowerShell 5.1 build | `out/ATLAS-Next-win-x64-ps51.zip`, SHA-256 `ffc1bfe9b7cb6e1ad71a7ef9bec9087f4faeb59c51e24e907b7fa4e1c6404d7` |
| Author results | 40/40 Windows checks, 3/3 Python integrity tests, both shell package-and-UI runs passed, 491 inventory hashes per ZIP. Offline build had 0 warnings and 0 errors. |

## Gemini Hydrogen’s assignment

Read the [prior verdict](../evidence/independent-review-next001.json), [adversarial probes](../evidence/regression-adversarial-next001.json), [owner report](ATLAS_OWNER_REPORT.md) and [author receipts](../TEST_EVIDENCE.md). Review the exact product commit in a disposable local clone. The subsequent author documentation commit contains only the handoff, report and evidence. Use the [build and package instructions](../../README.md) to reproduce the checks on an interactive x64 Windows desktop.

1. Re-run the offline build, Windows checks, Python integrity and public evaluation. Verify that the original model stays advisory and that the release retains its established permission boundaries. The Python rule comparator is the frozen NEXT-001 baseline.
2. Exercise D1 in the packaged app with unseen supported, conflicting and out-of-scope requests. Substitute an authentic but irrelevant citation for a pending result, including one of the same type. Inspect the returned body, both verdicts and old schema-1 receipts.
3. Exercise D2 with healthy completed and pending history mixed with corrupt records, invalid filenames and pending writes. Check original bytes, event counts, operator notice and behavior on a second launch. Check an I/O preflight failure before any interruption is written.
4. Build and run the package verifier under Windows PowerShell 5.1 and PowerShell 7. Confirm real window identity, private runtime, duration answer, export, stop and crash recovery. Distinguish clean-workstation coverage from a clean disconnected OS.
5. Record the independently observed results and exact candidate in a new review receipt. As sole next writer, update this file and the owner report, then commit the verdict. On `REPAIRS_REQUIRED`, assign the narrow remaining defects to the implementation engineer. On `ACCEPTED`, assign a bounded NEXT-002 increment to the implementation engineer. The next agent can resume by reading this file.

NEXT-002 priorities are independent custody of outcome evaluation, Research & Verification evidence sufficiency and contradiction checks, and useful local intelligence against authorized file-first evidence. Require independent outcomes before any model promotion. Executable automation, Windows privilege changes and edits to the legacy repository are outside this assignment. Continue to use the canonical handoff for each change of writer.
