# Author test evidence

## NEXT-006 author verification

Frozen product `c0e138f63ef342d5ff38a6bce1dd9cd579d1b01c` builds on NEXT-005 product `42bf5a6979540f5c6e3d0d59893b0b288b1f6300`, independent decision `4fa37b1`. Commit `148a62e1585d8118dc79512c97c1a70d5181a147` froze the local-search comparator, source digests, requests, inspected expected spans and scoring protocol before product changes. The candidate receives only the source, basename, symbol, follow-up and repeat count. [Protocol](../lab/code-depth-protocol.md) · [Original comparator results](evidence/next-006/search-baseline.json)

| Provenance | Conversations completed by each policy | Exact deliveries | Correct withholds | Wrong deliveries |
| --- | ---: | ---: | ---: | ---: |
| Installed .NET MCP Server template and python-dateutil, author-written requests | 8/8 | 10 | 6 | 0 |
| Constructed C# duplicate-class and overload controls | 5/5 | 4 | 6 | 0 |
| Total for each of ordinary search and ATLAS | 13/13 | 14 | 12 | 0 |

In a fresh clone, `python -B lab/code_depth.py search out/search.json` and `python -B lab/code_depth.py host out/code-depth.json` reproduced the outcomes across four repetitions per case. The ordinary comparator tied the host and cost less. Both used zero per-task external inference. External developer assistance authored the public protocol and product changes. These data provide no basis for model promotion. [Complete comparator results](evidence/next-006/search.json) · [Complete host results and source provenance](evidence/next-006/code-depth.json)

After import, the median first definition and follow-up took **49.27 ms** and **17.06 ms**. Warm medians were **12.39 ms** and **13.91 ms**. Fresh host process, import, four conversations and cleanup together took **308.85 ms** median, reaching **52.91 MiB** sampled working set. The Python comparator measured both operations together in an existing process at **0.0197 ms** first lookup and **0.0156 ms** warm, reaching **24.54 MiB** sampled working set. Energy and sustained multi-project throughput were unmeasured.

| Clean-clone command or workflow | Recorded outcome |
| --- | --- |
| `dotnet build checks -c Release --configfile NuGet.Config`, then `AtlasNext.Checks.exe` | Zero warnings/errors, 150/150 checks. Includes 33 additions over NEXT-005 for ownership, bounded location feedback, attached versus detached docs, language-specific follow-ups, namespace bounds, parent and citation substitution, revision and project isolation. [Build](evidence/next-006/build.txt) · [Windows checks](evidence/next-006/windows-checks.txt) |
| Cached Python with `-B` on `lab/test_integrity.py`, `lab/outcomes.py`, `lab/project_audit.py` and `lab/evaluate.py` | 11/11 integrity, 31/31 public controls, ten distribution receipts reproduced, original shadow router 54/72 synthetic. [Integrity](evidence/next-006/integrity.txt) · [Controls](evidence/next-006/outcomes.json) · [Audit](evidence/next-006/audit.txt) · [Router](evidence/next-006/evaluation.json) |
| `lab/code_navigation.py host out/code-navigation.json` | NEXT-005 public replay passed 8/8 conversations, twelve expected deliveries and four withholds. [Earlier protocol replay](evidence/next-006/code-navigation.json) |
| `OPENBLAS_NUM_THREADS=1`, `lab/project_specialist.py evaluate` | Host/TF-IDF/linear/native achieved 16/16/14/15 successes out of 16 measured cases and 31/31/29/30 successes out of 31 public controls, zero wrong deliveries. Frozen model bytes match NEXT-005. [Shadow comparison](evidence/next-006/benchmark.json) |
| Windows PowerShell 5.1.26100.9818 | 95,122,989 bytes. SHA-256 `61f7f221a06a605af618208b68637bada2e8e54c5db115ac058daf22338f0a2f`. [Verification](evidence/next-006/verify-ps51.txt) · [Nine packaged UI task receipts](evidence/next-006/code-ui-ps51.json) |
| PowerShell 7.6.9 | 112,770,972 bytes. SHA-256 `d700d866925afcc647046b6bf81e178387636b76fc1878c39df0b6f7067364ae`. [Verification](evidence/next-006/verify-ps7.txt) · [Nine packaged UI task receipts](evidence/next-006/code-ui-ps7.json) |

