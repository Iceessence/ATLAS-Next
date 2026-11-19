# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-008R follow-up repair — independent review of Python declaration grammar fix |
| State | PENDING_INDEPENDENT_REVIEW |
| Next writer | GPT-6.2 Sol |
| Implementation author | Gemini Argon Orchestrator |
| Frozen repaired product | `298db33c18f8f888f162569bf2b9bd44434a4a6b` |
| Author evidence | `ca4926d2bc69bebbe1086f6298938be2782a351d` |
| Prior NEXT-008R review | `2f2eb18ce10d7f65bd6caa43cd5c22cd20ec1a82` (`REPAIRS_REQUIRED` on `9ff586699629678d3342f7f9be3eaf69ec175bf6`; `NEXT-008R-1` and `NEXT-008R-3` verified, `NEXT-008R-2` blocked) |
| Frozen comparison | `8585ca05ef4f9632010b78d869fadef58bcdc45c` and pre-product correction `3b03b1e152ebf688513f2006e316ddab0c3294a4` |
| Last accepted baseline | NEXT-007 product `2011be05e61a7c7107bce3496cb0bf072e262341`, Argon's verdict `59bd350c35f17d6bc6ae29539afb2aa22c1cc909` (`ACCEPTED_WITH_LIMITATIONS`) |
| Verified packages | Windows PowerShell 5.1 (`95,153,220` bytes, SHA-256 `7171bcea876208a185073f5dadc69a1ef8d10693c92e8f3440f0a36d873bc0f6`, `out/NEXT-008/ATLAS-Next-win-x64-ps51.zip`) and PowerShell 7.6.9 (`112,806,426` bytes, SHA-256 `b51f115db30f5150ae37852cf17b56caf01e83d3139c9234355442037afbd50d`, `out/NEXT-008/ATLAS-Next-win-x64-ps7.zip` and `out/ATLAS-Next-win-x64.zip`) |
| Next feature milestone | NEXT-009 deferred until independent acceptance; upon acceptance, Sol selects and implements bounded NEXT-009 in the same activation |

## Sol assignment — Independent NEXT-008R follow-up review

Independently review repaired product `298db33c18f8f888f162569bf2b9bd44434a4a6b` (author evidence `ca4926d2bc69bebbe1086f6298938be2782a351d`) in clean checkouts under `docs/ai/ATLAS_REVIEW_PROTOCOL.md`:

1. **Inspect exact product and parser changes (`desktop/SourceDeclarations.cs`):** Verify `PythonHeaderParser` enforces strict Python 3.12 numeric tokens, expression operator rules (rejecting augmented assignments, binary `~`, invalid operators/gaps, unparenthesized `:=`, invalid continuations/newlines at depth 0, and malformed parameter/lambda/container/slice/string structures), fails closed on unsupported/malformed headers, and preserves valid declarations, exact annotation spans, and explicit `\` continuations.
2. **Replay protected, adversarial, and regression checks:** Re-run the four held malformed Python declaration headers alongside fresh Python/C# checks, verify withholding on both arity-qualified and unqualified definitions and both parameter/return follow-ups, confirm `NEXT-008R-1` (explicit C# `using` binding + extension differential) and `NEXT-008R-3` (ledger request/summary/linkage validation with matching `source_report_sha256` negative controls) remain intact, and replay the clean Windows (`240/240`), Python (`14/14` integrity, `31/31` outcomes, `10/10` audit, `54/72` router, `16/16/14/15` + `31/31/29/30` frozen specialist), NEXT-005/006/007 (`8/8`, `13/13`, `17/17`), and NEXT-008 (`12/14`, `18` exact, `5` withheld, `2` omitted-`using` false abstentions, `0` wrong) suites.
3. **Verify cross-shell packages & decide verdict:** Verify both shipped packages (`7171bcea876208a185073f5dadc69a1ef8d10693c92e8f3440f0a36d873bc0f6` and `b51f115db30f5150ae37852cf17b56caf01e83d3139c9234355442037afbd50d`) and clean rebuilds under PowerShell 5.1 and 7.6.9 (`519` inventory hashes, `28` Roslyn files, `native.json` `6b9b23fc…`, `shadow=[]`, live WPF flows). Commit the independent verdict (`ACCEPTED` / `ACCEPTED_WITH_LIMITATIONS` or `REPAIRS_REQUIRED`) through the canonical handoff; if accepted, immediately select and implement bounded `NEXT-009` in the same activation. Keep private evaluation cases outside Git, host execution disabled, network offline, native models `shadow`-only, Windows privileges unchanged, no push, and legacy ATLAS, PLATZ Engine, and FORGE untouched.

[Owner report](ATLAS_OWNER_REPORT.md) · [Test evidence](../TEST_EVIDENCE.md) · [Previous independent review](../evidence/independent-review-next008r.json)
