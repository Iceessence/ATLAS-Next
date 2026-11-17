# Architecture challenge and decisions

The owner's approved strategy is new code with selective artifact migration. This candidate reuses design lessons, with new source, data and model weights. The legacy product comparison against 08bdc29 was empty at legacy HEAD 695585f. The checked Qwen3.5-0.8B adapter config, Hydrogen REPAIRS_REQUIRED receipt and simulated row generator support Sol's cautions. Neither old weights nor the old Arena are runtime dependencies.

## Desktop and packaging proof

| Consideration | WPF + Python core | Tauri/React + Python | Selected WPF + C# core and offline Python lab |
| --- | --- | --- | --- |
| Native shell, startup, shutdown | Native frontend and child process handshake/recovery | WebView2, Rust shell and Python sidecar lifetimes | One Windows app and one profile writer; no runtime IPC or child model daemon |
| Installation, dependency/version management | .NET plus bundled Python/runtime wheels and IPC compatibility | Rust/JS build dependencies, WebView2 offline provisioning, sidecar and bridges | Offline installed SDK packs, private x64 runtime and exact checkpoint. Larger portable ZIP, one deployed application runtime |
| Signing and distribution | Sign and service binaries and Python stack | Sign shell and sidecar and service browser/runtime dependencies | Unsigned evaluation ZIP; distributor must independently review runtime notices, signing, servicing and release identity |
| GPU and inference | Natural PyTorch integration | Natural Python ML integration across a bridge | Simple CPU math matches Python here; larger GPUs later need a bounded worker plus measured lifecycle |
| UI and debugging | Native WPF tools, two languages in live traces | Flexible React ecosystem and web tests plus bridge debugging | WPF automation, one debugger for the live path. App latency measured directly rather than inferred from frame rate |
| Trust and future private server | Good if process ACLs and wire validation exist | Web origin/bridge and sidecar need narrow capabilities | Fewer live ingress paths. A UI-free engine supports later transport extraction. No deployed multi-user boundary |
| Maintenance and build feasibility | Strong future training path but Python redistribution needed now | Additional toolchain for one Windows-only screen; Cargo unavailable on PATH | SDK and runtime present. Clean clone builds offline, packaged real task and recovery pass |

Before choosing, built a minimal WPF window via NuGet with all package sources cleared, published, zipped, extracted and launched it. The first hidden-window probe timed out waiting on ContentRendered. A Loaded/dispatcher shutdown probe passed. This established reference-pack and Windows shell feasibility. The final portable ZIP separately bundles hostfxr, CoreCLR, WindowsDesktop runtime, licenses and launcher. UI Automation confirmed its running process loaded coreclr.dll from its extracted private runtime directory. Tauri and Python-sidecar options were compared architecturally and received no equivalent timing run. Approval remains pending for production signing and broader Windows coverage.

## Other departures

- For a fixed three-record corpus, exact ID lookup and curated lexical concepts justify an in-memory rebuildable index. No embedding-quality or broad semantic-search claim. New public records currently require a new reviewed package with updated asset pins.
- Small JSON task files plus an exclusive writer suffice for a single scope. SQLite, persistent DAG scheduling, multiple profiles and untrusted execution are deferred.
- Use a three-class original router and two-step source verification. No model grants or training loop in the desktop. Public research evaluation is deliberately labeled author evidence. The plan's protected M0/M1 gate and larger dataset target remain open.
- No legacy checkpoint comparator is like-for-like: old classifiers have other action sets, featurizers and benchmark labels. A matched linear model and deterministic rules are meaningful controls.
- The owner prohibits autonomous private/account/payment access and executable acquisition. The runtime contains only bounded lookup and user-triggered local receipt export. The Hyper-V preflight has read-only observation and always returns execution unauthorized. [Test evidence](TEST_EVIDENCE.md)
