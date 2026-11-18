# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | **NEXT-003** — eligible project-outcome collection, Primary-Research routing alignment, and shadow evaluation of project-aware native specialists (following **NEXT-002 `ACCEPTED_WITH_LIMITATIONS`**) |
| State | AUTHOR_ASSIGNED |
| Prior review task | NEXT-002 independent review |
| Prior review verdict | `ACCEPTED_WITH_LIMITATIONS` |
| Implementation candidate reviewed | `c1b09d61adc31871d9c3fa818e7f3b3c56c3b4cc` on local `main` (`7ef54fe5077a7ae4670e84769b0c98178e298dd1` author evidence transfer) |
| Prior review baseline | `4d9a6370815321878d4672200dcec49fed5379f4` (NEXT-001R `ACCEPTED_WITH_LIMITATIONS`) |
| Independent reviewer | Gemini Hydrogen |
| Next writer | GPT-6.2 Astra (implement NEXT-003, run author verification, and transfer back to Gemini Hydrogen with `State = PENDING_INDEPENDENT_REVIEW`) |
| Legacy & sibling isolation | Verified read-only: `C:\ATLAS\Entire ATLAS` untouched at `695585fedc38c72d77bf355ea595f42fcf7a9e07`; PLATZ Engine and FORGE untouched; zero external NuGet libraries |
| Independent evidence | [NEXT-002 review receipt](../evidence/independent-review-next002.json) · [Author test evidence](../TEST_EVIDENCE.md) · [Prior NEXT-001R receipt](../evidence/independent-review-next001r.json) |
| Approval & bounds | Bounded NEXT-002 local foundation accepted with limitations (`shadow`-only native model; executable automation disabled; unsigned package). Native promotion, guest execution, Windows privilege changes, and edits to legacy ATLAS, PLATZ Engine, or FORGE remain unauthorized. |

## NEXT-002 Independent Review Summary (`ACCEPTED_WITH_LIMITATIONS`)

**Environment:** Windows 11 Pro `10.0.26300` (Build `26300`, 64-bit), .NET SDK `10.0.401`, .NET Desktop Runtime `10.0.12`, Windows PowerShell `5.1.26100.9818` (`-ExecutionPolicy RemoteSigned`), cached `pwsh 7.6.9`, cached Python `3.12.14` / NumPy `2.3.5`, reviewed `2026-11-18T04:00:20Z`.

- **Candidate-to-HEAD & clean-clone build/checks (`c1b09d6` / `7ef54fe`):** `c1b09d6..7ef54fe` added only `docs/**`. Pinned routing assets/weights and `desktop/pins.json` remained unchanged, with `0` NuGet external libraries in `AtlasNext.deps.json`. In a fresh disposable clone: `dotnet build checks -c Release --configfile NuGet.Config` built with `0` warnings/`0` errors; `AtlasNext.Checks.exe` passed `63/63`; `lab/test_integrity.py` passed `5/5`; `lab/evaluate.py` (`OPENBLAS_NUM_THREADS=1`) reproduced `rule` `48/72`, `exemplar` `42/72`, `native` `54/72`, `linear` `54/72` (`native` stays `shadow`); `lab/outcomes.py` passed `10/10` public outcomes.
- **Cross-shell packaging & packaged UI (`5.1` & `7.6.9`):** Shipped `out/ATLAS-Next-win-x64.zip` (`94,808,548` bytes, SHA-256 `ca8295abf60b120d8c66f483a87ca05ec4435cb94ce7cbbf59c13957bc6406fe`) passed under `pwsh 7.6.9`; `out/ATLAS-Next-win-x64-ps51.zip` (`80,202,719` bytes, SHA-256 `5ed0da16ba1a0066a614e936aa83972cac8c3c4fa81e97e4177639547c90f3f5`) passed under Windows PowerShell `5.1`; fresh clone repackages passed under both shells. Independent packaged UI automation verified named project switching (`alpha-proj` / `beta-proj`), native file-picker import of UTF-8 BOM + CRLF + surrogate emoji Markdown (`line = 2`, UTF-16 `[27, 36)`, stored bytes 100% identical to original, SHA-256 matched), re-import immutable `revision 2`, isolation from post-import source edits, project separation, insufficient `TBD` withholding, and expired `Valid through` withholding.
- **Adversarial boundary & Research probes (`26/26` boundary/import, `9/9` Research/delivery):** Verified rejection of source ancestor junctions, profile-root ancestor junctions before store creation, hard links, UNC paths, ADS streams, truncated UTF-8, control characters, empty/oversize (`256 KiB + 1`) files, `>256` claims, `>32` documents, bad/duplicate `Valid through` lines, and invalid project names (unprivileged file symlink creation skipped by host OS privilege policy). Verified same-source contradiction, `TBD`/`n/a`/`unknown` insufficiency, >8 passage ambiguity capped at 8, source tamper/staleness/contradiction/citation-substitution between `review` and `Finish`, Research independence from Primary and shadow model, and NEXT-001R quarantine + legacy schema-1 preservation.
- **Custodian blind outcome suite (external storage, private inputs retained):** Suite SHA-256 `d9ca9752f9134151adb44c5fd85ae380dc206f4365c7a3aefbc170eece1649de` scored **`12/13`** (`10/11` independently constructed `custodian_fixture` cases; `2/2` `observed_project_outcome` cases backed by real clean-clone `dotnet build` (`51762ac6...`) and `AtlasNext.Checks.exe` (`97fdc2f7...`) receipts). Costs across 13 isolated runs: `51.9–70.2 ms` (`51,939.5–70,230.2 µs`) engine lookup, `187.7–217.6 ms` total process time, `50.99–53.90 MB` working set.
  - **Representative failure (`cust-procedure-keyword-in-fact-subject`):** When an imported fact subject contains a word from Primary `Routing.Rule`'s procedure keyword list (e.g. `restore`), Primary routes to `procedure` and skips `documents.Active()` (`Engine.cs:42`), so `Engine` safely withholds (`needs clarification`, `"Primary could not resolve a source for this request."`) even though `Research.Assess` independently reports `supported` with the exact span.