Each shell ran `-NoProfile -ExecutionPolicy RemoteSigned -File tools/package.ps1` then `tools/verify-package.ps1 -Zip out/ATLAS-Next-win-x64.zip`. Each verified 519 inventory entries and the private runtime. The WPF workflow exercised qualification, signature, documentation, enclosing type, namespace and ambiguous-location feedback, as well as cancellation, restart and quarantine. The lab checkpoint stays outside both ZIPs. The verification clone remained clean.

Coverage is bounded to one direct type qualifier. Overloads and duplicate qualified names withhold. The selected parent must be a completed current definition. Attached documentation returns original source spelling. Unsupported Python expressions and nested C# namespaces lacking a single fully qualified span can withhold. This small public comparison and one workstation do not establish general coding superiority. Independent new and protected outcomes belong to Hydrogen.

## NEXT-005 author verification

Frozen public protocol and ordinary search baseline `a219e95b3313ffbfc666ce8b764053e28f239abb` preceded product `42bf5a6979540f5c6e3d0d59893b0b288b1f6300`. Source navigation runs locally on explicitly imported files. For a follow-up the owner selects a completed definition in project history. Submit and Finish check the current project, unique declaration and revision. The signature receipt links back to the selected task. [Protocol](../lab/code-navigation-protocol.md) · [Frozen comparator](evidence/next-005/search-baseline.json)

| Two pinned unfamiliar Python codebases, eight conversations | Correct cited deliveries / 12 answerable turns | Wrong deliveries | Correct abstentions / 4 | Complete conversations / 8 |
| --- | ---: | ---: | ---: | ---: |
| Ordinary anchored declaration-line search | 10 | 2 | 4 | 6 |
| Frozen host and verifier | 12 | 0 | 4 | 8 |

Replayed in a new checkout with cached Python 3.12.14. `python -B lab/code_navigation.py search out/search.json` and `python -B lab/code_navigation.py host out/code-navigation.json` read installed pyhumps 3.8.0 and pdf2image 1.17.0 sources by pinned digest. Python AST and tokenization independently supply expected identifier and header spans. The two search failures returned only the first line of multiline signatures. Requests and harness are author-written. Both approaches used zero external inference calls per case. External developer assistance produced the implementation and public experiment. These observations are ineligible for training or private qualification. The benchmark covers two Python files. C# coverage comes from Windows and packaged UI controls. [Search replay](evidence/next-005/search.json) · [Host trials, exact text, costs and expectations](evidence/next-005/code-navigation.json)

Median in-process local search took 0.29 ms per conversation and sampled up to 25.4 MiB working set. The cold two-turn host probe took 211.9 ms median including process startup and file import, sampling up to 44.4 MiB. Inside the probe, after import, definition and follow-up submit/finish times had medians of 51.2 ms and 19.1 ms respectively. The measurements use different process boundaries. They establish an accuracy gain on these inputs at a higher observed cost. Energy, long sessions, large codebases and general coding productivity remain unmeasured.

| Clean-clone author verification | Result |
| --- | --- |
| `dotnet build checks -c Release --configfile NuGet.Config` and `AtlasNext.Checks.exe` | 0 warnings/errors, 117/117. Includes duplicates across files and types, project isolation, swapped follow-up parent, stale revisions, BOM/CRLF/UTF-16, restart, comments, source-vs-text separation, restricted proposals and both NEXT-004 edge repairs. [Build](evidence/next-005/build.txt) · [Checks](evidence/next-005/windows-checks.txt) |
| `python -B lab/test_integrity.py`, `lab/outcomes.py`, `lab/project_audit.py`, `lab/evaluate.py` | 10/10 integrity, 28/28 author fixtures, all ten distribution receipts reproduced, original shadow router 54/72 synthetic. [Integrity](evidence/next-005/integrity.txt) · [Fixtures](evidence/next-005/outcomes.json) · [Audit](evidence/next-005/audit.txt) · [Routing](evidence/next-005/evaluation.json) |
| `OPENBLAS_NUM_THREADS=1` and `lab/project_specialist.py evaluate` | No refit. Host/TF-IDF/linear/native completed 16/16/14/15 of 16 public measured cases, and passed 28/28/27/28 of 28 constructed controls. Zero wrong deliveries. [Frozen-policy results](evidence/next-005/benchmark.json) |
| Windows PowerShell 5.1.26100.9818, `package.ps1` then `verify-package.ps1` | 95,115,295 bytes, SHA-256 `87bd4040ee05b8a2067db5d5bfa395d5c5287a3409199f04c63a0af2dbe6e103`. [Verification](evidence/next-005/verify-ps51.txt) · [Definition](evidence/next-005/code.definition-ps51.json) · [Follow-up](evidence/next-005/code.signature-ps51.json) |
| PowerShell 7.6.9, same workflow | 112,761,107 bytes, SHA-256 `40f2741548285b675e64d457375f10104e8984dd88dbfbafd2756623558ca568`. [Verification](evidence/next-005/verify-ps7.txt) · [Definition](evidence/next-005/code.definition-ps7.json) · [Follow-up](evidence/next-005/code.signature-ps7.json) |

