# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | Repair blocking `NR-023-1` in `NEXT-023R` / `NEXT-024R`, re-verify integrated `NEXT-025` and sequential Windows packages, then provisionally advance into the next bounded native-intelligence batch |
| State | REPAIRS_REQUIRED |
| Next writer | GPT-6.2 Sol Extra High, by manual owner relay |
| Canonical product intent | [Master Plan v3.3](ATLAS_MASTER_PLAN.md) · [Startup and navigation](ATLAS_MASTER_PLAN_README.md) · [Pinned version](ATLAS_MASTER_PLAN_VERSION.json) |
| Documentation-only installation | f5f8f75a29ee02dc72da26dcc15a88fe14d42603 · **`ACCEPTED`** (docs only) · [Author checks](../evidence/next-021-025-integration/master-install.json) |
| Canonical SHA-256 | 8d32beb651deec0df5df8593f442801cfda9637ea5751f85b57e69064384b78b |
| Consulted sections | CH-015, CH-169, CH-172, CH-174, CH-175, CH-176, CH-182, CH-183, CH-184, CH-185, CH-186, CH-190, CH-192, APPENDIX-Q, APPENDIX-S, APPENDIX-V (plus all 301 indexed sections validated) |
| Last independently accepted product | NEXT-022 `cea141cf9cb0bc0c1d2918c97a60cc8aed903833` (packaged Windows desktop baseline remains equivalent to NEXT-020 `6dbf726443e9afd3c527f99d4851440e1404f4bb` pending `NEXT-023R`/`NEXT-024R` + integrated `NEXT-025`) |
| Latest independent verdict revision | Recorded with this commit · [Independent review receipt](../evidence/independent-review-next021-025.json) (reviewed on handoff `525a4441bcd49feb75c5f65585c0ab7fe781f70f`) |
| Native batch preregistration | ea2bac830121f310ab4d5292275a2e380732ea17 · [Comparison protocol](../../lab/next-021-025-protocol.md) |
| Integrated candidate under repair | 19f3f468b404d7642a2370a9eb19e6e5b2f4aff8 · [Review logs](../evidence/next-021-025-review/package-summary.json) |

## Independent per-update verdicts (Master v3.3 and `NEXT-021` through `NEXT-025`)

| Target | Product / doc revision | Author evidence | Clean & independent verification | Verdict |
| --- | --- | --- | --- | --- |
| Master v3.3 install | `f5f8f75a29ee02dc72da26dcc15a88fe14d42603` | `525a4441` (`master-install.json`) | ZIP `cbc7a3b0...` (`65` files + `SHA256SUMS.json`), `301` sections, `166/166` v3.2 chapters intact, `18` originals + `18` transcripts, `76` trace rows, `2/2` Py | **`ACCEPTED`** (documentation only) |
| `NEXT-021` | `7adf742aedc15997a8e81fc40240e64412a7fe59` | `e8dcc83ce17c72f307d0c6a160d674afee2e1787` | `0W/0E`, `291/291` Win, `16` Py, `504` actions (`14` families), exact manifest `ccc36ca7...` & 4 partition/knowledge SHA-256s; `NR-016-1` & `NR-020-1` (`b2fac610...`, `4/4`) resolved; `NR-021-1` low | **`ACCEPTED_WITH_LIMITATIONS`** |
| `NEXT-022` | `cea141cf9cb0bc0c1d2918c97a60cc8aed903833` | `c8849b7acc00c3f1d572cceb4d7a7d973638a3a6` | `0W/0E`, `291/291` Win, `3/3` Py, checkpoint `d8bfcc54...` (`494,787` B) exact match; `24/24` public; `19/24` protected (`2` wrong shadow, Brier `0.4168`, tied `linear` `19/24`, beat `exact_typed` `17/24`); `NR-022-1` med | **`ACCEPTED_WITH_LIMITATIONS`** |
| `NEXT-023` | `076627316c72fc51fbae087aea9cdffcc7c19fad` | `4573386bdaf42d09099ecd338158d7b6bcf9c936` | `0W/0E`, `291/291` Win, `3/3` Py, checkpoint `1834e999...` (`11,417` B) & live recovery reproduced; `24/24` public tied `lexical`; **blocking `NR-023-1`** drops Primary `withhold` in `rank_policy('procedure')` | **`REPAIRS_REQUIRED`** |
| `NEXT-024` | `eea0c32b8b0bea5496016bc53098eb6289618d47` | `8d02cb894de7bf5b089e69a6c16c354b02723a94` | `0W/0E`, `291/291` Win, `3/3` Py, `7e5e3e48...` (`12,898` B) & 6-step live compound reproduced; `21/24` protected tied `lexical`; **blocking `NR-023-1`** in `route()` executes `READ` on `withhold` + blocked by `NEXT-023` | **`REPAIRS_REQUIRED`** |
| `NEXT-025` (isolated / integrated) | `19f3f468b404d7642a2370a9eb19e6e5b2f4aff8` | `78d4e0ea52b861e7f6afc4a06123ba4bd9c53129` | `0W/0E`, `292/292` Win, `53/53` Py, refit `3c75904e...` (`497,487` B) rejected (`24/24` public Brier `0.040365`, `19/24` protected `0` gains Brier `0.4245`); `4/4` PS 5.1/7.6.9 WPF PASS (`522` inv). Blocked from baseline promotion by `NEXT-023`/`024` | **`REPAIRS_REQUIRED`** (integrated batch; `NEXT-025` **`ACCEPTED_WITH_LIMITATIONS`** in isolation) |

