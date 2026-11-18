# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-006 Option A read-only code depth (`Type.Member` disambiguation & docstring/container follow-ups) |
| State | ASSIGNED_FOR_DEVELOPMENT |
| Next writer | GPT-6.2 Astra |
| Accepted baseline | `42bf5a6979540f5c6e3d0d59893b0b288b1f6300` (author transfer `14e3206df116db4db5a1d22bc5a4bafa83ab2225`, protocol `a219e95b3313ffbfc666ce8b764053e28f239abb`), NEXT-005 accepted with limitations |
| Independent review receipt | [`docs/evidence/independent-review-next005.json`](../evidence/independent-review-next005.json) |
| Windows PowerShell 5.1 ZIP (verified) | `out/ATLAS-Next-win-x64-ps51.zip`, SHA-256 `87bd4040ee05b8a2067db5d5bfa395d5c5287a3409199f04c63a0af2dbe6e103` (519 inventoried files incl. SDK Roslyn; excludes lab shadow checkpoint) |
| PowerShell 7 ZIP (verified) | `out/ATLAS-Next-win-x64.zip`, SHA-256 `40f2741548285b675e64d457375f10104e8984dd88dbfbafd2756623558ca568` (519 inventoried files incl. SDK Roslyn; excludes lab shadow checkpoint) |
| Model status | Desktop router `6b9b23fc...` (54/72 synthetic) and lab document specialist `5c476024...` (15/16 public eval, 28/28 controls, outside ZIPs) remain strictly `shadow` / advisory. |

## Review Findings from NEXT-005 (Gemini Hydrogen)

1. **Full independent reproduction passed (`0` wrong deliveries):** Clean-clone `117/117` Windows checks, `10/10` Python integrity, `28/28` public outcomes, `10/10` distribution audits, frozen `project_specialist.py evaluate` (`16/16/14/15` eval, `28/28/27/28` controls), `8/8` vs `6/8` public `code_navigation.py` Python conversations, and shipped + fresh PS 5.1 / PS 7.6.9 two-turn UI packaging runs all succeeded.
2. **Protected factual replays & new C#/Python code evaluation:** Replays passed `13/13` (NEXT-002), `18/20` (NEXT-003), and `24/24` (NEXT-004 `host`/`tfidf`). On Hydrogen’s fresh 16-conversation (`32`-turn) external suite across real unfamiliar Python & C# source files and constructed adversarial controls, ATLAS achieved **`16/16` (`20/20` deliveries, `12/12` abstentions, `0` wrong deliveries, `0` external inference)** vs ordinary regex search **`11/16` (`16/20` deliveries, `6` wrong deliveries)**. A 12-file third-party Python sample imported `11/12` cleanly (`>256` decls safely refused). `34/34` adversarial C#/Python probes passed.
3. **Observed limits driving NEXT-006:** Duplicate/overloaded names (`Type.Member`), doc/XML summary comments, and member container context remain unhandled (`ambiguous`/`unsupported`), locking multi-class and overloaded real source navigation once duplicate identifiers appear. Option D execution remains blocked without an inspectable isolated guest boundary, Option B requires a network isolation boundary, and Option C still lacks real Option A failure volume to justify refitting.

## Bounded Assignment to GPT-6.2 Astra (NEXT-006 — Continued Option A)

Advance **read-only local conversation & coding assistance** on owner-selected `.cs` and `.py` files (strictly offline, read-only, zero model promotion, preregistering any public comparator before implementation):

1. **Container-qualified symbol disambiguation (`Type.Member`) & bounded ambiguity feedback:**
   - Index enclosing type context on Roslyn C# declarations and bounded lexical enclosing `class` context on Python declarations so `"Where is TypeName.Member defined?"` / `"Locate TypeName.Member"` cleanly resolves a unique `Type.Member` match across multi-class projects, while genuinely ambiguous cases (e.g., overloaded `Type.Member` with same name in one class, or bare `Member` in multiple classes) still withhold delivery (`ambiguous`) and safely list conflicting locations cleanly without guessing.
2. **Scoped documentation/summary or enclosing-type follow-up on a verified declaration:**
   - Extend selected-parent code follow-ups to retrieve an attached XML doc comment (`///`) / Python docstring (when explicitly present on the verified active revision) or enclosing class/namespace container context, preserving exact parent-ID, active-revision, UTF-16 span, project-isolation, tamper, and swapped-citation checks (`0` wrong deliveries). Keep unsupported follow-ups cleanly withheld.
3. **Measure against an honest preregistered local-search comparator (Option A -> Option C prerequisite):**
   - Commit the public C# + Python baseline comparator BEFORE product changes, keeping Hydrogen's private evaluation suites untouched. Record verified completions, wrong deliveries, safe abstentions, cold and warm turn latency, working set, and external assistance across distinct unfamiliar C# and Python files, including C# overload / duplicate-class cases. Keep both native models `shadow` outside any promotion.
4. **Checks, dual-shell packaging & clean handoff:** Update `checks/`, `lab/test_integrity.py`, `lab/outcomes.public.json`, rebuild/verify both Windows PowerShell 5.1 and PowerShell 7.6.9 ZIPs, update `docs/TEST_EVIDENCE.md`, `docs/ai/ATLAS_OWNER_REPORT.md`, and this handoff (`State = PENDING_INDEPENDENT_REVIEW`, `Next writer = Gemini Hydrogen`). Commit protocol, product code, and transfer evidence cleanly on local `main` (do not push).

Keep `C:\ATLAS\Entire ATLAS`, PLATZ Engine, and FORGE untouched; keep executable automation disabled and Windows privileges unchanged.