Both shells used `-NoProfile -ExecutionPolicy RemoteSigned`. Each ZIP verified 519 inventoried files and passed the actual file picker, source conversation, restart, contradiction and quarantine checks. Both packages include SDK-derived Roslyn syntax assemblies and localized resources. The original router is advisory, and the lab checkpoint stays outside the ZIP. The checks launcher initially lacked a runtime dependency entry for the parser in development. Shared SDK references for both entry points fixed that failure. The final clone stayed clean.

C# uses a syntax tree without compilation or project-wide type resolution and refuses conditional compilation and syntax errors. Python recognizes a bounded lexical subset and does not certify complete Python syntax or runtime binding. Header imports can reject unsupported extensions of the grammar. Identifiers are case-sensitive, overloaded or duplicate names withhold, and a changed parent requires a new lookup. These are owner-selected files, not automatic repository inspection. Independent protected outcomes for this milestone belong to Hydrogen.

## NEXT-004 author verification

Frozen product `0f57a164dc2f1437c633a7b574d350e4aced4642`, based on accepted product `bb015a42205b8c2b6e4ad4cefae99a6b116099dd` and independent handoff `3f227d4`. A fresh local clone built with `dotnet build checks -c Release --configfile NuGet.Config` produced zero warnings and errors. The checks executable passed 91/91 on Windows. This includes the original 71 checks, UTF-16 extraction across non-BMP text, paraphrases, literal-name ambiguity, unsupported modifiers, numeric and contextual evidence, project scope, journal recovery and delivery authority. The two reviewer-controlled suites remain in Hydrogen’s custody. [Build](evidence/next-004/build.txt) · [Windows checks](evidence/next-004/windows-checks.txt)

Cached Python with `-B` ran `lab/test_integrity.py` (9/9), `lab/evaluate.py` (original synthetic router 54/72), `lab/outcomes.py` (21/21 constructed host outcomes) and `lab/project_audit.py` (all ten archived installed-project observations reproduced, including the previously documented 20 NumPy, 8 Pillow and 7 lxml digest discrepancies). Measured source values have upstream receipts. Questions and document projections are author-written. Neither the fixture additions nor private cases entered fitting. [Integrity](evidence/next-004/integrity.txt) · [Routing](evidence/next-004/evaluation.json) · [Fixture receipts](evidence/next-004/outcomes.json) · [Source audit](evidence/next-004/audit.txt)

`OPENBLAS_NUM_THREADS=1` and `lab/project_specialist.py fit` reproduced the candidate checkpoint byte-for-byte, SHA-256 `5c476024d16fdc7054d901551bc66373639997b984f49cf89f74623f471d6a07`. Four training families, 16 questions and 64 host proposals yielded 16 successful targets (previously 14). Validation used two other families and eight questions. Both selected cutoffs in the lab comparison are 0.5. The 49-parameter network selected seed 11, with 34.7 ms fit time and 19,824 peak bytes traced by Python. No runtime learning or policy promotion. [Fit and validation trials](evidence/next-004/fitting.json) · [Byte comparison](evidence/next-004/refit-digest.txt)

| Frozen candidate policy | Correct document / 16 | Verified completions / 16 | Constructed outcomes / 21 | Wrong deliveries across both sets | Top-document Brier | p95 scoring µs | p95 host ms | Peak host working set MiB |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Deterministic host | 16 | 16 | 21 | 0 | — | Included in host | 110.5 | 72.1 |
| TF-IDF | 16 | 16 | 21 | 0 | — | 100 | 113.9 | 72.5 |
| Linear | 14 | 14 | 20 | 0 | 0.06255 | 448 | 113.9 | 72.5 |
| Native | 15 | 15 | 21 | 0 | 0.03764 | 318 | 113.9 | 72.5 |

