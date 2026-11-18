# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-004 verified paraphrase & Research coverage |
| State | ASSIGNED_FOR_DEVELOPMENT |
| Next writer | GPT-6.2 Astra |
| Accepted baseline | `bb015a42205b8c2b6e4ad4cefae99a6b116099dd` (author evidence `185a144a97e00f56d99bb4eae1388ec8b356208c`), NEXT-003 accepted with limitations |
| Independent review receipt | [`docs/evidence/independent-review-next003.json`](../evidence/independent-review-next003.json) |
| Windows PowerShell 5.1 ZIP (verified) | `out/ATLAS-Next-win-x64-ps51.zip`, SHA-256 `02af933421839f641f88afa44b6420393271aaad27a6a622610391d7c89fbe46` (excludes lab shadow checkpoint) |
| Canonical PowerShell 7 ZIP (verified) | `out/ATLAS-Next-win-x64.zip`, SHA-256 `79385d62acbe8dbee589f4f50e14376482dd577865341e5d367b605fc0ac5703` (excludes lab shadow checkpoint) |
| Native routing model | `assets/native.json` (`6b9b23fc...`, 4,163 params) remains strictly `shadow` / advisory (54/72 synthetic routing) |
| Lab document specialist | `lab/project-specialist.shadow.json` (`8f3cfcc1...`, 49 params) remains strictly `shadow` outside ZIPs (14/16 public eval, 12/13 public stress, 18/20 external blind) |

## Review Findings from NEXT-003 (Gemini Hydrogen)

1. **Verified gains:** All author counts reproduced cleanly (`71/71` Windows checks, `8/8` Python integrity, `13/13` public outcomes, `12/16 -> 14/16` public evaluation outcomes, `12/13 -> 13/13` NEXT-002 custodian suite replay, byte-identical `OPENBLAS_NUM_THREADS=1` shadow checkpoint refit, `20/20` independent adversarial C# security/isolation probes + named-project corrupt recovery, and both PS 5.1 and PS 7.6.9 cross-shell packaging/UI runs). Note on `lab/project_audit.py`: verified mismatched `RECORD` entries on `numpy` (`20`), `pillow` (`8`), and `lxml` (`7`) are runtime binary overlays (`+15,664` bytes on compiled binaries), while all 10 pure-Python source files match SHA-256 digests and AST line numbers.
2. **External custodian blind suite (`20` cases, `0` wrong deliveries):** `18/20` passed across `host`, `tfidf`, `linear`, and `native` (`10/12` unseen real `observed_project_outcome` trials across `certifi`, `charset-normalizer`, and disposable `bb015a4` build/ZIP/router receipts; `8/8` constructed controls).
3. **Why model promotion remains blocked:** Both public evaluation (`openpyxl-4`, `xlsxwriter-4`) and custodian observed outcomes (`2/12` misses) fail because `Research.RequestedSubject` and `Routing.ProjectFact` reject naturally phrased factual questions outside `<Prefix> <Subject>`. Meanwhile `tfidf` already routed `16/16` public evaluation docs cleanly (`100%` route accuracy), and the 49-param shadow MLP merely predicts host deliverability while false-abstaining on the single-doc `predicate` fixture (`12/13` vs `host` `13/13`).

## Bounded Assignment to GPT-6.2 Astra (NEXT-004)

Improve **verified natural-language factual handling** inside `Research` and `Primary` without weakening any safety, source-integrity, contradiction, staleness, modifier, or authorization guarantee:

1. **Expand deterministic Research & Primary matching for bounded factual paraphrases** (including natural question forms seen in `pillow-4`, `packaging-4`, `openpyxl-4`, `xlsxwriter-4`, and trailing helper words after a valid subject) so genuinely supported facts in owner-imported or pinned docs verify with exact UTF-16 spans and receipts rather than failing `unsupported`.
2. **Preserve zero wrong deliveries & strict safety regressions:** Prove that modifier mismatches (`primary` vs `secondary`), vague terms (`"What is the archive restoration?"`), definite+unconfirmed (`insufficient`), contradictions, stale sources, action words in fact subjects vs `procedure.restore`, and tampered `Authorization`/`Scope`/`Specialist` still withhold or refuse delivery cleanly.
3. **Re-benchmark on updated measured outcomes (shadow only):** Once `Research` handles safe paraphrases deterministically, re-run `lab/project_specialist.py` (`fit` and `evaluate` with `OPENBLAS_NUM_THREADS=1`), verify `linear`/`native` no longer break the `predicate` constructed case, and measure whether `native` adds real end-to-end usefulness or calibration value over `host` and `tfidf`. Preserve existing train/validation/evaluation family splits and provenance tags cleanly; keep both native models `shadow` unless genuinely justified for separate review.
4. **Add checks, rebuild both Windows ZIPs (PS 7 + PS 5.1), verify cleanly, and return for independent review:** Update Windows `checks/`, `lab/outcomes.public.json`, `docs/TEST_EVIDENCE.md`, `docs/ai/ATLAS_OWNER_REPORT.md`, and this handoff (setting `State` to `PENDING_INDEPENDENT_REVIEW` and `Next writer` to `Gemini Hydrogen`). Commit product changes separately from author evidence/handoff commits locally on `main` (do not push).

Keep `C:\ATLAS\Entire ATLAS`, PLATZ Engine, and FORGE untouched; keep executable automation disabled and Windows privileges unchanged.
