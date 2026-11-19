# NEXT-021 through NEXT-025 clean author replay

Integrated product `19f3f468b404d7642a2370a9eb19e6e5b2f4aff8`. Separate documentation installation `f5f8f75a29ee02dc72da26dcc15a88fe14d42603`. The author used fresh local clones with `git clone --no-hardlinks --no-local` and verified frozen HEADs. The machine used Windows x64, .NET SDK 10.0.401, Python 3.12.14, NumPy 2.3.5, and one BLAS thread. No hosted dependencies were fetched. [Master installation proof](master-install.json)

| Gate | Actual result |
| --- | --- |
| Master and source verification | Exact v3.3 canonical SHA-256, all 301 sections, 192 chapters, 23 parts, 23 appendices and 50 historical IDs passed. The validator is in the source test suite. No hosted CI was configured. [Master check](master.txt) |
| Release build and Windows checks | Zero warnings/errors and 292/292 Windows checks. [Build](build.txt) · [Checks](windows.txt) |
| Python and historical facts | 53/53 Python tests and 31/31 public factual controls. [Python](python.txt) · [Factual](factual.txt) |
| Foundry and from-scratch reproduction | All 504 host actions ran again. Partition and knowledge digests matched. All four checkpoint files, representing the language candidate, planner, two role heads and refit, reproduced byte-for-byte. The non-timing parts of all public comparison reports matched exactly. [Foundry](foundry.txt) · [Checkpoint hashes](checkpoint-replay.txt) |
| Native-only actual outcome | The full team used six steps to verify a public package audit fact and its Python declaration. No pretrained or external inference. [Live clean-clone receipt](live-compound.json) |
| Prior public replays | NEXT-005 8/8, NEXT-006 13/13, NEXT-007 17/17, NEXT-008 12/14 with its established explicit-using withholds, and NEXT-009 7/7. The 360 historical actions and all four historic data hashes reproduced. The historical specialist comparison stayed at host/TF-IDF 16/16, linear 14/16, original specialist 15/16, and NEXT-014 fit remained blocked with exit 2. These replays ran on `9a748d46456561a4fb1950cf6efbb99cb46b1e11`. The restored UI asset pin in `19f3f468` was the sole change between those revisions. [005](code_navigation.py.txt) · [006](code_depth.py.txt) · [007](project_work_host.py.txt) · [008](code_conversation_host.py.txt) · [009](research_recheck_host.py.txt) · [Historic data](native_data.py.txt) · [Fit gate](model_lab.py.txt) |
| Evaluator path | Author-only synthetic external-path checks exercised the frozen old Master and new mixed-source language scorers. Independent protected runs await the custodian. [Foundry notes](../next-021/README.md) · [Refit notes](../next-025/README.md) |

| Windows package | SHA-256 | Size | WPF result |
| --- | --- | ---: | --- |
| Windows PowerShell 5.1.26100.9818 | `dfd1befb7e23c7626e19d1bab378b76ffeda805775c00ba56927fe405c876162` | 95,203,011 bytes | [Passed on rerun](ps5-wpf.txt) |
| PowerShell 7.6.9 | `e2ab592ad454af4e92150932b28f95893d304234488d3aef2f0b346bc0d0aa13` | 112,864,343 bytes | [Passed](ps7-wpf.txt) |

Both packages verified 522 file hashes, private runtime, native file picker, claim and declaration citations, workspace/evidence panes, recovery and five byte-identical quarantine fixtures. Packaging ran `tools/package.ps1` and offline `tools/verify-package.ps1 -Zip out/ATLAS-Next-win-x64.zip` in each respective shell. No reference service was needed. The initial integrated attempt exposed a mistyped pin for the new evidence view, which was corrected from the actual asset bytes before the final freeze. One earlier 5.1 verification of the final ZIP reported two recovered tasks instead of the expected single crash fixture. The serial rerun used the same ZIP and passed. The cause of the extra task was not confirmed. [First 5.1 run](ps5-wpf-first.txt)

No model gained qualification. The weakest remaining evidence is genuine transfer beyond explicit typed references on public audit projections. The public comparison includes authored safety controls and cannot substitute for independent protected evaluation.