`lab/project_specialist.py evaluate` ran all four policies on identical inputs and real host outcomes. The native policy withheld the correct document for the XlsxWriter file count (0.310 confidence). Linear also withheld the openpyxl parameters (0.384) and the constructed argument-relation case. Both now pass the original predicate-syntax fixture. The host and TF-IDF have no probability forecast. Native improved on linear and its own frozen predecessor but supplies no additional completed task over either deterministic baseline. These four unfamiliar upstream families share one audit workflow. Reported timing is a single workstation observation with overlapping verification jobs. Median cold-process time was 276–282 ms. Energy, VRAM, sustained warm-session latency and general coding usefulness were unmeasured. The host’s historical p95 was 78.2 ms with a 53.4 MiB peak, so the added coverage also has an observed cost. [Complete trials, calibration bins and costs](evidence/next-004/benchmark.json)

| Same 16 measured / 21 constructed inputs | Host | TF-IDF | Linear | Native |
| --- | ---: | ---: | ---: | ---: |
| Accepted NEXT-003 executable and original policies | 14 / 15 | 14 / 15 | 14 / 14 | 14 / 14 |
| NEXT-004 executable and original policies | 16 / 21 | 16 / 21 | 14 / 18 | 14 / 18 |
| NEXT-004 executable and refitted policies | 16 / 21 | 16 / 21 | 14 / 20 | 15 / 21 |

To reproduce the historical controls, clone accepted product `bb015a4` separately, build its Release checks and copy only the frozen candidate’s two public outcomes JSON files into that disposable checkout. Run the original `project_specialist.benchmark()` with the original checkpoint and feature extractor. Repeat in that checkout with `outcomes.ROOT` set to the frozen candidate build root to change only the executable. The original native Brier was 0.12028 on the repaired host. All historical and candidate comparisons had zero wrong deliveries. Earlier independent results apply to their respective historical revisions. [Original host and policies](evidence/next-004/old-policy-old-host.json) · [Original policies against repaired host](evidence/next-004/old-policy-new-host.json) · [Earlier independent review](evidence/independent-review-next003.json)

| Shell running package.ps1 and verify-package.ps1 | Bytes | Verified local ZIP SHA-256 |
| --- | ---: | --- |
| Windows PowerShell 5.1.26100.9818 | 80,208,498 | `ab6960506785351502408a4899b556240f10996072f42788e2061634a211358f` |
| PowerShell 7.6.9 | 94,815,329 | `60edd2e022920f7ab9b2fb383815d17dfa23a1857804956cc1d4812f11fdcc30` |

Each shell used `-NoProfile -ExecutionPolicy RemoteSigned`, built its own ZIP and verified 491 inventory entries and the private runtime across three packaged launches. The packaged app imported an owner-selected file, answered “What is the restore signal colour set to, please?” with “amber”, exported its citation, and withheld delivery after a conflicting import. Reopen, cancellation, interruption and quarantine also passed. The lab checkpoint is absent from both ZIPs. The clone stayed clean. These runs used the same Windows workstation. Clean offline OS deployment, production signing and execution isolation remain open. [5.1 verification](evidence/next-004/verify-ps51.txt) · [5.1 receipt](evidence/next-004/import-ps51.json) · [7 verification](evidence/next-004/verify-ps7.txt) · [7 receipt](evidence/next-004/import-ps7.json)

## NEXT-003 author verification

Candidate `bb015a42205b8c2b6e4ad4cefae99a6b116099dd` on local main, built and verified in a fresh disposable clone. The previously accepted implementation was `c1b09d6`; its documentation and review checkout was `618f870`. Under the original code, the added regression for “What is the restore manifest entries?” failed before the fix. The current app resolves the imported subject and verifies the answer from the full project. If Primary has no eligible source, a supported Research finding still withholds delivery. A revoked journal authorization also blocks delivery.

