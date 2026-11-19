# Canonical ATLAS Next handoff

| Field | Current assignment |
| --- | --- |
| Task | NEXT-008R independent review of bounded C# call binding, Python declaration spans, and ledger request integrity repairs |
| State | PENDING_INDEPENDENT_REVIEW |
| Next writer | GPT-6.2 Sol |
| Product author | Gemini Argon Orchestrator |
| Frozen product | `9ff586699629678d3342f7f9be3eaf69ec175bf6` |
| Author evidence | `51cb0a66d241583e9029011a3fa8210107633dd7` |
| Frozen comparison | `8585ca05ef4f9632010b78d869fadef58bcdc45c` and pre-product correction `3b03b1e152ebf688513f2006e316ddab0c3294a4` |
| Last accepted baseline | NEXT-007 product `2011be05e61a7c7107bce3496cb0bf072e262341`, Argon's verdict `59bd350c35f17d6bc6ae29539afb2aa22c1cc909` (`ACCEPTED_WITH_LIMITATIONS`) |
| Prior reviewed product | `80c09b1b928c68e08b05bf0770cbc3781f86a81f` (`a6cc368`, `REPAIRS_REQUIRED`) |
| Windows PowerShell 5.1 ZIP | `out/NEXT-008/ATLAS-Next-win-x64-ps51.zip`, SHA-256 `b3c28fd3942c788c4a53ff43ed4b81b1dcf30a53f770c7aadf8e1a6c1ad2cac1` (`95,147,775` bytes) |
| PowerShell 7 ZIP | `out/NEXT-008/ATLAS-Next-win-x64.zip`, SHA-256 `7dac0f5b5c534be82bc01e69613b01b9a079bbd4001e914037cb9eabf205b47d` (`112,799,916` bytes) |
| Model status | Desktop router `6b9b23fc…` advisory, project specialist `5c476024…` lab-only, both shadow and byte-identical to NEXT-007 |
| Next feature milestone | NEXT-009 deferred until independent acceptance |

## Sol Phase A — Independent NEXT-008R review

1. Verify Git history and clean-clone reproduction of repaired product `9ff586699629678d3342f7f9be3eaf69ec175bf6` and author evidence `51cb0a66d241583e9029011a3fa8210107633dd7`. Rerun `dotnet build checks -c Release --configfile NuGet.Config`, `AtlasNext.Checks.exe` (`227/227`), `lab/test_integrity.py` (`13/13`), `lab/outcomes.py` (`31/31`), `lab/project_audit.py` (`10/10`), `lab/evaluate.py`, `OPENBLAS_NUM_THREADS=1 lab/project_specialist.py evaluate`, NEXT-005/006/007 replays (`8/8`, `13/13`, `17/17`), `lab/code_conversation.py search`, `lab/code_conversation_host.py` + `--verify-only` (`12/14`, `18` exact deliveries, `5` withholds, `2` honest omitted-`using` abstentions, `0` wrong deliveries, `6/6` real-source ledger rows, `7/7` negative mutations), and both Windows PowerShell 5.1 and PowerShell 7.6.9 `tools/package.ps1` + `tools/verify-package.ps1` workflows against the shipped `out/NEXT-008/*.zip` packages and clean rebuilds. [Author test evidence](../TEST_EVIDENCE.md) · [NEXT-008 review receipt](../evidence/independent-review-next008.json) · [Ledger](../evidence/next-008/eligible-outcomes.json)
2. Replay evaluator-held C# competing extension-method cases (implicit-usings disabled vs explicit `using System.Linq;` across both direct and contextual caller queries), Python grammar and annotation span cases (top-level `lambda` colon, explicit `\` continuations before/inside/after `-> ReturnType`, required-after-default parameter ordering, reserved keywords as parameter/declaration names, valid annotated/defaulted declarations, and no follow-up through rejected declarations), and ledger/report altered/swapped request checks with matching report SHA-256, keeping private expectations outside Git.
3. Commit a separate independent review receipt, owner report update, and canonical handoff locally on `main` (`ACCEPTED`, `ACCEPTED_WITH_LIMITATIONS`, or `REPAIRS_REQUIRED`). If accepted, immediately select, preregister, implement, and verify bounded NEXT-009 in the same activation according to `docs/ROADMAP.md` and `ATLAS_REVIEW_PROTOCOL.md`, then return to Gemini Argon Orchestrator via `PENDING_INDEPENDENT_REVIEW`. Keep host execution disabled, network offline, native models shadow-only, Windows privileges unchanged, and legacy ATLAS, PLATZ Engine, and FORGE untouched. Work stays local.

[Independent NEXT-008 review](../evidence/independent-review-next008.json) · [Owner report](ATLAS_OWNER_REPORT.md) · [Reciprocal review protocol](ATLAS_REVIEW_PROTOCOL.md) · [Argon's accepted NEXT-007 decision](../evidence/independent-review-next007.json) · [Roadmap](../ROADMAP.md)
