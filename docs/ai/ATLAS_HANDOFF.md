# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-005 read-only local code conversation (Roadmap Option A) + frozen C baseline |
| State | ASSIGNED_FOR_DEVELOPMENT |
| Next writer | GPT-6.2 Astra |
| Accepted baseline | `0f57a164dc2f1437c633a7b574d350e4aced4642` (author transfer `3ae7a039031b476a325df6508bbcec1feadb8529`), NEXT-004 accepted with limitations |
| Independent review receipt | [`docs/evidence/independent-review-next004.json`](../evidence/independent-review-next004.json) |
| Windows PowerShell 5.1 ZIP (verified) | `out/ATLAS-Next-win-x64-ps51.zip`, SHA-256 `ab6960506785351502408a4899b556240f10996072f42788e2061634a211358f` (excludes lab shadow checkpoint) |
| Canonical PowerShell 7 ZIP (verified) | `out/ATLAS-Next-win-x64.zip`, SHA-256 `60edd2e022920f7ab9b2fb383815d17dfa23a1857804956cc1d4812f11fdcc30` (excludes lab shadow checkpoint) |
| Native routing model | `assets/native.json` (`6b9b23fc...`, 4,163 params) remains strictly `shadow` / advisory (54/72 synthetic routing) |
| Lab document specialist | `lab/project-specialist.shadow.json` (`5c476024...`, 49 params) remains strictly `shadow` outside ZIPs (15/16 public eval, 21/21 public stress, 21/24 fresh NEXT-004 blind) |

## Review Findings from NEXT-004 (Gemini Hydrogen)

1. **Verified progress:** Clean-clone rebuild (`0` warnings/errors, `0` NuGet dependencies), `91/91` Windows checks, `9/9` integrity tests, `21/21` public outcomes, `10/10` distribution audits, byte-exact `OPENBLAS_NUM_THREADS=1` refit (`5c476024...`), `16/16/14/15` public project outcomes, `21/21/20/21` public stress outcomes, `bb015a4` historical controls, and PS 5.1 + PS 7.6.9 packaging/UI verification all passed.
2. **Protected & fresh external challenge suites (`0` wrong deliveries across 57 external cases):** Replays passed `13/13` across all four policies on NEXT-002 and `18/20` on NEXT-003 (`native` abstentions dropped `4 -> 1`). On the new 24-case NEXT-004 external blind suite (`12` observed outcomes across unseen packages/builds + `12` fresh constructed controls), deterministic `host` and `tfidf` reached **`24/24`**, while `native` reached **`21/24`** (`3` false abstentions) and `linear` reached **`18/24`** (`6` false abstentions).
3. **Adversarial & cost findings:** `17/17` direct C# adversarial probes confirmed `# Installed payload audit` heading reset (`insufficient`), compound version constraint acceptance (`>=5.1,<5.5`), swapped citation/tamper/project isolation, and named-project corrupt recovery. Two non-unsafe false-abstention edge bugs were observed: `Routing.SearchTerms` dropping words (`package`, `audit`, `number`, `value`, `setting`, `installation`) causes longer co-existing claims to mismatch Research, and `Research.LiteralLookup` omitting `, please` misses collision disambiguation in favor of Primary/Research mismatch. Cold lookup latency increased from `~74–76 ms` (`NEXT-003`) to `~109 ms` (`~72.4 MiB` peak working set).

## Roadmap Selection & Bounded Assignment to GPT-6.2 Astra (NEXT-005)

Selected milestone from [Roadmap](../ROADMAP.md): **Option A — Local conversation and coding assistance (read-only and offline)**, with **Option C**'s independent scoring protocol frozen prior to any specialist refit, **Option B**'s citation/freshness hygiene preserved locally, and **Option D**'s execution disabled.

1. **Bounded source-file symbol lookup & scoped follow-up (Option A):** Allow an owner to select/import real project source files (e.g., C# and Python files under existing size/UTF-8/link/project boundaries, strictly read-only — no execution, no auto-editing) and run a two-step read-only project conversation:
   - Locate where a symbol (class/method/function) is defined, citing exact file revision, digest, declaration line, and UTF-16 span.
   - Answer a scoped follow-up in the active project (e.g., its parameters/signature or enclosing source declaration) with exact revision and line citations.
   - Safely withhold `ambiguous` (duplicate symbol definitions across files/types) or `unsupported` (unknown symbols, cross-project bleed, extra constraints) with **0 wrong deliveries**, and update cleanly across immutable new revisions when the selected source file changes.
2. **Cleanly fix the two verified false-abstention edge bugs without regression:** Align `Routing.ProjectFact` subject collision handling with `Research` (avoid stripping distinguishing qualifiers like `package`/`audit`/`number` when an exact or fuller match exists, or validate against exact subject match) and normalize `, please` before `LiteralLookup`, adding regression checks in `checks/` and `lab/outcomes.public.json`.
3. **Freeze the Option C baseline & local-search comparator first:** Establish an inspectable ordinary local-search baseline and record verified completions, wrong deliveries, safe abstentions, latency (cold & warm if measured), memory working set, and reference/external assistance across at least two unfamiliar codebases using public/author fixtures only (keeping Hydrogen's private evaluation suites untouched). Keep `assets/native.json` and `lab/project-specialist.shadow.json` strictly `shadow` / advisory.
4. **Package, verify, and return for review:** Rebuild both Windows ZIPs under Windows PowerShell 5.1 and PowerShell 7.6.9, run `verify-package.ps1` across both, update `checks/`, `lab/test_integrity.py`, `docs/TEST_EVIDENCE.md`, `docs/ai/ATLAS_OWNER_REPORT.md`, and this handoff (`State = PENDING_INDEPENDENT_REVIEW`, `Next writer = Gemini Hydrogen`). Commit code separately from transfer docs/evidence locally on `main` (do not push).

Keep `C:\ATLAS\Entire ATLAS`, PLATZ Engine, and FORGE untouched; keep executable automation disabled and Windows privileges unchanged.
