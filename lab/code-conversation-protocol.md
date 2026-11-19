# NEXT-008 public comparison protocol

Freeze this protocol, multi-turn oracle cases, ordinary-search comparator, and measured baseline output before changing product code. The regression baseline is the independently accepted NEXT-007 product `2011be05e61a7c7107bce3496cb0bf072e262341`, reviewed in `docs/evidence/independent-review-next007.json`.

## Operations

- **Rich C# parameter selectors (`Where is Type.Member(...) defined?` / `called?`):** Extend simple by-value C# parameter type matching beyond primitives/identifiers to generic (`Name<T1,T2>`), nullable (`T?`), and 1D array (`T[]`) type spellings, normalized across spaces, while preserving exact case and withholding modifiers (`ref`, `out`, `in`, `params`) or nested/unsupported types. Include standard .NET SDK implicit global usings (`System`, `System.Collections.Generic`, `System.IO`, `System.Linq`) in the in-memory Roslyn reference context so cross-file C# bindings do not fail on files omitting redundant top-level `using` directives.
- **Python positional arity with annotations and simple defaults (`Where is Type.member(N parameters) defined?`):** Support positional parameters carrying `: Type` annotations and simple constant/empty defaults (`None`, `True`, `False`, numbers, single-line strings, `()`, `[]`), counting receiver parameters as declared. Keyword-only separators (`*`), positional-only separators (`/`), `*args`, `**kwargs`, complex default expressions, and shadowed class redefinitions continue to withhold safely.
- **Conversational follow-ups on a selected completed `code.definition` turn:**
  - `What are its parameters?` / `Show its parameters.` (`code.parameters`) cites the exact parenthesized parameter list span `( ... )`.
  - `What is its return type?` / `Show its return type.` (`code.return`) cites the exact C# return type span or Python `-> ReturnType` annotation span, and withholds as `unsupported` on type declarations or unannotated Python declarations.
  - `Where is it called?` / `Show its callers.` (`code.calls`) performs cross-file C# call-site citation against the selected `ParentTaskId` declaration revision in active project sources, withholding as `unsupported` when called without an active selected definition, on Python declarations, or when evidence changes after submission.
- **Multi-document claim synthesis (`Compare project claims: key1 | key2`):** Retains NEXT-007 per-claim provenance, conflict, staleness, and insufficient-evidence guarantees.

## Oracle, Comparator, and Ledger Rules

`lab/code_conversation.py` pins the accepted NEXT-007 source files from Git (`2011be05e61a7c7107bce3496cb0bf072e262341`), the installed `python-dateutil 4.0.0` `dateutil/relativedelta.py` source by SHA-256, and three reproducible metric projections from `c05b083064372621289fc5e9ed86e5782ab6b4af`. The 14 multi-turn/project cases comprise 6 real-source public observations (author-written prompts over real sources) and 8 constructed controls. Constructed controls, negative ledger checks, and independent custodian labels never enter training or private qualification; the eligible outcome ledger (`eligible-outcomes.json`) stores only real-source public observations including negative capability outcomes (`unsupported`). Both policies use zero per-case external inference calls.
