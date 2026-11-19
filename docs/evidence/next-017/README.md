# NEXT-017 original source specialist

Product `3eb40b96aca90bc8243086fd8e3b620c4b486970`. CPU NumPy 2.3.5 and Python 3.12.14, one BLAS thread. Run `python -B lab/native_models.py fit` and `python -B lab/native_models.py evaluate` after reconstructing NEXT-016 measurements. Three learning checks passed, including seed reproducibility and an evaluation-role denial.

The 65-parameter tanh network (6→8→1) trained from random weights with 600 Adam steps, using 96 proposed-source outcomes (16 verified positives and 80 negatives). Seed 37 at 0.50 selected by validation. Training log loss fell from 0.69037 to 0.03370. Validation log loss was 0.00460 and all eight validation tasks succeeded. The source-score fit used 121.5 ms, 36,408 peak Python-traced bytes and 520 parameter bytes. Those figures exclude the host process and some native allocations. CUDA training is optional through an installed compatible CuPy runtime. This workstation had an RTX 5080 but no CuPy provider, so only CPU training was exercised.

| Public evaluation | Useful / 20 | Wrong | Withheld | p95 scoring µs |
| --- | ---: | ---: | ---: | ---: |
| Unrestricted host | 16 | 0 | 4 | 1.5 |
| TF-IDF | 16 | 0 | 4 | 58 |
| Logistic regression | 15 | 0 | 5 | 427 |
| Original specialist | 13 | 0 | 7 | 395 |

The specialist withheld three tasks the host completed. All four new pycparser questions were withheld by the host under every proposed source. Model latency measures feature computation and selection over measured counterfactual actions. Host action costs and per-family failures are included in the public report. [Weights and Adam moments](checkpoint.json) · [Training and validation](fit.json) · [Evaluation](evaluation.json)