| Clean-clone command or workflow | Recorded result |
| --- | --- |
| `dotnet build checks -c Release --configfile NuGet.Config` | 0 warnings and 0 errors. SDK 10.0.401, WindowsDesktop runtime 10.0.12, no external NuGet libraries. [Build](evidence/next-003/build.txt) |
| `checks/bin/Release/net10.0-windows/AtlasNext.Checks.exe` | 71/71 passed, including imported procedure vocabulary, retained handbook procedures, unknown-plus-definite claims, project isolation and source/authorization checks. [Windows results](evidence/next-003/windows-checks.txt) |
| `python -B lab/test_integrity.py`, `python -B lab/outcomes.py` | 8/8 integrity checks and 13/13 public constructed end-to-end cases. [Integrity](evidence/next-003/integrity.txt) · [Fixture results](evidence/next-003/public-fixtures.json) |
| `python -B lab/evaluate.py` | Frozen synthetic routing suite, rule 48/72, exemplar 42/72, original desktop model 54/72 and linear 54/72. [Routing results](evidence/next-003/synthetic-routing.json) |
| `python -B lab/project_audit.py` | Re-read ten installed public project distributions, verified archived source observations against current bytes, and reproduced their published metadata, payload check counts and API signatures. The audit observed hash mismatches in the installed NumPy, Pillow and lxml distributions. Their receipts identify the affected relative files and observed hashes. The cause was outside this experiment. [Audit replay](evidence/next-003/audit.txt) · [Provenance and split protocol](../lab/project_data/README.md) |
| `python -B lab/project_specialist.py fit` | A second fit reproduced the frozen lab checkpoint byte-for-byte, SHA-256 `8f3cfcc1e06aa7a447851dd203509f83e68f92c5265b89afecdf0b9957fbfd14`. Original network 6 inputs, 6 hidden units, 1 output (49 weights and biases). Logistic comparator has 7 parameters. Four training projects, 16 authored questions, 64 actual in-scope source proposals, 14 exact downstream successes. Two validation projects and 8 questions. Seeds 11, 37 and 73 and all three cutoffs are recorded. Selected seed 73, cutoff 0.5. Its fit took 38.0 ms with 19,912 bytes of traced peak allocation. [Measured targets, costs and all validation trials](evidence/next-003/fitting.json) |
| `python -B lab/project_specialist.py evaluate` | Four previously unseen upstream projects, 16 independently checkable author outcomes. Results below. Each measured action exercised real import, submit and finish in a disposable profile. Policies choosing the same proposal reuse that measured action. [Per-case results, costs and calibration](evidence/next-003/benchmark.json) |
| `python -B lab/project_specialist.py blind --manifest <external-manifest> --report <fresh-external-report>` | Exercised using an author-owned copy of the public fixtures outside the checkout. The temporary profiles were removed. The comparison was 13/13 for host and TF-IDF, 12/13 for both learned policies. No custodian inputs were used in this run. [External-mode smoke test](evidence/next-003/external-author-smoke.txt) |

**Held-out project outcomes.** The historical host passed 12/16. The repaired host passed 14/16 on the same questions, including both previously missed restore-manifest lookups. To repeat the historical run, build the Release checks in a disposable clone at `618f870`. In the new candidate's `lab/` Python environment, point `outcomes.ROOT` to the historical clone and call `project_specialist.observe(case, None)` for each `project_cases.cases("evaluation")` row. Inputs and oracle remain those of NEXT-003. [Historical replay](evidence/next-003/next002-accepted-baseline.json)

| Policy | Correct document / 16 | Correct completed answer / 16 | Wrong deliveries | Withheld | p50 / p95 scorer µs | Top-document Brier / ECE |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| Repaired deterministic host | 14 | 14 | 0 | 2 | In host | — |
| TF-IDF cosine | 16 | 14 | 0 | 2 | 244 / 317 | — |
| Learned linear comparator | 14 | 14 | 0 | 2 | 288 / 375 | 0.00293 / 0.04762 |
| ATLAS-original shadow specialist | 14 | 14 | 0 | 2 | 266 / 290 | 0.00017 / 0.01193 |

The host's p50/p95 lookup times were 73.4/114.5 ms. The selected native policy's measured actions took 74.8/131.0 ms in the host, with median cold-process time 243.1 ms and maximum sampled host working set 56,135,680 bytes. Python 3.12.14, NumPy 2.3.5 and one OpenBLAS thread were used. Small-sample calibration describes only these requests. The linear and native policies each abstained twice. TF-IDF proposed a document each time, and the host withheld twice. All policies missed “Which arguments does the workbook loader accept?” and “How many installed files did the audit check?” because the verifier could not establish support under its question grammar. Both learned policies also abstained on an answerable declarative sentence in the constructed stress suite. The model features favor colon-delimited claims. The recorded failures remain in the evaluation.

**Windows packaging.** The commands in both 64-bit shells were `-NoProfile -ExecutionPolicy RemoteSigned -File tools/package.ps1` and `-NoProfile -ExecutionPolicy RemoteSigned -File tools/verify-package.ps1 -Zip out/ATLAS-Next-win-x64.zip`. Each checked 491 payload hashes, the private runtime, three launches, crash recovery and quarantine, native import, project-scoped lookup, export and contradiction withholding. The question “What is the restore signal colour?” returned `amber` at line 2, UTF-16 [41, 46). Both packages contained the original advisory desktop checkpoint and excluded the experimental specialist. [Package model inspection](evidence/next-003/package-models.txt)

