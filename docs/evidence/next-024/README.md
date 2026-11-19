# NEXT-024 distinct original Master decision contracts

Frozen product `eea0c32b8b0bea5496016bc53098eb6289618d47`. Primary reuses the original 6,211-parameter text model. Research and Engineering train separate 61-parameter source-ranking heads initialized with seeds 11 and 73. Research learned from 108 factual actions and 18 positives. Engineering learned from 36 declaration actions and six positives. Each head ranks a different authorized source type. An inconclusive disagreement yields an independent evidence check under the same read budget. It never grants tool permission. The scope is audited facts and declaration inspection. General research and engineering competence remain unverified. [Weights](masters.json) · [Fit evidence](fit.json)

| Public ablation | Correct outcomes / 24 | Verified deliveries | Read actions |
| --- | ---: | ---: | ---: |
| Raw host | 8 | 0 | 0 typed reads |
| Fixed order | 16 | 8 | 28 |
| Deterministic literal order | 24 | 16 | 16 |
| NEXT-023 original planner | 24 | 16 | 16 |
| Primary / Research / Engineering alone | 20 / 20 / 16 | 12 / 12 / 8 | 24 / 20 / 28 |
| Primary+Research / Primary+Engineering / Research+Engineering | 24 / 20 / 24 | 16 / 12 / 16 | 16 / 24 / 16 |
| All three | 24 | 16 | 16 |

The full team changed zero first reads relative to literal order. There were zero wrong answers, and two-role variants matched the full team at lower runtime cost. The three-role experiment demonstrates bounded learned proposals, with no proven utility advantage. [Ablations and traces](evaluation.json)

The live compound task linked a factual audit request and a Python source declaration request on the same independently scoped project. The host drew exact evidence for both shadow goals and kept the raw host authority separate. The batch caps the two linked traces at eight total steps with a shared 60-second deadline. [Live compound trace](live-compound.json)