## Consolidated repair and hygiene register

1. **`NR-023-1` [High — Blocking in `lab/atlas_planner.py` (`rank_policy`) and `lab/atlas_team.py` (`route`, `evaluate`, `live_team`)]:** When Primary (`atlas_language.infer`) predicts `withhold`, `rank_policy("procedure")` looks up `knowledge["procedures"].get("withhold") -> None` and returns an active source read list, and `atlas_team.route()` emits Primary's `"withhold"` message yet returns an active `order` instead of stopping reads. Reproduced live on `pycparser` (`Avoid revealing the Release version for pycparser.`): Primary predicted `"withhold"` (`0.9983`), yet both `atlas_planner.py query --policy procedure` and `atlas_team.py query` executed `READ` and emitted `COMPLETED_SHADOW` (`"3.12"`) while the authoritative host returned `needs clarification`. Propagate Primary's explicit `withhold` veto out of `rank_policy()` and `route()` so planner/team abstain cleanly without executing `READ`. Add positive and negative unit/live tests covering Primary `withhold` propagation.
2. **`NR-022-1` [Medium — Non-blocking in `lab/atlas_language.py` (`disallowed`, lines 174–178) & host catalog precedence]:** Closed English regex list in `disallowed()` missed unfamiliar refusal/mutation verbs in protected adversarial tests (`exact_typed` `17/24` with `4` wrong shadow proposals; `native`/`linear` `19/24` with `2` wrong shadow proposals), and built-in handbook keys (`export format`) preempt project `.md` facts of the same key during host observation (`1` false abstention). Target in the next batch without keyword overfitting.
3. **`NR-021-1` [Low — Non-blocking in `lab/foundry_sources.py` (`typed_request`, lines 143–148)]:** `typed_request("locate_definition", key)` strips class qualifiers (`key.split(".")[-1]`), which would hit host `ambiguous` if multi-class Foundry `.py` sources ever share a method name (`Alpha.parse` vs `Beta.parse` verified ambiguous). Preserve dotted qualifiers when present.
4. **`NR-025-1` [Low — Observation in `lab/atlas_refit.py` (line 77)]:** `if diag["useful"] > control["useful"]:` vs `>=` (triggered cleanly on `11/12` vs `12/12` today; refine future intervention triggers in the next learning wave using held-out calibration and protected error analysis).
- *Resolved from prior review:* **`NR-016-1`** (`family_key()` in `native_data.validate_cases()` and `foundry.intake()`) and **`NR-020-1`** (`native_review.py --master / --master-sha`) verified resolved end-to-end.

## Next assignment for GPT-6.2 Sol

1. Freeze bounded repairs **`NEXT-023R`** and **`NEXT-024R`** fixing **`NR-023-1`** (and optionally addressing `NR-021-1` / `NR-022-1`), re-verify `NEXT-025` / integrated head (`0W/0E`, Windows checks, Python tests, Foundry/checkpoint/live-compound/refit replays, and sequential PS 5.1 & PS 7.6.9 `package.ps1` + `verify-package.ps1`), and return clean local evidence.
2. After local verification passes, Sol is provisionally authorized to proceed into the next bounded five-update batch aligned with Master Plan v3.3 `CH-190` Wave A/B/C/D, specifically targeting genuine learned improvements over `linear`/`lexical` baselines on harder paraphrased, compositional, or multi-step unfamiliar tasks rather than matching literal rules on single-key templates.
3. Keep all native models `shadow`-only, host execution disabled (`read owner-imported text`), protected evaluator suites outside Git/training, legacy ATLAS (`695585f`)/PLATZ/FORGE untouched, and NO `git push`. Manual owner relay only.

[Owner report](ATLAS_OWNER_REPORT.md) · [Review protocol](ATLAS_REVIEW_PROTOCOL.md) · [Roadmap](../ROADMAP.md)
