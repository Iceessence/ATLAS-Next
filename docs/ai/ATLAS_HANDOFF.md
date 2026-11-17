# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | **NEXT-002** — file-first knowledge import & span citations, Research & Verification evidence checks, and independent outcome evaluation (following **NEXT-001R `ACCEPTED_WITH_LIMITATIONS`**) |
| State | AUTHOR_ASSIGNED |
| Prior review task | NEXT-001R independent re-review |
| Prior review verdict | `ACCEPTED_WITH_LIMITATIONS` |
| Implementation candidate reviewed | `87acbccf36d8c5e1916db84c1e4a2f2f122d3d14` on local `main` (`8f331fa8600726138d53d4859de8f47d3ffd5b77` author handoff) |
| Review baseline | `14e4c016581f7e4c7f1e49756925c1cd5f83d107` (NEXT-001 `REPAIRS_REQUIRED`) |
| Independent reviewer | Gemini Hydrogen |
| Next writer | GPT-6.2 Astra (implement NEXT-002, run author checks, and transfer back to Gemini Hydrogen with `State = PENDING_INDEPENDENT_REVIEW`) |
| Legacy isolation | Verified read-only: `C:\ATLAS\Entire ATLAS` at `695585fedc38c72d77bf355ea595f42fcf7a9e07` (`suite-results-hydrogen.json` untouched since `2026-11-17T11:21:48-08:00`); zero NuGet/source/Git dependencies |
| Independent receipts | [NEXT-001R review receipt](../evidence/independent-review-next001r.json) · [Prior NEXT-001 review](../evidence/independent-review-next001.json) · [Prior adversarial regression](../evidence/regression-adversarial-next001.json) |
| Approval & bounds | Bounded M1 foundation accepted with limitations (`shadow`-only native model; no executable automation; unsigned package). Native promotion, guest execution, Windows privilege changes, and edits to `C:\ATLAS\Entire ATLAS` remain unauthorized. |

## NEXT-001R Independent Re-Review Summary (`ACCEPTED_WITH_LIMITATIONS`)

**Environment:** Windows 11 Pro `10.0.26300` (Build `26300`, 64-bit), .NET SDK `10.0.401`, .NET Desktop Runtime `10.0.12`, Windows PowerShell `5.1.26100.9818` (`-ExecutionPolicy RemoteSigned`), cached `pwsh 7.6.9`, cached Python `3.12.14` / NumPy `2.3.5`, reviewed `2026-11-17T23:53:54Z`.

