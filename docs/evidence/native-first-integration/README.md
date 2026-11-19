# NEXT-016 through NEXT-020 integrated author verification

Frozen product `6dbf726443e9afd3c527f99d4851440e1404f4bb`. Local clones used `git clone --no-hardlinks --no-local`. The source checkout, replay and both package clones were clean at verification. .NET SDK 10.0.401, Windows PowerShell 5.1.26100.9818, PowerShell 7.6.9, Python 3.12.14 and NumPy 2.3.5. All new model evaluation and live native tasks used zero external inference calls.

| Clean-checkout gate | Observed result |
| --- | --- |
| Offline Release build | 0 warnings, 0 errors. [Build](build.txt) |
| Windows | 291/291, including new pinned model provenance and rejected refit display. [Checks](windows.txt) |
| Python | 33/33 with `OPENBLAS_NUM_THREADS=1`, including 16 new native-first tests. [Tests](python.txt) |
| Read-only host | 31/31 author-public factual controls, ten previous distribution audit receipts and five new receipts. [Factual controls](outcomes.txt) · [Previous audit](audit-existing.txt) · [New audit](audit-native.txt) |
| Acquisition and replays | All 360 actions reconstructed. All four partition digests and the stable specialist, Master, cooperation and refit report fields reproduced exactly. All three checkpoints reproduced byte-for-byte. [Capture](capture.txt) · [Checkpoint replay](checkpoint-replay.txt) |
| Prior accepted public suites | NEXT-005 8/8, NEXT-006 13/13, NEXT-007 17/17, NEXT-008 12/14 with two known omitted-using withholds, NEXT-009 7/7. No new incorrect host deliveries. [005](code_navigation.py.txt) · [006](code_depth.py.txt) · [007](project_work_host.py.txt) · [008](code_conversation_host.py.txt) · [009](research_recheck_host.py.txt) |
| Prior models | Synthetic shadow router 54/72. NEXT-014 `model_lab.py --fit` remained blocked with exit 2. Existing checkpoint results stayed at 16/16 host, 16/16 TF-IDF, 14/16 linear and 15/16 native. The earlier artifacts are unchanged. [Router](evaluate.py.txt) · [Eligibility gate](model_lab.py.txt) |
| Protected evaluator channel | Three author gate tests and a single synthetic external-path CLI smoke. Fresh protected data was unavailable to the author by design. [Evaluator contract](../next-020/README.md) |

| Integrated Windows package shell | ZIP SHA-256 | Bytes | WPF |
| --- | --- | ---: | --- |
| Windows PowerShell 5.1.26100.9818 | `b29cf787ff37da95edacc4e6e519c7ab02bc28dbd6716d9d29edf5bfedd5edec` | 95,201,648 | [Passed](ps5-wpf.txt) |
| PowerShell 7.6.9 | `fb8bf71ce5bf037f39807a6009e059097e9bddaeccb374d812b7383805806758` | 112,862,660 | [Passed](ps7-wpf.txt) |

Each shell built via `tools/package.ps1` then ran `tools/verify-package.ps1 -Zip out/ATLAS-Next-win-x64.zip` sequentially. Both passed 521 inventory hashes, offline model provenance, scoped source and research evidence, cancellation, quarantine, and persisted project recovery. The earlier NEXT-015R live-reference gates ran separately under both shells. [Repair evidence](../next-015r/README.md)

This is author evidence. All native candidates remain shadow-only, and the previous accepted NEXT-014 baseline remains the independent reference point.