| Shell | ZIP bytes | SHA-256 | Evidence |
| --- | ---: | --- | --- |
| Windows PowerShell 5.1.26100.9818 | 80,203,430 | `02af933421839f641f88afa44b6420393271aaad27a6a622610391d7c89fbe46` | [Build](evidence/next-003/package-ps51.txt) · [UI verification](evidence/next-003/verify-ps51.txt) · [Exported receipt](evidence/next-003/import-ps51.json) |
| PowerShell 7.6.9 | 94,809,381 | `79385d62acbe8dbee589f4f50e14376482dd577865341e5d367b605fc0ac5703` | [Build](evidence/next-003/package-ps7.txt) · [UI verification](evidence/next-003/verify-ps7.txt) · [Exported receipt](evidence/next-003/import-ps7.json) |

The observed source values were collected by the author from real installed artifacts. Questions and document projections were authored for this workflow. The four unseen test projects share the same distribution-audit format. These results provide no independent model qualification or evidence of general coding performance. The lab checkpoint stays in shadow. A clean disconnected OS without the installed desktop runtime, production signing and isolated guest execution remain open.

## NEXT-002 author verification

Exact product candidate `c1b09d61adc31871d9c3fa818e7f3b3c56c3b4cc` in a fresh local clone on Windows 11 Pro x64. SDK 10.0.401, bundled WindowsDesktop runtime 10.0.12, cached Python 3.12.14, NumPy 2.3.5 and `OPENBLAS_NUM_THREADS=1`. The working tree in the verification clone was clean after the suite.

| Command or workflow | Observed result |
| --- | --- |
| `dotnet build checks -c Release --configfile NuGet.Config` | 0 warnings, 0 errors, no external NuGet libraries. [Build](evidence/next-002/build.txt) |
| `checks/bin/Release/net10.0-windows/AtlasNext.Checks.exe` | 63/63 passed. Includes UTF-8 BOM, CRLF, supplementary Unicode, revision replacement and tamper, idempotency, project switching and cross-project refusal, stale and conflicting claims, insufficient values, invalid input and hard links. A separate probe rejected an ancestor junction before creating a store. [Windows checks](evidence/next-002/windows-checks.txt) · [Junction probe](evidence/next-002/junction-check.txt) |
| `python -B lab/test_integrity.py` and `python -B lab/evaluate.py` | 5/5 integrity checks. Frozen routing benchmark scored rule 48/72, exemplar 42/72, native 54/72 and linear 54/72. [Tests](evidence/next-002/integrity.txt) · [Evaluation](evidence/next-002/public-routing.json) |
| `python -B lab/outcomes.py` | 10/10 public end-to-end cases passed. The scorer checks actual engine receipt, record or exact source span, verdict and result. Each trial records elapsed time and working set, including expected abstentions. [Full trials](evidence/next-002/public-outcomes.json) |
| `python -B lab/outcomes.py --manifest <external-manifest> --report <fresh-external-report>` | 1/1 author-run case grounded in this candidate's observed zero-warning build. The question was scored against the release-build receipt digest. The temporary case profile was removed. [Case and provenance](evidence/next-002/observed-build-case.json) · [Actual outcome](evidence/next-002/observed-build-outcome.json) |
| Windows PowerShell 5.1.26100.9818, package and UI verification | 80,202,719 bytes. SHA-256 `5ed0da16ba1a0066a614e936aa83972cac8c3c4fa81e97e4177639547c90f3f5`. [Package](evidence/next-002/package-ps51.txt) · [Verification](evidence/next-002/verify-ps51.txt) · [Export](evidence/next-002/span-receipt-ps51.json) |
| PowerShell 7.6.9, package and UI verification | 94,808,548 bytes. SHA-256 `ca8295abf60b120d8c66f483a87ca05ec4435cb94ce7cbbf59c13957bc6406fe`. [Package](evidence/next-002/package-ps7.txt) · [Verification](evidence/next-002/verify-ps7.txt) · [Export](evidence/next-002/span-receipt-ps7.json) |

