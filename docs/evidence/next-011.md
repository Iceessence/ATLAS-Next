# NEXT-011 local reference chat author evidence

Product `3ae818874c19288b24df943fea4d7f0e4202273c`. Offline build succeeded. Windows checks 276/276 include model name validation, ordered context, response bounds, redirect refusal and cancellation. Production transport only connects to the numeric IPv4 loopback socket and ignores proxies and redirects. The owner explicitly enables connection and chooses an installed tagged model. Conversations are window-local. Deterministic evidence retains its own workflow.

## Historical reference trials

Ollama 0.40.13 ran on `127.0.0.1:11434` with cloud disabled and preinstalled models, with no model download. Two actual calls with `qwen2.5-coder:7b` returned the preregistered `BRIDGE` and `BRIDGE-LOCAL` in 2870.87 ms and 2279.14 ms. Probe working set was 50,241,536 bytes, excluding the reference service. The smaller `qwen3.5:0.8b` ran both calls but failed exact response recovery. Both are external references. Neither two-turn result establishes general coding quality. The reference service is a same-host trust dependency and its output is unverified text.

## Current selected reference

After the owner requested a newer model, the production reference client was tested with the preinstalled `qwen3.8:27b` (SHA-256 `aaee06c39dcf2437cde036998d960e1fc1494b8191be7cc9657d01e509097813`). It returned the exact two expected responses in 10,429.38 ms and 8,174.58 ms. The probe sampled 50,769,920 bytes of client working set, excluding Ollama and GPU memory. This was the highest Qwen version tag in the installed inventory. The app requires explicit selection on each window and selects no default model. [Model response receipt](next-011/next011-smoke-qwen38.json)