### Justified Acceptance Scope & Open Limitations

- **Accepted scope:** Bounded NEXT-002 local Windows 11 x64 evaluation foundation (3-record handbook + project-scoped immutable TXT/Markdown revisions with exact UTF-16 spans, deterministic Research & Verification claim/contradiction/staleness/sufficiency checks, shadow-only `4,163`-parameter CPU model, external custodian outcome harness, and offline portable ZIP packaging).
- **Open limitations:**
  1. Primary routing vs `Engine` imported-claim handoff causes safe false-abstention when a requested imported fact subject contains a procedure keyword (`12/13` custodian blind outcomes).
  2. Combining a definite value with an `undetermined` value across two active sources reports `contradiction` rather than partial/unconfirmed support (still safely withheld).
  3. Bounded grammar supports explicit single-line `Subject: value` / `Subject is value.` statements and affirmative lookup forms only (no general prose, aliases, or real-world truth attestation).
  4. Native CPU model stays `shadow`-only (`54/72` synthetic routing, tied with linear comparator); no learned policy promotion authorized.
  5. Clean disconnected Windows OS without .NET `10.0.12` runtime, release signing, unprivileged file symlink creation, and Hyper-V guest isolation (`Get-VM` denied; `executionAuthorized = false`) remain untested/blocked.

---

## Next Assignment for GPT-6.2 Astra (`NEXT-003`)

Keep the original ATLAS vision and PLATZ Technologies separation central: three complementary master capabilities (**Primary**, **Engineering & Execution**, **Research & Verification**), useful locally trained specialist models, file-first knowledge, verified self-improvement, and safe computer automation. Advance in dependency order with this bounded **NEXT-003** increment:

1. **Invariants:** Keep `C:\ATLAS\Entire ATLAS`, PLATZ Engine, and FORGE untouched; keep executable automation disabled and Windows privileges unchanged (`tools/check-guest.ps1` read-only); add zero external NuGet dependencies; keep all learned models strictly `shadow`/advisory until independent blind outcome superiority over deterministic and linear baselines is established.
2. **Resolve the Primary-Research Imported Fact Routing Gap (`cust-procedure-keyword-in-fact-subject`):**
   - Ensure Primary (`Routing.Rule` / `Engine.Submit`) resolves matching imported project fact claims for affirmative fact queries even when the subject includes words like `restore`, `configure`, `guide`, `recovery`, or `steps`, while preserving safe abstention on handbook procedure-vs-fact ambiguity (`"What is the archive restoration?"` -> `needs clarification`) and existing `NEXT-001R` regressions.
   - Distinguish combining a definite source with an `undetermined` source cleanly (either preserving an explicit insufficient/unconfirmed diagnostic or documenting it clearly) without allowing delivery.
3. **Expand Eligible Project-Outcome Collection & Multi-Source Tasks:**
   - Grow non-template `observed_project_outcome` and realistic multi-document project corpora in `lab/` (e.g., build/test/config/manifest facts, procedure-vs-fact selection across real project docs, contradiction/freshness suites) with explicit provenance tags, source receipts, and disjoint project-family train/validation/eval splits. Keep custodian blind inputs external (`--manifest` / `--report`).
4. **Project-Aware Native Specialist Shadow Experiments (No Promotion):**
   - Implement and benchmark compact, original CPU-trainable specialist candidate(s) (e.g., project-aware Primary routing or Research claim/relevance/sufficiency scorer) trained exclusively on eligible non-custodian project outcomes against strong deterministic and linear/TF-IDF comparators on the full end-to-end outcome harness (`lab/outcomes.py` + held-out families).
   - Record all seed trials, negative/tied comparisons, compute/memory costs, and calibration/abstention metrics under `lab/` and `docs/TEST_EVIDENCE.md`, keeping deterministic host verification authoritative in `Engine`.
5. **Verify & Transfer:**
   - Add automated C# (`checks/Program.cs`), Python (`lab/test_integrity.py`), and packaged UIA tests; run offline Release build, `AtlasNext.Checks.exe`, `lab/test_integrity.py`, `lab/evaluate.py`, `lab/outcomes.py`, and both Windows PowerShell `5.1` and PowerShell `7` `package.ps1` + `verify-package.ps1`.
   - Commit implementation and evidence separately on local `main`, update this file (`State = PENDING_INDEPENDENT_REVIEW`, `Next writer = Gemini Hydrogen`, exact candidate hash, ZIP hashes, and open limits) and [ATLAS_OWNER_REPORT.md](ATLAS_OWNER_REPORT.md).
