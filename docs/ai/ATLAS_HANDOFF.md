# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-001R — repair misrouted relevance verification, startup/journal corruption blocking, and packaging harness race (following NEXT-001 independent review) |
| State | REPAIRS_REQUIRED |
| Prior author | GPT-6.2 Astra |
| Independent reviewer | Gemini Hydrogen |
| Next writer | GPT-6.2 Astra (implement bounded repairs and author verification, then transfer back to Gemini Hydrogen for re-review) |
| New repo | `C:\ATLAS\ATLAS-Next`, standalone `main`, no legacy Git parent (`81866215b829ec1a7c016971d93e3cf7f7ed20f1`) |
| Reviewed candidate | `7d319bc33be94c747f15fbc6f52d873379b51282` (code/assets/tools) and `72799cad478df3deac467e4bb5c72e364ac571d2` (author docs/evidence HEAD prior to review commit) |
| Legacy isolation | Verified read-only: `C:\ATLAS\Entire ATLAS` at `695585fedc38c72d77bf355ea595f42fcf7a9e07` with only pre-existing unstaged `suite-results-hydrogen.json` (`2026-11-17T11:21:48-08:00`); zero NuGet/source/Git dependencies |
| Independent evidence | [Independent review receipt](../evidence/independent-review-next001.json) · [Adversarial regression evidence](../evidence/regression-adversarial-next001.json) |
| Approval | Initial standalone development remains authorized for bounded NEXT-001R fixes only. Native model stays in `shadow` mode. Automation, VM privileges, hidden-evaluator custody, and release signing remain unapproved/blocked. |

## NEXT-001 Independent Review Verdict (`REPAIRS_REQUIRED`)

**Environment:** Windows 11 Pro `10.0.26300` (Build `26300`, 64-bit), .NET SDK `10.0.401`, .NET Desktop Runtime `10.0.12`, PowerShell `5.1.26100.9818` (system default), cached `pwsh 7.6.9` (`C:\Users\ppawe\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe`), cached Python `3.12.14` / NumPy `2.3.5` (`C:\Users\ppawe\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`), reviewer `Gemini Hydrogen`, reviewed at `2026-11-17T23:32:51Z`.

### Independently Verified Passing Scope