Both shell runs used `-NoProfile -ExecutionPolicy RemoteSigned -File tools/package.ps1`, then `-NoProfile -ExecutionPolicy RemoteSigned -File tools/verify-package.ps1 -Zip out/ATLAS-Next-win-x64.zip`. Each verified 491 hashes, private CoreCLR and three real launches. In project `package-check`, the picker imported `channel.md`. The app returned `amber` from line 2, UTF-16 offsets [33, 38). Importing `other.txt` with a different value withheld delivery while reporting that the subject and intent matched. [Persisted contradiction](evidence/next-002/packaged-contradiction.json)

**Development failures.** An early grader rejected the second of two equally valid citations. The case now declares both allowed documents. A nested external workspace exposed a Windows long-path cleanup failure. The runner now supplies extended paths to the temporary-directory cleanup. Early native-picker checks missed WPF readiness and assumed the common dialog exposed an edit pattern. The final harness waits for the WPF status control, scopes the native picker by process and locates the Windows file-name control. An initial hard-link fixture used an unavailable .NET API and was replaced with the Windows file API. [Rejected trial](evidence/next-002/rejected-outcome-trial.json)

**Qualification limits.** Author-created fixtures and the observed build case are author evidence. Imported claims are attributed statements under the bounded grammar. External truth, broad semantic reasoning, model improvement and confidential evaluator custody require independent evidence. Project partitioning operates within the same trusted Windows account. Fresh disconnected OS, signing and guest execution remain open.

## NEXT-001R repair candidate

A clean local clone of `87acbccf36d8c5e1916db84c1e4a2f2f122d3d14` on Windows 11 Pro x64 built with .NET SDK 10.0.401 and the installed 10.0.12 desktop runtime. The first new duration regression failed on the NEXT-001 implementation. All required author commands passed on the committed repair candidate.

| Check | Observed result |
| --- | --- |
| Offline Release build with cleared package sources | 0 warnings and 0 errors. [Build output](evidence/next-001r/build.txt) |
| Windows checks | 40 passed, 0 failed, 0 skipped. A valid same-type replacement citation passed source integrity and failed answer relevance. A locked file during startup preflight left existing files intact. A second run recovered the healthy review and preserved all 20 quarantined originals. [Check output](evidence/next-001r/windows-checks.txt) |
| Python integrity and public evaluation | 3 integrity tests passed. With one BLAS thread, the original model and linear comparator each scored 54/72, the frozen NEXT-001 reference rule 48/72, and exemplar matching 42/72. This benchmark measures synthetic route labels. The new delivery verifier is exercised by the Windows checks. [Integrity output](evidence/next-001r/integrity.txt) · [Full public evaluation](evidence/next-001r/public-evaluation.json) |
| Windows PowerShell 5.1.26100.9818 | Packaging and verification both passed. ZIP 80,186,910 bytes, SHA-256 `ffc1bfe9b7cb6e1ad71a7ef9bec9087f4faeb59c51e24e907b7fa4e1c6404d7`. [Package](evidence/next-001r/package-ps51.txt) · [Three launches](evidence/next-001r/verify-ps51.txt) · [Exported receipt](evidence/next-001r/receipt-ps51.json) |
| PowerShell 7.6.9 | Packaging and verification both passed. Canonical ZIP 94,789,796 bytes, SHA-256 `9aad994a249b3fc92cdfaa9c6b0fbb217f15de6ea9b48bd7d167f8c3e4d11281`. [Package](evidence/next-001r/package-ps7.txt) · [Three launches](evidence/next-001r/verify-ps7.txt) · [Exported receipt](evidence/next-001r/receipt-ps7.json) |

Both ZIPs passed 491 inventory hashes and loaded their own CoreCLR. Each packaged window returned the 30-day fact, displayed both verdicts, exported the receipt, reopened completed history, cancelled a task and recovered a killed review alongside five damaged files. The original PowerShell 5.1 invocation encountered a local script-policy refusal before execution. The documented process-local `RemoteSigned` invocation worked without elevation. Build scripts and tests ran on the same interactive workstation. A clean disconnected OS, private evaluation, signing and guest isolation remain unverified.

## Historical NEXT-001 author run

Product candidate **7d319bc33be94c747f15fbc6f52d873379b51282**, Windows 11 Pro x64, .NET SDK 10.0.401, desktop runtime 10.0.12. All receipts here are author evidence. The documented purpose of the tests is bounded local research and package validation.

