"""NEXT-008 frozen public conversational cases, source inputs and ordinary-search comparator."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import statistics
import subprocess
import time
from importlib.metadata import distribution
from code_navigation import ROOT, span, working_set

BASE = "2011be05e61a7c7107bce3496cb0bf072e262341"
EVIDENCE_BASE = "c05b083064372621289fc5e9ed86e5782ab6b4af"

CODE = {
    "Repo.cs": """namespace Work.Sample;
class Store
{
    public IReadOnlyList<string> Load(string? filter) => Array.Empty<string>();
    public IReadOnlyList<string> Load(IReadOnlyList<int> ids) => Array.Empty<string>();
    public int Save(byte[] payload) => payload.Length;
}
""",
    "Worker.cs": """namespace Work.Sample;
class Worker
{
    public int Run(Store s, IReadOnlyList<int> ids) => s.Load(ids).Count;
    public int Shadow(IReadOnlyList<int> ids) { var s = new FakeStore(); return s.Load(ids).Count; }
    // s.Load(ids);
    public string Quote() => "s.Load(ids)";
}
class FakeStore { public IReadOnlyList<string> Load(IReadOnlyList<int> ids) => Array.Empty<string>(); }
""",
    "ShadowOnly.cs": """class ShadowOnly
{
    class LocalStore { public IReadOnlyList<string> Load(IReadOnlyList<int> ids) => new string[0]; }
    public int Run(IReadOnlyList<int> ids) { var Store = new LocalStore(); return Store.Load(ids).Count; }
}
""",
    "typed.py": """class Metrics:
    def record(self, name: str, value: int = 0) -> bool:
        return value > 0
    def strict(self, name, *, unit="ms") -> None:
        pass
