# NEXT-020 native self-improvement trial

Product `6dbf726443e9afd3c527f99d4851440e1404f4bb`. Run `python -B lab/native_improvement.py` after reconstructing NEXT-016 measurements with `OPENBLAS_NUM_THREADS=1`. The prior specialist and deterministic host tied at 8/8 on validation. That lack of advantage triggered the preregistered acquisition and refit.

The eight reportlab and pdfplumber tasks supplied 32 new proposed-source outcomes, all actual host withholds. The refit used 128 source proposals, including the original 16 positives. Seed 73 at 0.50 was selected. Training loss fell from 0.69441 to 0.13818. Fit took 125.7 ms and used 46,904 peak Python-traced bytes. The specialist has 65 parameters and its checkpoint retains Adam moments.

On the frozen public comparison, the refit delivered 13/20, the previous specialist 13/20, and the strongest baseline 16/20. No verified answer changed from wrong to correct or correct to wrong. Both original specialist trials had zero incorrect deliveries. The refit reduced explicit abstentions from seven to six but the new route was still withheld by the host. The candidate was rejected for no improvement over the strongest baseline. The previous checkpoint remained byte-identical and current app authorization remains deterministic. [Checkpoint](refit-candidate.json) · [Full before and after report](trial.json)

The separate evaluator command scores the frozen original checkpoint and the exact rejected refit. Use an external `outcomes.py` schema-1 manifest with a digest-pinned sibling suite. Include a stable `family` on each case, use explicitly authorized Markdown sources and keep all private files and reports outside Git. The harness rejects overlapping public families, normalized requests, or source text. The evaluator controls its acceptance criteria and custody. The trainer has no private suite argument. Use the previous candidate files at the frozen revisions without changing them.

```powershell
python -B lab/native_review.py --manifest <external-manifest> --report <fresh-external-report> --candidate docs/evidence/next-020/refit-candidate.json --candidate-sha 79918ba61f82b571f5d8618d11f7542851f3d1f4763c5050c3c439619a2af34d
```

Three review-gate unit tests passed. A separate author-only synthetic CLI invocation exercised one supported fact through the real host and successfully wrote an external report. There was no private evaluator run or qualification. Custody is procedural and separate from candidate inference. This work does not establish a new Windows security perimeter.

The next data requirement is verified positive and negative actions for naturally posed owner-authorized questions from unrelated project families. The current acquisition and frozen partition plumbing can measure and retain them. The batch includes five new audit receipts, and the current new families exposed a verifier coverage limit. Exact recorded claims can still be withheld when the request is outside the host's supported grammar. More wrong-source counterfactuals alone cannot address that bottleneck.
