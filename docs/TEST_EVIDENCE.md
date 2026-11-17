# Author test evidence

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