""",
    "claims1.md": "Verification status: passed\nTarget framework: net10.0-windows\n",
    "claims2.txt": "Review mode: read-only\n",
}


def loc(file, line, quote):
    return (file, line, quote)


# Each conversation has 2 turns (turn 1 -> optional selected parent -> turn 2), except single-turn negative/research checks encoded as 1 or 2 turns.
# Turn spec: (question, expected_verdict, [expected_citations])
CASES = [
    (
        "real-cs-generic-compare-convo",
        ["ProjectQuestions.cs", "Engine.cs", "CodeQuestions.cs", "Documents.cs", "Research.cs", "Library.cs", "Routing.cs", "Assets.cs"],
        [
            ("Where is ProjectQuestions.Compare(string, IReadOnlyList<Passage>) defined?", "supported", [loc("ProjectQuestions.cs", 45, "Compare")]),
            ("Where is it called?", "supported", [loc("Engine.cs", 101, "ProjectQuestions.Compare(request,passages)")]),
        ],
    ),
    (
        "real-cs-nullable-params-return",
        ["Engine.cs"],
        [
            ("Where is Engine.ReadParent(Guid?, IReadOnlyList<Passage>) defined?", "supported", [loc("Engine.cs", 168, "ReadParent")]),
            ("What are its parameters?", "supported", [loc("Engine.cs", 168, "(Guid? id,IReadOnlyList<Passage> active)")]),
        ],
    ),
    (
        "real-cs-array-return-type",
        ["Journal.cs"],
        [
            ("Where is Journal.ValidClaims(ClaimAssessment[], Citation[]) defined?", "supported", [loc("Journal.cs", 123, "ValidClaims")]),
            ("What is its return type?", "supported", [loc("Journal.cs", 123, "bool")]),
        ],
    ),
    (
        "real-py-defaults-init-params",
        ["relativedelta.py"],
        [
            ("Where is relativedelta.__init__(22 parameters) defined?", "supported", [loc("relativedelta.py", 103, "__init__")]),
            ("What are its parameters?", "supported", [loc("relativedelta.py", 103, "(self, dt1=None, dt2=None,\n                 years=0, months=0, days=0, leapdays=0, weeks=0,\n                 hours=0, minutes=0, seconds=0, microseconds=0,\n                 year=None, month=None, day=None, weekday=None,\n                 yearday=None, nlyearday=None,\n                 hour=None, minute=None, second=None, microsecond=None)")]),
        ],
    ),
    (
        "real-py-calls-followup-unsupported",
        ["relativedelta.py"],
        [
            ("Where is relativedelta.normalized(1 parameter) defined?", "supported", [loc("relativedelta.py", 284, "normalized")]),
            ("Where is it called?", "unsupported", []),
        ],
    ),
    (
        "real-research-3way-provenance",
        ["Checks.txt", "Ledger.txt", "Package7.txt"],
        [
            ("Compare project claims: Windows checks | Ledger observations | Verified files", "supported",
             [loc("Checks.txt", 1, "185"), loc("Ledger.txt", 1, "5"), loc("Package7.txt", 1, "519")]),
        ],
    ),
    (
        "ctrl-cs-generic-calls-convo",
        ["Repo.cs", "Worker.cs", "ShadowOnly.cs"],
        [
            ("Where is Store.Load(IReadOnlyList<int>) defined?", "supported", [loc("Repo.cs", 5, "Load")]),
            ("Where is it called?", "supported", [loc("Worker.cs", 4, "s.Load(ids)")]),
        ],
    ),
    (
        "ctrl-cs-nullable-params",
        ["Repo.cs"],
        [
            ("Where is Store.Load(string?) defined?", "supported", [loc("Repo.cs", 4, "Load")]),
            ("What are its parameters?", "supported", [loc("Repo.cs", 4, "(string? filter)")]),
        ],
    ),
    (
        "ctrl-cs-array-return",
        ["Repo.cs"],
        [
            ("Where is Store.Save(byte[]) defined?", "supported", [loc("Repo.cs", 6, "Save")]),
            ("What is its return type?", "supported", [loc("Repo.cs", 6, "int")]),
        ],
    ),
    (
        "ctrl-cs-shadowed-generic-only",
        ["Repo.cs", "ShadowOnly.cs"],
        [
            ("Where is Store.Load(IReadOnlyList<int>) defined?", "supported", [loc("Repo.cs", 5, "Load")]),
            ("Where is it called?", "unsupported", []),
        ],
    ),
    (
        "ctrl-cs-class-return-unsupported",
        ["Repo.cs"],
        [
            ("Where is Store defined?", "supported", [loc("Repo.cs", 2, "Store")]),
            ("What is its return type?", "unsupported", []),
        ],
    ),
    (
        "ctrl-py-annotated-default-return",
        ["typed.py"],
        [
            ("Where is Metrics.record(3 parameters) defined?", "supported", [loc("typed.py", 2, "record")]),
            ("What is its return type?", "supported", [loc("typed.py", 2, "bool")]),
        ],
    ),
    (
        "ctrl-py-kwonly-unsupported",
        ["typed.py"],
        [
            ("Where is Metrics.strict(3 parameters) defined?", "unsupported", []),
        ],
    ),
    (
        "ctrl-parentless-calls-followup",
        ["Repo.cs", "Worker.cs"],
        [
            ("Where is it called?", "unsupported", []),
        ],
    ),
]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def sources():
    origin = {name: {"kind": "constructed_public_control"} for name in CODE}
    files = dict(CODE)

    def git_file(rev, path):
        return subprocess.check_output(["git", "show", f"{rev}:{path}"], cwd=ROOT)

    for name in ("ProjectQuestions.cs", "Engine.cs", "CodeQuestions.cs", "Documents.cs", "Research.cs", "Library.cs", "Routing.cs", "Assets.cs", "Journal.cs"):
        path = "desktop/" + name
        data = git_file(BASE, path)
        files[name] = data.decode("utf-8")
        origin[name] = {"kind": "observed_project_source", "revision": BASE, "path": path, "sha256": sha(data)}

    dist = distribution("python-dateutil")
    path = "dateutil/relativedelta.py"
    data = Path(dist.locate_file(path)).read_bytes()
    if sha(data) != "73dc3c523083fb4a1b8300fb428a6196b4948caaaa0800789230c7fa360bf992":
        raise ValueError("Expected the pinned python-dateutil source")
    files["relativedelta.py"] = data.decode("utf-8")
    origin["relativedelta.py"] = {
        "kind": "observed_project_source",
        "distribution": "python-dateutil",
        "version": dist.version,
        "path": path,
        "sha256": sha(data),
    }

    for name, epath, pattern, field in [
        ("Checks.txt", "docs/evidence/next-007/windows-checks.txt", r"All (\d+) checks passed\.", "Windows checks"),
        ("Ledger.txt", "docs/evidence/next-007/ledger.txt", r"^PASS (\d+) real-source outcomes", "Ledger observations"),
        ("Package7.txt", "docs/evidence/next-007/verify-ps7.txt", r"^PASS (\d+) inventory hashes", "Verified files"),
    ]:
        data = git_file(EVIDENCE_BASE, epath)
        matches = re.findall(pattern, data.decode("utf-8"), re.M)
        if len(matches) != 1:
            raise ValueError(f"Expected one observed measurement in {epath}")
        files[name] = f"{field}: {matches[0]}\n"
        origin[name] = {
            "kind": "observed_measurement_projection",
            "revision": EVIDENCE_BASE,
            "path": epath,
            "raw_sha256": sha(data),
            "projection": pattern,
        }

    for name, data in files.items():
        origin[name]["import_sha256"] = sha(data.encode("utf-8"))
    return files, origin


def anchor(files, name, offset, end):
    return {"source": name, **span(files[name], offset, end)}


def gold(files, locations):
    result = []
    for name, line, quote in locations:
        lines = files[name].splitlines(keepends=True)
        line_start = sum(map(len, lines[:line - 1]))
        full = "".join(lines[line - 1:])
        pos = full.index(quote)
        start = line_start + pos
        result.append(anchor(files, name, start, start + len(quote)))
    return sorted(result, key=lambda x: (x["source"], x["start"]))


def search_turn(files, request, last_def=None):
    if request.startswith("Compare project claims: "):
        citations = []
        subjects = request.split(": ", 1)[1].split(" | ")
        for subject in subjects:
            matches = []
            for name, text in files.items():
                if not name.endswith((".txt", ".md")):
                    continue
                for match in re.finditer(r"^" + re.escape(subject) + r":[ \t]+(\S[^\r\n]*)", text, re.I | re.M):
                    matches.append(anchor(files, name, match.start(1), match.end(1)))
            if not matches:
                return ("insufficient", []), last_def
            citations.extend(matches)
        return (("supported", citations) if len({c["source"] for c in citations}) >= 2 else ("insufficient", [])), last_def

    if request in ("What are its parameters?", "What is its return type?", "Where is it called?"):
        # Ordinary comparator only has simple single-line header extraction and token search
        if not last_def:
            return ("unsupported", []), last_def
        fname, sym, owner = last_def
        text = files[fname]
        if request == "What are its parameters?":
            m = re.search(r"\b" + re.escape(sym) + r"(\([^()\r\n]*\))", text)
            if m:
                return ("supported", [anchor(files, fname, m.start(1), m.end(1))]), last_def
            return ("unsupported", []), last_def
        if request == "What is its return type?":
            m = re.search(r"\b(?:public|private|internal|static)\s+([\w<>?\[\]]+)\s+" + re.escape(sym) + r"\s*\(", text)
            if m:
                return ("supported", [anchor(files, fname, m.start(1), m.end(1))]), last_def
            return ("unsupported", []), last_def
        # "Where is it called?" naive search: finds any word/method call across other files
        cites = []
        for f, t in files.items():
            if f == fname or not f.endswith((".cs", ".py")):
                continue
            for m in re.finditer(r"\b(?:" + re.escape(owner) + r"\.)?" + re.escape(sym) + r"\(([^()\r\n]*)\)", t):
                cites.append(anchor(files, f, m.start(), m.end()))
        return (("supported", cites) if cites else ("unsupported", [])), last_def

    m_simple = re.fullmatch(r"Where is (\w+) defined\?", request)
    if m_simple:
        sym = m_simple.group(1)
        decls = []
        for f, t in files.items():
            for match in re.finditer(r"\bclass\s+(" + re.escape(sym) + r")\b", t):
                decls.append((f, sym, sym, anchor(files, f, match.start(1), match.end(1))))
        if len(decls) == 1:
            return ("supported", [decls[0][3]]), (decls[0][0], sym, sym)
        return ("unsupported", []), None

    m = re.fullmatch(r"Where is (\w+)\.(\w+)\(([^()]*)\) defined\?", request)
    if not m:
        return ("unsupported", []), None
    owner, name, selector = m.groups()
    decls = []
    for f, t in files.items():
        if not f.endswith((".cs", ".py")):
            continue
        owner_m = re.search(r"\bclass\s+" + re.escape(owner) + r"\b", t)
        if not owner_m:
            continue
        for match in re.finditer(r"\b(" + re.escape(name) + r")\(([^()\r\n]*)\)", t[owner_m.end():]):
            params = [p.strip().split()[0] for p in match[2].split(",") if p.strip()]
            sel_arity = re.fullmatch(r"(\d+)\s+parameters?", selector)
            wanted = [p.strip() for p in selector.split(",")] if selector else []
            if (len(params) == int(sel_arity[1])) if sel_arity else (params == wanted):
                decls.append((f, name, owner, anchor(files, f, owner_m.end() + match.start(1), owner_m.end() + match.end(1))))
    if len(decls) != 1:
        return (("ambiguous" if decls else "unsupported"), []), None
    return ("supported", [decls[0][3]]), (decls[0][0], name, owner)


def turn_outcome(actual, expected, expected_spans):
    verdict, citations = actual
    sorted_citations = sorted(citations, key=lambda x: (x["source"], x["start"]))
    if verdict == "supported":
        return "exact_delivery" if expected == verdict and sorted_citations == expected_spans else "wrong_delivery"
    if expected == "supported":
        return "false_abstention"
    return "correct_withhold" if verdict == expected else "verdict_mismatch"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("policy", choices=["search"])
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    files, origin = sources()
    trials = []
    for cid, names, turns in CASES:
        selected = {n: files[n] for n in names}
        rep_times = []
        turn_results = None
        for _ in range(4):
            t0 = time.perf_counter()
            last_def = None
            cur_turns = []
            for q, _, _ in turns:
                res, last_def = search_turn(selected, q, last_def)
                cur_turns.append(res)
            rep_times.append((time.perf_counter() - t0) * 1000)
            turn_results = cur_turns

        scored_turns = []
        for (q, exp_v, exp_locs), act in zip(turns, turn_results, strict=True):
            exp_spans = gold(selected, exp_locs)
            scored_turns.append({
                "question": q,
                "expected_verdict": exp_v,
                "expected_citations": exp_spans,
                "verdict": act[0],
                "citations": act[1],
                "outcome": turn_outcome(act, exp_v, exp_spans),
            })
        trials.append({
            "id": cid,
            "provenance": {n: origin[n] for n in names},
            "turns": scored_turns,
            "complete": all(t["outcome"] in ("exact_delivery", "correct_withhold") for t in scored_turns),
            "milliseconds": rep_times,
        })

    flat = [t["outcome"] for tr in trials for t in tr["turns"]]
    counts = {k: flat.count(k) for k in ("exact_delivery", "wrong_delivery", "false_abstention", "correct_withhold", "verdict_mismatch")}
    report = {
        "schema": 1,
        "policy": args.policy,
        "baseline": BASE,
        "external_inference_calls": 0,
        "conversations_complete": sum(1 for tr in trials if tr["complete"]),
        "conversations_total": len(trials),
        "turn_counts": counts,
        "first_ms": statistics.median(tr["milliseconds"][0] for tr in trials),
        "warm_ms": statistics.median(v for tr in trials for v in tr["milliseconds"][1:]),
        "working_set_bytes": working_set(),
        "trials": trials,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"conversations": f"{report['conversations_complete']}/{report['conversations_total']}", **counts}))


if __name__ == "__main__":
    main()
