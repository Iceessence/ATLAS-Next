# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | Independent review of Argon's NEXT-010 active project source inventory, then implement NEXT-011 |
| State | PENDING_INDEPENDENT_REVIEW |
| Next writer | GPT-6.2 Sol |
| Current product author | Gemini Argon Orchestrator |
| Independently accepted baseline | NEXT-009 product `f4db48313eefe6df27639d4a17c114cee6224d75` (verdict `37c273f65c56fec35b7962c747ba86291018460b`, [receipt](../evidence/independent-review-next009.json)) and NEXT-008R Sol literal repair `112f9fb071698b5d6b256184f3debda54dd9e305` (verdict `406ba1dcddeb4e3770d5ff1858a3f08e23fcf22c`, [receipt](../evidence/independent-review-next008r-sol.json)) |
| NEXT-010 candidate | Product `595ca3611a3ab9f6cd6cdd51851b45a4d0f10153`, author evidence `53378db48997d63e3f16b79a5f0736d76f98777f`, `PENDING_INDEPENDENT_REVIEW` |
| Offline distribution | `out/NEXT-010/ATLAS-Next-win-x64-ps51.zip`, `out/NEXT-010/ATLAS-Next-win-x64-ps7.zip`, and `out/ATLAS-Next-win-x64.zip`. [Sizes, exact SHA-256 and inventories](../evidence/next-010/package-inventory.txt) |

## Sol Phase A — Independent NEXT-010 review

1. Verify `595ca3611a3ab9f6cd6cdd51851b45a4d0f10153` in clean detached checkouts on Windows (`dotnet build checks -c Release --configfile NuGet.Config`, `AtlasNext.Checks.exe` expecting `268/268`, `python -B lab/test_integrity.py`, public controls/audits/shadow benchmarks, earlier `NEXT-005`..`NEXT-009` replays + `research_recheck.py` / `research_recheck_host.py`). Confirm `assets/native.json`, `assets/native.manifest.json`, and `lab/project-specialist.shadow.json` remain unchanged and `shadow`-only, and that `git diff 595ca361..53378db` contains docs/evidence only.
2. Independently challenge the `NEXT-010` Active Project Source Inventory & Status View (`desktop/Documents.cs`, `desktop/Engine.cs`, `desktop/Window.xaml`, `desktop/Window.xaml.cs`): test empty state, mixed `.txt`/`.md`/`.cs`/`.py` ordering, singular/plural indexed passage counts (`0`/`1`/`N`), idempotent and incremented revisions, live project isolation on `Open project`, persisted profile reopen, `>8` truncation (`(+N more)`), UIA `SourcesInfo` text readability (`AutomationProperties.Name` absent so `.Current.Name` equals `.Text`), fail-closed throwing in `Engine.ProjectSources()` on tampered/corrupted stored revisions or reparse-point checks, and non-crashing `WorkWindow` fallback notice.
3. Verify both shipped ZIPs in `out/NEXT-010` and fresh clean rebuilds sequentially (never concurrently) under Windows PowerShell `5.1.26100.9818` and PowerShell `7.6.9` using `tools/package.ps1` and `tools/verify-package.ps1`.
4. Commit your independent review receipt and verdict separately on local `main` (do not self-approve any future edit).

## Sol Phase B — Repair if needed, then NEXT-011

- If `NEXT-010` requires repairs, complete the repair and freeze it cleanly first.
- Once accepted (or after repair), select and implement bounded milestone `NEXT-011` within the roadmap and current owner authority, prioritizing practical coding assistance, offline research workflows, or independently evaluable native improvement where evidence qualifies.
- Freeze `NEXT-011` product and author evidence in separate local commits, build and verify sequential cross-shell Windows WPF packages, update `docs/ai/ATLAS_OWNER_REPORT.md` and `docs/ai/ATLAS_HANDOFF.md` (`PENDING_INDEPENDENT_REVIEW` -> `Gemini Argon Orchestrator`), keep host execution disabled (`read owner-imported text`), keep work offline/local (no `git push`), keep private evaluator expectations outside Git, keep native models `shadow`-only unless independently qualified, and leave `C:\ATLAS\Entire ATLAS` (`695585f`), PLATZ Engine, and FORGE untouched. [Sol-led protocol](ATLAS_REVIEW_PROTOCOL.md) · [Roadmap](../ROADMAP.md) · [Owner report](ATLAS_OWNER_REPORT.md)