- **Clean-clone & source verification (`87acbcc` / `8f331fa`):** `87acbcc..8f331fa` changed only `docs/**`. `87acbcc` left `assets/native.json`, `assets/native.manifest.json`, `lab/*`, and `desktop/pins.json` unchanged, with `0` NuGet external libraries (`AtlasNext.deps.json`). In a fresh disposable clone, `dotnet build checks -c Release --configfile NuGet.Config` built with `0` warnings/`0` errors; `AtlasNext.Checks.exe` passed `40/40`; `lab/test_integrity.py` passed `3/3`; `lab/evaluate.py` reproduced exact parity (`rule` `48/72`, `exemplar` `42/72`, `native` `54/72`, `linear` `54/72`).
- **D1 verification (routing, relevance & authentic irrelevant citation rejection):** Both `Engine` and packaged `AtlasNext.exe` UI returned `fact.retention` (`30 days`) with `SourceIntegrity = passed`, `AnswerRelevance = supported`, and `Verification = source and answer checked` on `"How long is the archive retention policy?"` and unseen supported queries (`"How long are completed exports kept in our archive?"`, `"What is the encoding of an exported task receipt?"`, `"Show me the steps to recover my snapshot."`). Conflicting/wrong-subject/out-of-scope prompts returned `needs clarification`. Substituting authentic same-type `fact.format` into a pending retention review passed `SourceIntegrity` (`passed`) and withheld delivery on `AnswerRelevance` (`unsupported`, `State = needs clarification`, `Verification = answer withheld`) in both `Engine` and packaged UI; cross-type `procedure.restore` substitution failed `SourceIntegrity = failed`. Native model (`Qualification = shadow`) remained strictly advisory when disagreeing with `deterministic-route-v2`. Old NEXT-001 schema-1 receipt ([packaged-task.json](../evidence/packaged-task.json)) loaded byte-unchanged and displayed `Source integrity: not checked · Answer relevance: not checked` in packaged UI.
- **D2 verification (startup preflight & mixed corruption quarantine):** On an `IOException` lock preflight failure, `Engine` aborted before quarantining any file or committing any `"interrupted"` transition. After lock release, `Engine` (19 mixed bad/uncommitted files + stray `notes.txt` + legacy receipt + completed + review + queued) and packaged `AtlasNext.exe` (5 mixed bad/uncommitted files + legacy receipt + review + stray `notes.txt`) started cleanly, preserved completed/legacy receipts and `notes.txt`, transitioned pending work once to `interrupted`, quarantined every damaged/uncommitted file byte-for-byte under `quarantine\<batch>\<reason>\`, displayed exact counts, and reported `0` new interruptions/quarantines on relaunch.
- **D3 verification (PS 5.1 & PS 7 packaging/UIA):** Verified shipped `out/ATLAS-Next-win-x64.zip` (`94,789,796` bytes, SHA-256 `9aad994a249b3fc92cdfaa9c6b0fbb217f15de6ea9b48bd7d167f8c3e4d11281`) under `pwsh 7.6.9` and `out/ATLAS-Next-win-x64-ps51.zip` (`80,186,910` bytes, SHA-256 `ffc1bfe9b7cb6e1ad71a7ef9bec9087f4f4aeb59c51e24e907b7fa4e1c6404d7`) under Windows PowerShell `5.1` (all `491` `inventory.json` hashes identical across both ZIPs), plus fresh repackages in a disposable clone under both shells (including PS 5.1 `Replace` overwrite path), confirming private `coreclr.dll`, `ATLAS Next` window binding across launches, export, cancellation, crash recovery, and quarantine counts.

### Justified Acceptance Scope & Open Limitations

- **Accepted scope:** Bounded local M1 Windows 11 x64 evaluation foundation (3-record read-only demo handbook, deterministic route + whole-request relevance gating, advisory 4,163-parameter shadow CPU model, exclusive-lease fault-tolerant task journal with quarantine, offline portable ZIP packaging).
- **Open limitations carried into NEXT-002+:**
  1. Clean disconnected Windows host with no installed .NET `10.0.12` desktop runtime was not tested (workstation has .NET `10.0.12` installed globally, though private runtime path loading was confirmed).
  2. Package and PowerShell scripts are unsigned (default PowerShell `Restricted` policy requires `-ExecutionPolicy RemoteSigned`).
  3. Hyper-V guest preflight ([check-guest.ps1](../../tools/check-guest.ps1)) remains unauthorized (`Get-VM` denied; `executionAuthorized = false`); computer automation remains disabled.
  4. Native model remains `shadow`-only (`54/72` synthetic templates, tied with linear baseline; no independent evaluator custody or empirical task-outcome labels yet).
  5. `Relevance.Supports` relies on fixed per-revision regexes and conservatively returns `needs clarification` on valid paraphrases outside those patterns (e.g., `"Please state the archive retention duration."`, `"How are archives restored?"` where `"restored"` is missing from `Library.Find`, and `"How many days are exports retained?"`).

---

## Next Assignment for GPT-6.2 Astra (`NEXT-002`)

Keep the original ATLAS vision central: three complementary master capabilities (**Primary**, **Engineering & Execution**, **Research & Verification**), useful locally trained specialist models, file-first knowledge, verified self-improvement, and safe computer automation. Advance the foundation cleanly with this bounded **NEXT-002** increment:

1. **Invariants (must hold throughout NEXT-002):**
   - Keep `C:\ATLAS\Entire ATLAS` untouched/read-only.
   - Keep `atlas-native-route-v1` (and any new learned models) strictly `shadow`/advisory until independently measured outcome evaluation justifies promotion.
   - Keep executable/shell/guest automation disabled; do not modify Windows privileges or add external NuGet dependencies (`NuGet.Config` `<clear />`).
2. **File-First Knowledge Import & Exact Span Anchoring:**
   - Add owner-triggered local plain-text / Markdown document import in the desktop app (using native file selection, rejecting reparse points/links/oversized or invalid UTF-8 inputs, copying vetted files into an owned profile knowledge store with stable ID, version, SHA-256 digest, and rebuildable index alongside the 3 pinned demo records).
   - Anchor citations and verified results to exact file paths/IDs, revision digests, and passage line/character spans in the imported source file.
3. **Research & Verification Capability (Second Master Role):**
   - Introduce an explicit **Research & Verification** verification module beyond hardcoded 3-record regexes: evaluate claim-level evidence sufficiency, subject/intent match, freshness/trust metadata, and multi-source contradiction across pinned and imported files, surfacing **Primary** routing and **Research & Verification** verdicts distinctly in task receipts and WPF UI while failing closed (`needs clarification`) on ambiguity, missing support, or conflicting sources.
   - Address the minor lexical gap observed during review (`"How are archives restored?"` failing in `Library.Find` because `"restored"` was absent from `procedure.restore` matching) without weakening whole-request relevance safety.
4. **Independent Outcome Evaluation & Self-Improvement Readiness:**
   - Separate an outcome-grounded evaluation runner (`lab/`) that scores actual end-to-end retrieved record/span + verification outcomes (including hard negatives, contradiction pairs, and unfamiliar paraphrases, distinct from author `train.json` templates) and supports a custodian-supplied hidden suite path/manifest outside training inputs so Gemini Hydrogen can run independent blind outcome checks.
   - Keep rejected/negative trials and cost/latency measurements visible; train/evaluate any new compact original candidate only in `shadow` mode unless independent held-out outcomes surpass both the active deterministic policy and linear comparator.
5. **Build, Package, Verify & Transfer:**
   - Add comprehensive unit, adversarial contradiction/import, WPF/UIA, and Python integrity/evaluation tests; run offline Release build, `AtlasNext.Checks.exe`, Python tests/eval, and both Windows PowerShell 5.1 and PowerShell 7 `package.ps1` + `verify-package.ps1`.
   - Commit implementation and `docs/TEST_EVIDENCE.md` / receipts in distinct clean commits on local `main`, update this handoff (`State = PENDING_INDEPENDENT_REVIEW`, `Next writer = Gemini Hydrogen`, exact candidate hash, ZIP hashes, and open limits) and [ATLAS_OWNER_REPORT.md](ATLAS_OWNER_REPORT.md).
