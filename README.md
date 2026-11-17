# ATLAS Next

Local Windows desktop research candidate. Ask the demonstration handbook about archive retention, restoring an archive, or export format. Each answer comes from a versioned file. The app compares its original CPU model with the active deterministic router and can export a receipt for every task.

## Run the packaged app

Extract the entire `out/ATLAS-Next-win-x64.zip` into a writable local folder and open **ATLAS Next.cmd**. This portable release includes its x64 .NET 10.0.12 runtime. No install, Python, GPU, model service or account is required. The package is unsigned. The author-tested target is Windows 11 Pro x64. [Build and package evidence](docs/TEST_EVIDENCE.md)

1. Choose **Find evidence** on the example request. Inspect the selected record, rule and native recommendation.
2. Choose **Verify and finish**. ATLAS reopens the file, checks the citation and evaluates whether that record supports the whole request. The window and exported receipt show separate source-integrity and answer-relevance verdicts. Try “How do I restore an archive?” for a procedure, or enter `fact.format` for an exact lookup.
3. **Export receipt** writes JSON under the profile’s exports directory. Close and reopen to see completed history. Stop a pending lookup individually or use **Stop all tasks**.

The default profile is `%LOCALAPPDATA%\ATLAS Next\profile`. History and exported receipts include request text in plaintext. Use public demonstration questions. Tests use new temporary profiles. At startup ATLAS preserves malformed records and interrupted writes under `quarantine` in the profile, grouped by recovery batch and reason. The window reports recovered tasks and the number of preserved files. Older receipts retain their original result and display “not checked” for verdicts they never recorded. The directory override `ATLAS_NEXT_PROFILE` is a trusted operator setting.

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
