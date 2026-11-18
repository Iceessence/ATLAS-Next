# ATLAS Next

Local Windows desktop research candidate. Ask the demonstration handbook about archive retention, restoring an archive, or export format. Each answer comes from a versioned file. The app compares its original CPU model with the active deterministic router and can export a receipt for every task.

## Run the packaged app

Extract the entire `out/ATLAS-Next-win-x64.zip` into a writable local folder and open **ATLAS Next.cmd**. This portable release includes its x64 .NET 10.0.12 runtime. No install, Python, GPU, model service or account is required. The package is unsigned. The author-tested target is Windows 11 Pro x64. [Build and package evidence](docs/TEST_EVIDENCE.md)

1. Choose **Find evidence** on the example request. Inspect the selected record, rule and native recommendation.
2. Choose **Verify and finish**. ATLAS reopens the file, checks the citation and evaluates whether that record supports the whole request. The window and exported receipt show separate source-integrity and answer-relevance verdicts. Try “How do I restore an archive?” for a procedure, or enter `fact.format` for an exact lookup.
3. **Export receipt** writes JSON under the profile’s exports directory. Close and reopen to see completed history. Stop a pending lookup individually or use **Stop all tasks**.

The default profile is `%LOCALAPPDATA%\ATLAS Next\profile`. History and exported receipts include request text in plaintext. Use public demonstration questions. Tests use new temporary profiles. At startup ATLAS preserves malformed records and interrupted writes under `quarantine` in the profile, grouped by recovery batch and reason. The window reports recovered tasks and the number of preserved files. Older receipts retain their original result and display “not checked” for verdicts they never recorded. The directory override `ATLAS_NEXT_PROFILE` is a trusted operator setting.

## Use a project document

Enter a short project name and choose **Open project**, then **Import TXT or Markdown**. The native picker authorizes one local file for that project. The app starts in `default`. Open a named project again to resume its history. The three demonstration handbook records are shared. Files and lookup history from other projects are excluded from the active lookup. Project scope is an application boundary within the owner's OS account.

For example, import a UTF-8 document containing `Signal colour: amber`, then ask “What is the signal colour?” Choose **Verify and finish** and **Export receipt**. The receipt identifies the project, source name, owned revision path, SHA-256 and the exact value span. Character positions are zero-based UTF-16 offsets, including any leading BOM and original line endings. Line numbers start at one. Re-importing the same selected path with new bytes creates another immutable revision. Selecting a different original path creates a distinct document.

Research & Verification reads explicit, single-line `Subject: value` or `Subject is value.` statements, including Markdown list items. Text in fenced code blocks supplies no claims. An optional `Valid through: yyyy-MM-dd` applies to the document. An undated claim is reported as undated. The supported forms include “What is …?”, “Name …” and “Which value is recorded for …?”. Competing values for the same recognized subject, expired evidence and insufficient support withhold delivery. A completed lookup reports what a source says. It does not independently establish the real-world truth of a statement or grant permission to execute it. The bounded grammar does not evaluate general prose, logical entailment, or all aliases and contradictions.

Each project accepts up to 32 local TXT/Markdown documents, each at most 256 KiB and 256 indexed claims. Plaintext copies and receipts stay in the local profile. Linked paths and malformed UTF-8 are rejected. Imports never enter model training.

## Evaluate actual lookup outcomes

After the Release checks build, run `python -B lab/outcomes.py` for the public, author-created suite. For a blind run, the custodian invokes `python -B lab/outcomes.py --manifest <private-manifest> --report <fresh-private-report>`. Keep both outside the checkout in custodian-controlled storage. The manifest pins a sibling UTF-8 JSON suite by SHA-256. [Public case format](lab/outcomes.public.json) · [Manifest example](lab/outcomes.manifest.json)

Each case has separate questions, source texts, expectations and provenance. Declare `author_fixture`, `custodian_fixture`, or `observed_project_outcome`. An observed outcome additionally requires an `outcome_receipt` with a sibling filename and SHA-256. An external path or matching digest alone cannot establish independent custody or empirical truth. The runner gives the isolated test process only the question and texts, scores the real engine's receipt and records failures, lookup latency and process memory. Custodians retain their suite, labels and detailed reports. Only independently attested results qualify as independent evidence.

## Build from a fresh checkout

Use an interactive Windows x64 desktop, 64-bit Windows PowerShell 5.1 or PowerShell 7, .NET SDK 10.0.401 (or compatible patch) with WindowsDesktop reference packs, and the matching .NET/WindowsDesktop 10.0.12 runtime folders for bundling. Package sources are cleared in NuGet.Config. From the repo root:

```powershell
dotnet build checks -c Release --configfile NuGet.Config
.\checks\bin\Release\net10.0-windows\AtlasNext.Checks.exe
.\tools\package.ps1
.\tools\verify-package.ps1 -Zip .\out\ATLAS-Next-win-x64.zip
```

On a host with a restricted default script policy, invoke each local script in its own process with `powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\tools\package.ps1` and `powershell.exe -NoProfile -ExecutionPolicy RemoteSigned -File .\tools\verify-package.ps1 -Zip .\out\ATLAS-Next-win-x64.zip`. For PowerShell 7, substitute the installed path to `pwsh.exe`. Both shells use the installed .NET x64 distribution under `%ProgramFiles%\dotnet` to build and bundle the app. Python and PowerShell 7 can be invoked by absolute path when absent from PATH.

The last check opens real test windows, exports a receipt and force-closes one disposable packaged test session. Packaging produces a fresh stage and ZIP under ignored `out/`. Build and test profiles are disposable. For research reproduction use Python 3.12.14 with NumPy 2.3.5 installed. In a disposable clone run `python -B lab/test_integrity.py`, `python -B lab/evaluate.py` and, for retraining, set `OPENBLAS_NUM_THREADS=1` and run `python -B lab/train.py`. Retraining rewrites model files and timing metadata there. App manifests are pinned to the released bytes. [Independent review assignment](docs/ai/ATLAS_HANDOFF.md)

[Owner report](docs/ai/ATLAS_OWNER_REPORT.md) · [Product vision](docs/PRODUCT_VISION.md) · [Architecture](docs/ARCHITECTURE.md) · [Decisions](docs/DECISIONS.md) · [Requirements](docs/REQUIREMENTS.md) · [Roadmap](docs/ROADMAP.md)