- **Candidate-to-HEAD & dependency audit:** `7d319bc..72799ca` modified only 13 doc/evidence files (`README.md`, `docs/**`). `AtlasNext.deps.json` and `project.assets.json` contain zero NuGet packages (`"libraries": {}`) and zero references to `Entire ATLAS`.
- **Disposable-clone build & checks (`7d319bc`):** `dotnet build checks -c Release --configfile NuGet.Config` succeeded (`0` warnings, `0` errors); `AtlasNext.Checks.exe` passed `21/21` (`0` failed, `0` skipped); `lab/test_integrity.py` passed `3/3`.
- **Training & evaluation reproduction (`OPENBLAS_NUM_THREADS=1`):** Byte-exact reproduction of `assets/native.json` (`6b9b23fcf890807f2a1d989f1b9870660bcf3030ce6e729674844e9f722076c4`) and `lab/linear.json` (`bc96bb6544593030ba2d24304747f6eb68030aa6c37dd175e88b8db16d0fea8e`). `lab/evaluate.py` matched `docs/evidence/public-routing-evaluation.json` on all counts, confusion matrices, accuracy (`rule` `48/72` `66.7%`, `exemplar` `42/72` `58.3%`, `native` `54/72` `75.0%`, `linear` `54/72` `75.0%`), macro-F1, and error lists (only p50/p95 timings differed: `native` p95 `24.1 µs` vs author `60.5 µs`). Note on label provenance: `origin = "rule_label"` reflects author template-intent assignment, NOT `corpus.rule()`, which disagrees with labels on `8/120` train and `24/72` holdout rows.
- **Package contents & isolated security probes (`20/20`):** Shipped `out/ATLAS-Next-win-x64.zip` (`94,784,782` bytes, SHA-256 `ff63a8c90ccd6df0191e8af78eb2ec4c9ba60ef71ce287d4f748f5b5ef8815a1`) verified all `491` `inventory.json` hashes; clone rebuild under `pwsh 7.6.9` (`7b885e28...`) matched `489/491` file hashes (only `AtlasNext.dll` and `AtlasNext.pdb` differed). When targeting the real `ATLAS Next` UIA window, the packaged app loaded private `coreclr.dll` from extracted `runtime\`, completed/exported receipts, survived reopen, executed individual/global stops, blocked a second writer on `session.lock`, and recovered kill-interrupted tasks as `interrupted`. All 20 boundary probes passed (out-of-scope prompts, 2048/2049-char budget, whitespace refusal, unpinned `rogue.json` ignored, tampered `native.manifest.json` rejected, tampered `task.Evidence` rejected, shadow `Proposal` unable to override deterministic `Specialist` or `Authorization`).
- **Guest preflight:** `tools/check-guest.ps1 -Name ATLAS-Next-Offline-Review` failed read-only `Get-VM` with authorization denial; automation remains disabled (`executionAuthorized = false`). Clean disconnected Windows OS without installed .NET 10 runtime and production signing remain untested.

### Unresolved Blocking Defects

1. **`NEXT-001-D1` (Critical — Misrouted Retrieval Marked "Verified" / Source Integrity Conflated with Answer Relevance):**
   - **Reproduced in `Engine` and packaged `AtlasNext.exe` UI:** Query `"How long is the archive retention policy?"` is routed by `Routing.Rule` (`desktop/Routing.cs:51-54`) to `Specialist = "procedure"` because `"how"` is checked before `"retention"`/`"policy"` (while the shadow native model proposed `"fact"` at `98.0%`). `Library.Find` (`desktop/Library.cs:32-39`) filters to `Type == "procedure"` and selects `procedure.restore` solely on the shared generic token `"archive"` (`score = 1`). `Engine.Finish` (`desktop/Engine.cs:52-55`) checks only file hash, scope, and `task.Specialist == source.Record.Type`, marking the task `State = "completed"` with `Verification = "release hash, scope and version checked"` and returning archive restoration steps instead of the 30-day retention policy.
   - **Symmetric/related failures reproduced:** `"How long will the archive retention policy keep exports?"`, `"How many days is archive retention?"`, and `"How long will atlasprint keep its archive?"` all return `procedure.restore` marked verified; `"What is the archive restoration?"` and `"What is the retention policy for uranium?"` both return `fact.retention` (`30 days`) marked verified; `"How is an export receipt formatted?"` and `"Name the archive format."` fail to retrieve `fact.format`.
2. **`NEXT-001-D2` (Critical — Single Corrupt Record or Invalid Filename Bricks Application Startup & Partially Mutates History):**
   - **Reproduced in `Engine`/`Journal`/`WorkWindow` and packaged `AtlasNext.exe`:** A single malformed JSON `<guid>.json`, 0-byte/truncated JSON, `null`, `{}`, schema/ID mismatch, `events: null`, or invalid filename (`corrupt.json`, hyphenated `<guid-D>.json`) causes `Journal` constructor (`desktop/Journal.cs:34-56`) to throw, showing `"ATLAS Next startup failed"` and exiting (`1`) while blocking access to all healthy tasks.
   - **Partial mutation hazard:** Because `History()` iterates lazily inside `Journal` constructor, when a valid `review` task precedes a corrupt record in directory enumeration, `Journal` mutates the valid `review` record to `"interrupted"` on disk before crashing on the corrupt file (`review_ordered_before_corrupt_partial_mutation`).
   - **Post-Journal crashes & `.pending` orphans:** Valid JSON with `"events": []` passes `Journal` constructor and crashes `Engine.History()` / `WorkWindow` startup (`Sequence contains no elements` in `Engine.cs:11`); short `Proposal.Sha256` (< 16 chars) crashes `WorkWindow.ShowTask` (`Window.xaml.cs:35`). Orphaned `<guid>.json.pending` files without a committed `<guid>.json` are silently ignored and remain orphaned in `tasks/` (though `<guid>.json` in `queued`/`review` paired with `.pending` safely transitions to `interrupted` while leaving `.pending` behind).
3. **`NEXT-001-D3` (Major — `verify-package.ps1` UIA Window Race & `package.ps1` PowerShell 5.1 Failure):**
   - Unmodified `tools/verify-package.ps1` failed on both independent runs (`Missing control Status` on freshly packaged clone ZIP; `Missing control Search` at line 53 on shipped repo ZIP). `Launch()` matches `$_.MainWindowHandle -ne 0`, binding to `UAC_InputIndicatorOverlayWnd` (`Title=''`, `0` UIA descendants) instead of waiting for `WorkWindow` (`Title='ATLAS Next'`).
   - `tools/package.ps1` failed under system default Windows PowerShell `5.1.26100.9818` because `[IO.Path]::GetRelativePath` is unavailable on .NET Framework 4.x (`pwsh 7.6.9` and Python `3.12.14` exist only in local runtime cache, not on `PATH`).

---

## Next Assignment for GPT-6.2 Astra (`NEXT-001R`)

Keep the original ATLAS vision central: three complementary master capabilities (Primary, Engineering & Execution, Research & Verification), useful locally trained specialist models, file-first knowledge, verified self-improvement, and safe computer automation. Do not jump ahead to M2/M4 features during this repair pass; fix the three defects above cleanly and boundedly, then return the exact repair candidate for independent re-review:

1. **Preserve strict boundaries:** Keep `C:\ATLAS\Entire ATLAS` untouched/read-only, keep `native` model in `shadow` mode, keep automation/guest execution disabled, add zero external NuGet dependencies (`NuGet.Config` `<clear />`), and do not alter Windows privileges.
2. **Fix `NEXT-001-D1` (Intent Routing, Relevance Checking & Honest Verification):**
   - Separate **source integrity verification** (release SHA-256, scope, revision) from **answer-relevance / intent verification** in `Work` receipts, UI status/details, and `Engine.Finish`. Never stamp `"verified"`/`"completed"` without checking whether the retrieved record satisfies the query's actual intent and subject scope.
   - Fix `Routing.Rule` (`desktop/Routing.cs`) and `Library.Find` (`desktop/Library.cs`) so phrases like `"how long"`, `"how many"`, `"retention"`, `"policy"`, `"duration"`, `"format"` route to `fact` rather than matching bare `"how"` as `"procedure"`; evaluate catalog matches without letting a single shared noun (`"archive"`) lock in a record from the wrong type or ignore competing evidence/out-of-corpus subject mismatch (`"uranium"`), safely returning `"needs clarification"` on conflict or insufficient relevance.
   - Add automated regression tests in `checks/Program.cs` covering `"How long is the archive retention policy?"`, `"How long will the archive retention policy keep exports?"`, `"How many days is archive retention?"`, `"How is an export receipt formatted?"`, `"What is the archive restoration?"`, and `"What is the retention policy for uranium?"`.
3. **Fix `NEXT-001-D2` (Fault-Tolerant Startup, Corrupt Record Quarantine & Journal Reconciliation):**
   - Update `Journal` (`desktop/Journal.cs`), `Engine` (`desktop/Engine.cs`), and `WorkWindow` (`desktop/Window.xaml.cs`) so malformed JSON, truncated/0-byte JSON, `null`/`{}`, schema/ID mismatch, empty/null `events`, short `Proposal.Sha256`, or invalid filenames in `profile\tasks\` are **quarantined** (moved cleanly to e.g. `profile\quarantine\` or isolated with an operator notice) rather than crashing startup or hiding healthy history.
   - Materialize/validate all readable tasks **before** persisting `"interrupted"` transitions on startup so partial recovery mutations cannot happen if another file is corrupt.
   - Reconcile and clean up leftover `.json.pending` files deterministically on startup (surfacing quarantined/recovered counts visibly in `WorkWindow` without crashing).
   - Add automated regression tests in `checks/Program.cs` and packaged startup tests covering malformed JSON alongside valid completed/review tasks, invalid filenames, `events: []`, short `Proposal.Sha256`, and orphaned `.json.pending` writes.
4. **Fix `NEXT-001-D3` (Packaging & UIA Verification Scripts):**
   - Fix `tools/verify-package.ps1` `Launch()` to resolve the actual `ATLAS Next` window (`ProcessId` + UIA `Name == "ATLAS Next"` / `MainWindowTitle == "ATLAS Next"`), eliminating the `UAC_InputIndicatorOverlayWnd` race, and add packaged verification coverage for `"How long is the archive retention policy?"` and corrupt-profile startup resilience.
   - Make `tools/package.ps1` work cleanly under both Windows PowerShell 5.1 and PowerShell 7 (load `System.IO.Compression.FileSystem`, avoid `[IO.Path]::GetRelativePath`, write BOM-free UTF-8 `inventory.json`), and document exact tool/runtime assumptions accurately in `README.md`.
5. **Re-verify & Transfer:**
   - Rebuild offline (`dotnet build checks -c Release --configfile NuGet.Config`), run `AtlasNext.Checks.exe`, `lab/test_integrity.py`, `lab/evaluate.py`, `tools/package.ps1`, and `tools/verify-package.ps1`.
   - Record updated receipts in `docs/TEST_EVIDENCE.md` and `docs/evidence/`, commit code and docs cleanly on `main`, update this file (`State = PENDING_INDEPENDENT_REVIEW`, `Next writer = Gemini Hydrogen`, exact new candidate commit, ZIP hash, and pass/fail counts), and refresh `docs/ai/ATLAS_OWNER_REPORT.md`.

### Post-Repair Milestone (`NEXT-002`, gated on `NEXT-001R` independent `ACCEPTED`)

Once `NEXT-001R` passes independent review, the next milestone advances the original ATLAS vision:
- **Independent evaluator custody & real benchmark:** Separate custodian-held outcome evaluation (beyond author synthetic templates) before any native model promotion.
- **Second master capability (Research & Verification):** Implement explicit evidence-sufficiency / contradiction / relevance verification checks complementing Primary routing.
- **File-first knowledge expansion:** Authorized local TXT/Markdown document import with stable IDs and exact span citations.
- **Verified self-improvement & safe automation:** Stand up the bounded weakness-to-experiment loop and owner-provisioned offline Hyper-V guest inspection (`M2`) before enabling any computer execution.
