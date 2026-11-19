# NEXT-025 controlled original-model refit

Frozen product `19f3f468b404d7642a2370a9eb19e6e5b2f4aff8`. After NEXT-021 acquisition, run `python -B lab/atlas_refit.py` with `OPENBLAS_NUM_THREADS=1`. The preregistered validation trigger observed one missed python-docx intent in the previous original text checkpoint. The acquisition pool added eight source-backed positives and four verified authored safety controls, bringing text training from 36 to 48 eligible requests. The new 6,211-parameter checkpoint was initialized from scratch with seed 37 and retained both Adam moments. Fit took 95.8 ms with 315,360 peak Python-traced bytes and 49,688 parameter bytes. [Checkpoint](refit.json) · [Trial](trial.json)

| Metric | NEXT-022 incumbent | NEXT-025 candidate |
| --- | ---: | ---: |
| Validation useful outcomes | 11/12 | 12/12 |
| Public evaluation useful outcomes | 24/24 | 24/24 |
| Public verified deliveries | 16 | 16 |
| Public incorrect proposal outcomes | 0 | 0 |
| Public multiclass Brier score | 0.000231 | 0.040365 |

No public evaluation action changed. The candidate was **rejected** because it added no verified unfamiliar-task completions over the strongest controls. It was worse calibrated on that set. No public or validation losses were observed, but there is no broad forgetting or qualification claim. The trial preserved the incumbent bytes and did not alter active routing. Every learned path remains shadow-only.

The custodian can run the frozen comparison on an externally held, SHA-256-pinned `lab/outcomes.py` schema-1 manifest. Include stable project `family`, a `keys` array corresponding to the supplied Markdown and Python texts, independently checked citation expectations, and explicit source authorization. The scorer checks overlaps against the frozen public Foundry. Keep the manifest, sibling sources/expectations and output outside every Git checkout. No protected suite was available to the author. An author-only mixed-source synthetic smoke exercised the separate report path and the live Windows host. This establishes interface behavior, not independent qualification.

```powershell
python -B lab/atlas_language_review.py --manifest <external-manifest> --report <fresh-external-report> --candidate docs/evidence/next-025/refit.json --candidate-sha 3c75904e75e4c7774fa4260642c55092a44a5756bbc54353f2123e74e3414a2b
```

The important next evidence is performance on naturally composed questions with implicit references, diverse actionable evidence and real source ambiguity. The current prompt families are authored, the safety controls are explicitly marked, and literal typed access still matches the learned result.
