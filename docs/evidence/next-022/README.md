# NEXT-022 original language intent experiment

Frozen product `cea141cf9cb0bc0c1d2918c97a60cc8aed903833`. After NEXT-021 acquisition, set `OPENBLAS_NUM_THREADS=1`, run `python -B lab/atlas_language.py fit`, then `python -B lab/atlas_language.py evaluate`. The original tanh encoder learns from raw request character 2/3/4-grams with a 384-entry vocabulary fit on training only. The architecture is 384→16→3, with 6,211 parameters, random seed 37, and 400 full-batch Adam steps. Training used 24 source-derived requests and 12 separately marked safety controls. No pretrained weights, teacher-generated data or hosted inference were used. The 1,155-parameter linear model used equal input and training data.

| Public evaluation | Typed intent matches | Verified completions | Correct withholds | False abstentions |
| --- | ---: | ---: | ---: | ---: |
| Raw host | Unspecified | 0 | 8 | 16 |
| Exact typed control | Unspecified | 16 | 8 | 0 |
| TF-IDF | 20 | 16 | 8 | 0 |
| Linear | 24 | 16 | 8 | 0 |
| Historical original router | 11 | 4 | 8 | 12 |
| Original text encoder | 24 | 16 | 8 | 0 |

No evaluated policy delivered an incorrect proposal on those 24 tasks. The candidate tied simpler baselines and incurred more inference time. It missed one of eight answerable validation requests. The initial validation exposed two valid subjects followed by sentence-ending punctuation. A focused literal-boundary fix recovered those references without changing weights or the public evaluation outcome. The first report is retained for comparison. The host still independently verifies a scoped typed source read. The selected typed action remains in shadow and grants no authority to reinterpret the owner's raw intent.

The nonlinear fit used 93.1 ms and 311,832 peak Python-traced bytes on CPU. Public scoring p95 was 231 µs, excluding host startup. Reports separate fitted intent, deterministic grounding, final matched outcome and host cost. This is a small, split-authored intent experiment over audited source types. It does not establish broad language or generative competence. [Frozen checkpoint](language.json) · [Fit evidence](fit.json) · [Public evidence](evaluation.json) · [Initial grounding replay](initial-grounding.json)
