# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | Develop the next bounded five-update batch (`NEXT-021` through `NEXT-025`) targeting the verified host grammar & positive-acquisition bottleneck while fixing non-blocking hygiene items `NR-016-1` and `NR-020-1` |
| State | ACCEPTED_WITH_LIMITATIONS |
| Next writer | GPT-6.2 Sol |
| Last independently accepted product | NEXT-020 `6dbf726443e9afd3c527f99d4851440e1404f4bb` (advances accepted baseline from NEXT-014 `81cc70eb0e1cff91c16b78d729acf82b5dab90cd`; covers `NEXT-015R` and `NEXT-016` through `NEXT-020`) |
| Reviewed transfer | `797c5c78dc4551d9c4e205752797bc656be9d93a` |
| Preregistered native experiment | `409982b3ef3fdb85616286e2d9ddfdc8d3c7af03` · [Protocol](../../lab/native-first-protocol.md) |
| Independent batch receipt | [independent-review-next015r-020.json](../evidence/independent-review-next015r-020.json) |
| Prior review history | `NEXT-010R`..`NEXT-014` accepted at `0f81225daa12b5bae0638d24203f312dcf412693` ([receipt](../evidence/independent-review-next011-015.json)); `NEXT-010` original `REPAIRS_REQUIRED` at `b358452bef7ca1624c00c24d91db60323cd85b2c` ([receipt](../evidence/independent-review-next010.json)) |

## Independent per-update verdicts (`NEXT-015R` & `NEXT-016` through `NEXT-020`)

| Update | Frozen product | Author evidence | Clean & independent verification | Verdict |
| --- | --- | --- | --- | --- |
| `NEXT-015R` | `9c48621fc3868a02291c100c85d2225e67b393ae` | `f62ab385995e84b28ae0cde7cc2477a16fc1c667` | `0W/0E`, `290/290` Win, `14+3` Py, `4/4` shipped+rebuilt PS 5.1/7.6.9 live `qwen3.8:27b` WPF PASS (`520` inventory), `3/3` C# `ResearchPlan` probes PASS (`NR-015-1` & `NR-013-1` resolved) | **`ACCEPTED_WITH_LIMITATIONS`** |
| `NEXT-016` | `a27ff64345547bff4f4182caced9907f712dde65` | `0c448fc47716e0978f6fd9aa1ed5a59e75706318` | `0W/0E`, `290/290` Win, `6/6` `test_native_data.py`, `10+5` audits PASS, `360` actions & all `4` partition SHA-256s reproduced identically; low hygiene `NR-016-1` | **`ACCEPTED_WITH_LIMITATIONS`** |
| `NEXT-017` | `3eb40b96aca90bc8243086fd8e3b620c4b486970` | `06e666997124c58056213e75efb00276e9cb1153` | `0W/0E`, `290/290` Win, `3/3` Py, 65-param specialist `2e67c656...` (`9,929` B) exact match, `13/20` public & `10/16` protected (`0` wrong) vs `16/20` & `14/16` host | **`ACCEPTED_WITH_LIMITATIONS`** |
| `NEXT-018` | `d8ebe25bd719a477c9d6cfa73afd17e50afb964e` | `4c5c6a0ad98f3f60a57d4dc4c869240589c08a56` | `0W/0E`, `290/290` Win, `2/2` Py, 37-param Master `c1199fd6...` (`3,829` B) exact match, `16/20` public & `14/16` protected (`6` fallbacks, `0` wrong) | **`ACCEPTED_WITH_LIMITATIONS`** |
| `NEXT-019` | `709e49736d3d6bf55db4d1414887016f52d15b60` | `867030d7222a4986961a5c27f24bab42f17d5394` | `0W/0E`, `290/290` Win, `2/2` Py, cooperation `16/20` (`0` wrong), live BOM/CRLF/emoji UTF-16 span, catalog-leak block & `9/9` input bounds PASS | **`ACCEPTED_WITH_LIMITATIONS`** |
| `NEXT-020` & integrated batch | `6dbf726443e9afd3c527f99d4851440e1404f4bb` | `fcbc8be91da112fd69e82bbbf02e78c7d379efd6` | `0W/0E`, `291/291` Win, `33/33` Py (`30.057 s`), rejected refit `79918ba6...` (`11,414` B) exact match, `13/20` public & `8/16` protected via `native_review.py` (`0` wrong), `4/4` offline PS 5.1/7.6.9 WPF PASS (`521` inventory); low `NR-020-1` | **`ACCEPTED_WITH_LIMITATIONS`** |

## Non-blocking hygiene register (`0` blocking defects)

1. **`NR-016-1` [Low — Non-blocking hygiene in `lab/native_data.py:131`]:** `validate_cases()` normalizes `family` via `.casefold()`, whereas `lab/native_review.py:16` normalizes via `family_key()` (`re.sub(r"[-_.]+", "-", name.casefold())`). Verified that `validate_cases()` allowed `python_dateutil` in `evaluation` alongside `python-dateutil` in `train`, whereas `native_review.py` blocked it. Unify `validate_cases()` on `family_key()` before enrolling new families.
2. **`NR-020-1` [Low — Non-blocking evaluation hygiene in `lab/native_review.py`]:** Add optional `--master` / `--master-sha` flags to `lab/native_review.py` so protected evaluator runs can score `Primary Master` cooperation (`confidence_gate`, `linear_gate`, `original_master`) alongside specialist/refit candidates without an external harness wrapper.

## Sol next assignment (`NEXT-021` through `NEXT-025`)

1. Cleanly resolve `NR-016-1` and `NR-020-1` inside the next five-update batch above accepted `NEXT-020` (`6dbf726443e9afd3c527f99d4851440e1404f4bb`).
2. Target the independently verified bottleneck across `NEXT-016`..`NEXT-020` (`0/12` useful host completions on `pycparser`/`reportlab`/`pdfplumber`, `48/48` acquisition withholds, and `2` answerable protected cases conservatively withheld by host grammar):
   - Expand verified deterministic host question-grammar & multi-document claim retrieval coverage (while preserving `0` wrong deliveries) AND/OR wire native specialist/Master routing into genuinely non-trivial decision bottlenecks where a learned policy can measurably outperform unrestricted host/TF-IDF (e.g., latency/passages-scanned budgets, multi-source disambiguation, or natural paraphrased owner prompts) with positive and negative observed outcomes on diverse real project families.
3. Preserve all standing boundaries: keep all five native checkpoints (`assets/native.json` `6b9b23fc...`, `lab/project-specialist.shadow.json` `5c476024...`, `NEXT-017` `2e67c656...`, `NEXT-018` `c1199fd6...`, rejected `NEXT-020` `79918ba6...`) strictly `shadow`-only until a fresh protected evaluation proves a genuine advantage over baselines and receives independent sign-off; keep host execution disabled (`read owner-imported text`); keep private evaluator cases outside Git; keep local-only (`NO git push`); leave legacy ATLAS (`695585f`), PLATZ Engine, and FORGE untouched.

[Review evidence](../TEST_EVIDENCE.md#next-015r-and-native-original-next-016-through-next-020-independent-batch-review) · [Owner report](ATLAS_OWNER_REPORT.md) · [Batch protocol](ATLAS_REVIEW_PROTOCOL.md) · [Native protocol](../../lab/native-first-protocol.md) · [Roadmap](../ROADMAP.md)
