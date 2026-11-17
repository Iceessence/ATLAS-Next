# Architecture

A single unelevated WPF process calls the trusted C# Engine. Release-pinned arrays supply local neural inference. The offline lab trains and evaluates separately. The desktop exposes a single read-only handbook operation. There is no shell, network provider, arbitrary path tool or guest execution interface exposed to a model.

| Owner | Runtime contract |
| --- | --- |
| WorkWindow | Accept request and show persisted attempts, source and comparison. Buttons submit, verify, cancel or export. |
| Engine | Submit a bounded request with a GUID, persist queued state, obtain a model proposal, select the allowed deterministic route and resolve an eligible source. Finish or cancel an existing ID. StopAll cancels every pending review. |
| Routing | Digest- and shape-checked 256-input, 16-hidden, 3-output CPU forward pass. Immutable shadow binding. Active deterministic policy is limited to public handbook lookup. |
| Library and Assets | Rebuild an in-memory catalog from pinned release records on startup. Exact ID before type and keyword/curated synonym matching. Ties or missing evidence request clarification. Reopen and digest-check a selected record on completion. Scope, revision, type and digest must still match. |
| Journal | One Windows profile writer holds an exclusive handle. One UTF-8 JSON per task contains input, authority, scope, dependencies, limits, specialist, model, result, evidence, verification, event sequence, memory and time sampling. Stage, flush and same-volume replace for each transition. |
| Lab | Offline Python builds a fixed-feature original MLP and linear comparator. Typed origin whitelist and loader accept only public rule labels for this experiment. Pinned corpora and generated golden outputs exercise parity in C#. |

Supported transitions are queued → review → completed/cancelled/failed, queued → needs clarification/failed, and queued/review → interrupted on restart. Duplicate task ID with the same request returns its current receipt. Conflicting input is refused. A repeated finish or cancel on a terminal task has no effect. Interrupted work needs a fresh request. The two-step interaction gives the owner a genuine cancellation point before answer publication. Power-loss filesystem durability beyond the exercised process-crash cases is unqualified.

Task history belongs to the local OS user. App-level verifiers detect changed shipped data/model files against digests embedded in the executable. A local user able to modify the binary or local journal is outside that guarantee. The results describe source integrity for a demo fixture, with no external claim authentication. Verification checks are deterministic host operations. CPU time and working-set telemetry are observations, not an OS resource sandbox.

Future extension should add typed actions with explicit permissions and independent validation through this host contract. A future private transport would need authenticated principal mapping and narrower storage ACLs. Move to indexed transactional persistence when multiple writers, long-running jobs or multi-principal scope require it. [Decisions](DECISIONS.md)
