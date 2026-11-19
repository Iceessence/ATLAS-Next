# NEXT-023 original read-only planning and procedure reuse

Frozen product `076627316c72fc51fbae087aea9cdffcc7c19fad`. The six-hidden-unit source planner learned from 24 verified positives and 120 actual counterfactual actions or withholds in six training families. The 61-parameter 8→6→1 model started from seed 37 and fit in 24.4 ms on CPU. This reuses the existing standard gradient fitter. [Frozen weights](planner.json) · [Fit costs](fit.json)

A bounded investigation records path selection, each read and each independent evidence assertion with step dependencies. An actual failed or inappropriate read can lead to a second attempted source. The host limits steps to five and charged read wall time to 60 seconds, checks scope and source hashes, and denies arbitrary candidate indexes. The procedure-assisted policy selects a proven public procedure card through the original language model. Source success in a shadow trace requires the independent read oracle, matching scope, revision, digest and exact span. The original raw host lookup remains authoritative.

| Evaluation policy | Useful / 24 | Completed deliveries | Reads | Recoveries |
| --- | ---: | ---: | ---: | ---: |
| Fixed order | 16 | 8 | 28 | 4 |
| Deterministic literal order | 24 | 16 | 16 | 0 |
| Original learned source order | 24 | 16 | 16 | 0 |
| Original learned order plus procedure store | 24 | 16 | 16 | 0 |

No wrong deliveries appeared. The fixed order recovered four task rows after an unsuccessful first evidence check. The original policy avoided redundant reads against the fixed baseline but tied literal ordering on path cost and outcomes. The procedure-store route added policy overhead and no new verified completions. This is a small linear dependency graph and bounded recovery experiment. It proves no general procedural competence. [Public replay](evaluation.json)

Run `python -B lab/atlas_planner.py query --project pycparser --question "Show the evidence for this installation's published name (pycparser)."` after Foundry acquisition. The report shows model-assisted selection, source identity and the original host status. Use `--policy fixed` to see a real failed first evidence assertion followed by a second read. Three tests cover replan, denial, stale sources, cancellation and wall budget.
