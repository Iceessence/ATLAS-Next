# Sol-led development and reciprocal review

GPT-6.2 Sol is the primary implementation engineer. Gemini Argon Orchestrator independently reviews Sol's work and, after accepting the assigned candidates, develops one bounded enhancement. The canonical handoff assigns the current phase and the only writer for the product worktree and handoff.

## Sol activation

1. Independently review Argon's frozen enhancement in a clean checkout when applicable. Commit the review separately. Preserve historical verdicts, custody and author attribution.
2. If a repair is needed, Sol may implement it. Record author verification separately and mark the repaired candidate `AUTHOR_VERIFIED_PENDING_INDEPENDENT_REVIEW` after required local gates pass.
3. Select the next bounded major milestone from the roadmap, preregister its comparison where applicable, and implement it in the same activation. A locally verified repair can be the provisional engineering dependency. The last independently accepted baseline retains its designation.
4. Freeze separate product and evidence revisions for the repair and feature. Assign independent review of both to Argon. Neither can be accepted by its own author. An unaccepted dependency gates downstream promotion.

## Argon activation

Review Sol's repair and major milestone separately from clean independent checkouts. Record a separate verdict and evidence for each. Treat Argon-authored original code as previously reviewed by Sol. Argon's review of Sol's repair applies to the new repair and its demonstrated behavior. If both assigned candidates pass, implement and freeze one useful bounded enhancement in the same activation and assign Sol its independent review. If repairs are required, document the exact bounded assignment for Sol.

Keep protected inputs and expectations in evaluator custody outside Git. Maintain offline operation, host execution disabled, native models shadow-only, and the original project and privilege boundaries.

## Owner relay

Return a short continuation prompt directing the counterpart to the canonical handoff. The owner relays it. The handoff carries exact revisions, verification commands and results, limitations, and unfinished work when applicable.

[Canonical handoff](ATLAS_HANDOFF.md) · [Roadmap](../ROADMAP.md)
