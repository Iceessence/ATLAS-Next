# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | Independently review NEXT-015R and the complete native-original NEXT-016 through NEXT-020 batch |
| State | PENDING_INDEPENDENT_REVIEW |
| Next writer | Gemini Argon Orchestrator |
| Preregistered native experiment | `409982b3ef3fdb85616286e2d9ddfdc8d3c7af03` · [Protocol](../../lab/native-first-protocol.md) |
| Integrated frozen product | NEXT-020 `6dbf726443e9afd3c527f99d4851440e1404f4bb` · [Clean replay and packages](../evidence/native-first-integration/README.md) |
| Last independently accepted product | NEXT-014 `81cc70eb0e1cff91c16b78d729acf82b5dab90cd` (advances accepted baseline from NEXT-009 `f4db48313eefe6df27639d4a17c114cee6224d75`; per-update acceptance covers `NEXT-010R`, `NEXT-011`, `NEXT-012`, `NEXT-013`, `NEXT-014`) |
| Reviewed transfer | `c548430ad2f2efe0bc54d0757de913a0899d78bb` |
| Independent batch receipt | [independent-review-next011-015.json](../evidence/independent-review-next011-015.json) |
| NEXT-010 original verdict (unchanged) | `REPAIRS_REQUIRED` on Argon `595ca3611a3ab9f6cd6cdd51851b45a4d0f10153`, Sol review `b358452bef7ca1624c00c24d91db60323cd85b2c`. [Receipt](../evidence/independent-review-next010.json) |
| NEXT-010R verdict | **`ACCEPTED_WITH_LIMITATIONS`** on `06f850119666c2f78ba850d8d17f63c1a4e87b5e` (author evidence `069309d1b77a7dfcec36d8ada2c8aa4a3cf3ec66`; `271/271` checks; `8/8` independent checks) |
| NEXT-011 verdict | **`ACCEPTED_WITH_LIMITATIONS`** on `3ae818874c19288b24df943fea4d7f0e4202273c` (+ `b9ea81c6`; evidence `f250e283...`, `41370d36...`; `276/276` checks; `7/7` independent checks; live `qwen3.8:27b` smoke `BRIDGE` -> `BRIDGE-LOCAL`) |
| NEXT-012 verdict | **`ACCEPTED_WITH_LIMITATIONS`** on `5e327ad9d52ee91117fad1bba718f65d8c066ac3` (+ `b9ea81c6`; evidence `1da748f5...`; `280/280` checks; `8/8` independent checks) |
| NEXT-013 verdict | **`ACCEPTED_WITH_LIMITATIONS`** on `80d6685914464261eacbe09365ec7cd8df9570e9` (evidence `ea3e5e9b...`; `285/285` checks; `10/10` independent checks; low non-blocking hygiene note `NR-013-1`) |
| NEXT-014 verdict | **`ACCEPTED_WITH_LIMITATIONS`** on `81cc70eb0e1cff91c16b78d729acf82b5dab90cd` (evidence `79238448...`; `285/285` checks; `3/3` lab gates; blocked fit exit `2`; `7/7` independent Python leakage/split gates) |
| NEXT-015 & overall batch verdict | **`REPAIRS_REQUIRED`** on `b9ea81c67e5d9269b4f6348088918e80a2561836` (via `1d77a604`, `37f0de92`; evidence `c8016fc8...`; `289/289` checks; `8/8` C# Workspace/UI checks; offline WPF PASS in PS 5.1 & PS 7.6.9; live `-ReferenceModel qwen3.8:27b` PASS through `BRIDGE-LOCAL` then fails `Shutdown timeout` at `Close-Session` due to `NR-015-1`) |

## Consolidated repair register

1. **`NR-015-1` [Medium — BLOCKING for `NEXT-015` live-reference packaged UIA workflow]:**
   - **Locations:** `desktop/ReferenceWindow.xaml.cs` (`ModelChanged`), `desktop/ReferenceWindow.xaml`, `tools/verify-package.ps1` (`Close-Dialog` lines 144–146 & 475–487).
   - **Verified defect:** Unmodified `tools/verify-package.ps1 -Zip <zip> -ReferenceModel qwen3.8:27b` passes `PASS packaged two-turn local reference-model conversation using qwen3.8:27b` (`BRIDGE` -> `BRIDGE-LOCAL`) sequentially under both Windows PowerShell `5.1.26100.9818` and PowerShell `7.6.9`, then fails at `tools/verify-package.ps1:169` (`Shutdown timeout`) in `Close-Session $session` right after `Close-Dialog $chat`. Expanding the `Model` `ComboBox` (`$picker.Expand()` + `Select-AtlasItem`) inside modal `ReferenceWindow` leaves `Model.IsDropDownOpen == true` (`Expanded`) and creates a Win32 `PopupRoot` HWND after which UIA `WindowPattern.Close()` returns without closing `ReferenceWindow`, blocking `WorkWindow.Close()`. Isolated `%TEMP%` proofs verified that posting `WM_CLOSE` (`0x0010`) in `Close-Dialog` closes `ReferenceWindow` immediately and completes the post-chat relaunch and shutdown checks in both shells.
   - **Required repair (`NEXT-015R`):** Set `Model.IsDropDownOpen = false;` in `ReferenceWindow.ModelChanged` (`desktop/ReferenceWindow.xaml.cs`) so keyboard/UIA selection cleanly dismisses the dropdown, and harden `Close-Dialog` in `tools/verify-package.ps1` (collapse open dropdown popups and fall back to Win32 `WM_CLOSE` `0x0010` when `WindowPattern.Close()` leaves the modal dialog open, waiting for dialog dismissal before calling `Close-Session`). Re-run unmodified `tools/verify-package.ps1 -Zip <zip> -ReferenceModel qwen3.8:27b` sequentially in Windows PowerShell 5.1 and PowerShell 7.
2. **`NR-013-1` [Low — Non-blocking programmatic API hygiene in `NEXT-013`]:**
   - **Locations:** `desktop/Engine.cs` (`Study`, `SubmitCore`, `FinishProjectAnswer`), `desktop/ResearchPlan.cs`.
   - **Finding:** `ResearchPlan.IsValid()` validates pipe-delimited `Fields` with `StringSplitOptions.TrimEntries`, and `ResearchWindow.Prepare` trims UI fields, whereas a direct programmatic `Engine.Study(plan)` call with trailing/leading spaces in `plan.Fields` enters `review` (`SubmitCore` trims `task.Request`) and fails closed (`"Project evidence failed authorization."`) at `Finish()` because `task.Plan.Request != task.Request`. Normalize `plan.Objective` and `plan.Fields` with `.Trim()` inside `Engine.Study` (or `ResearchPlan.Normalized()`).

## Frozen candidates and author evidence

The owner's native-original directive supersedes the suggested follow-up themes from the prior audit. The prior verdicts stand. NEXT-015R completed its local gates before the five-update implementation. Each row is pending independent review.

| Update | Frozen product | Author evidence revision | Scope and observed result |
| --- | --- | --- | --- |
| NEXT-015R | `9c48621fc3868a02291c100c85d2225e67b393ae` | `f62ab385995e84b28ae0cde7cc2477a16fc1c667` | Repairs NR-015-1 and NR-013-1. Sequential clean 5.1 and 7.6.9 live reference verification passed. [Evidence](../evidence/next-015r/README.md) |
| NEXT-016 | `a27ff64345547bff4f4182caced9907f712dde65` | `0c448fc47716e0978f6fd9aa1ed5a59e75706318` | 360 measured host actions, 60 questions and 15 public families. Eligibility and split hashing. [Evidence](../evidence/next-016/README.md) |
| NEXT-017 | `3eb40b96aca90bc8243086fd8e3b620c4b486970` | `06e666997124c58056213e75efb00276e9cb1153` | Random-init 65-parameter specialist. Useful 13/20 versus 16/20 host and TF-IDF, zero wrong. [Evidence](../evidence/next-017/README.md) |
| NEXT-018 | `d8ebe25bd719a477c9d6cfa73afd17e50afb964e` | `4c5c6a0ad98f3f60a57d4dc4c869240589c08a56` | Random-init 37-parameter Primary Master, trained with family-held-out specialist signals. Cooperation tied host and simpler gates at 16/20. [Evidence](../evidence/next-018/README.md) |
| NEXT-019 | `709e49736d3d6bf55db4d1414887016f52d15b60` | `867030d7222a4986961a5c27f24bab42f17d5394` | Real local read-only query, learned shadow proposals, independently authorized host evidence and receipts. [Evidence](../evidence/next-019/README.md) |
| NEXT-020 | `6dbf726443e9afd3c527f99d4851440e1404f4bb` | `fcbc8be91da112fd69e82bbbf02e78c7d379efd6` | New 65-parameter refit 13/20, rejected. Original weights retained, protected evaluator scoring path and app provenance report. [Evidence](../evidence/next-020/README.md) |

## Argon independent assignment

1. Review the repair, five frozen products and integrated product in clean checkouts. Reproduce both exact NEXT-015R live `qwen3.8:27b` package checks in PS 5.1 and PS 7.6.9. Reproduce offline WPF verification for the integrated product under both shells.
2. Reproduce the source audit and action acquisition, all three original checkpoint byte hashes, public results and refit rejection. Check permissions and request binding, held-family exclusion, provenance, malformed and duplicate input handling, negative retention, real no-reference task delivery, true specialist cooperation and continued host authority. Check whether stronger comparisons or coverage failures alter the claims.
3. Use a fresh protected suite only in evaluator custody. Score authorized unfamiliar-project requests and examine negative cases and contradictory evidence. Use the documented `lab/native_review.py` scorer for the frozen specialist and refit and keep private manifests, expected outcomes and reports outside Git. The author-public results do not supply qualification.
4. Record independent verdicts for the repair, each of the five milestones and the combined batch. Commit any bounded repair register and the next assignment through this handoff. Retain NEXT-014 as the latest independently accepted baseline until a new independent verdict.
5. Continue local-only. Host executable automation stays disabled, network stays offline except for the explicit localhost reference regression, native models stay shadow-only, and Windows privileges stay unchanged. No push. Leave legacy ATLAS, PLATZ Engine and FORGE untouched.

[Prior review evidence](../TEST_EVIDENCE.md#next-010r-and-next-011-through-next-015-independent-batch-review) · [Owner report](ATLAS_OWNER_REPORT.md) · [Batch protocol](ATLAS_REVIEW_PROTOCOL.md) · [Roadmap](../ROADMAP.md)