| Command or probe | Observed result |
| --- | --- |
| Fresh independent clone of the new repository, `dotnet build checks -c Release --configfile NuGet.Config` | Two projects built, 0 warnings and 0 errors. Package sources cleared. [Build output](evidence/build.txt) |
| `checks/bin/Release/net10.0-windows/AtlasNext.Checks.exe` | 21 passed. Includes checkpoint tamper, 72 golden output comparisons within 1e-10, retrieval, real WPF actions, task state, individual/global stop and modified source rejection. [Check output](evidence/windows-checks.txt) |
| `python -B lab/test_integrity.py` | 3 tests passed. Origin relabeling refusal, disjoint public splits and model/data digests. |
| `python -B lab/train.py` in disposable clone, OPENBLAS_NUM_THREADS=1 | Reproduced exact weight SHA-256 6b9b23fcf890807f2a1d989f1b9870660bcf3030ce6e729674844e9f722076c4. NumPy 2.3.5 / Python 3.12.14 / CPU. |
| `python -B lab/evaluate.py` | Official synthetic held-out report includes each error, class confusion, macro-F1, latency and comparisons. [Public evaluation](evidence/public-routing-evaluation.json) |
| `tools/package.ps1`, then `tools/verify-package.ps1 -Zip out/ATLAS-Next-win-x64.zip` | Final ZIP 94,784,782 bytes with SHA-256 ff63a8c90ccd6df0191e8af78eb2ec4c9ba60ef71ce287d4f748f5b5ef8815a1. Verified 491 inventoried files, observed bundled CoreCLR, invoked Find/Verify/Export in the packaged window, reopened the completed result, cancelled a task and force-terminated a pending review. On relaunch it was interrupted. [Actual receipt](evidence/packaged-task.json) |
| `tools/check-guest.ps1 -Name ATLAS-Next-Offline-Review` | Windows denied Get-VM authorization. VM existence/configuration could not be established. Service running is insufficient evidence of isolation. No elevated change attempted. |

The packaged example completed a scoped fact lookup and returned the handbook's 30-day retention setting. It recorded 7,107 µs for first native inference and a 124,661,760-byte process working set in that run. The Python benchmark times measure feature extraction plus warm process classification and are not packaged-app response times.

## Research result and failure cases

Training used 120 public rule-labeled synthetic requests from eight fictional project names, validation used 12 requests from two other names, and held-out evaluation used 72 requests from six other names with disjoint prompt templates. All label assignments derive from declared template intent. The fixture generator and text were authored by Astra. This is a test of paraphrase/topic transfer on synthetic questions, with no independent empirical project outcome labels. The training entry point reads train and validation only. Public held-out files are accessible to a developer and the process identity. An independent reviewer must create and control any future secret qualification suite.

Random-init 256→16 tanh→3 softmax policy, 4,163 parameters, 250 full-batch gradient steps at learning rate 0.55. Seeds 19/37/73 scored 100%, 91.7% and 83.3% on validation. Selected seed 19. Timed fits 0.158, 0.021 and 0.022 seconds on CPU. The release manifest pins code revision, data, seed and checkpoint. The linear baseline has 771 parameters.

| Policy | Correct / 72 | Accuracy | Macro-F1 | Python p95 µs |
| --- | ---: | ---: | ---: | ---: |
| Active deterministic rule | 48 | 66.7% | 0.655 | 3.6 |
| Training-exemplar overlap, secondary comparison added after initial evaluation | 42 | 58.3% | 0.556 | 42.0 |
| Original MLP | 54 | 75.0% | 0.744 | 60.5 |
| Linear learned baseline | 54 | 75.0% | 0.741 | 20.1 |

The native router was +8.33 percentage points above the active rule and tied the linear comparator. It misclassified “What steps restore the atlasprint archive?” as fact and “How long will atlasprint keep its archive?” as clarify. Similar failures repeat across the six project names. Its softmax score is uncalibrated. There is no independent superiority finding. [Model lineage](../assets/native.manifest.json)

## Negative results and untested boundaries

The initial hidden-window packaging probe timed out waiting for a render notification. A Loaded/dispatcher probe worked. The initial smoke launcher waited on descendants and blocked on the open app. A later UIA lookup raced control creation. The harness now waits on the specific process/window and control, and final product verification passed. No final product test was skipped in the 21 C# checks or 3 Python tests. Physical-network-disconnected clean-OS install, production signing, multi-user security, large-catalog scale, complete DPI/accessibility testing, destructive power-loss, private evaluation and safe guest execution are unverified. The package uses only local build inputs and launched with its private runtime on this workstation. The network adapter was not disabled for the test.
