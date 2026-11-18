"""NEXT-007 frozen public cases, source inputs and ordinary-search comparator."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import statistics
import subprocess
import tempfile
import time
from importlib.metadata import distribution
from code_navigation import ROOT, span, working_set

BASE = "c0e138f63ef342d5ff38a6bce1dd9cd579d1b01c"
CODE = {
    "Sensor.cs": """static class Sensor
{
    public static int Read() => 1;
    public static int Read(int n) => n;
    public static int Read(string s) => s.Length;
}
""",
    "Caller.cs": """static class Caller
{
    public static int Test() => Sensor.Read(42);
    public static string Text() => "Sensor.Read(43)";
    // Sensor.Read(44);
    public static int Other() => Sensor.Read("different");
}
""",
    "Shadow.cs": """static class Shadow
{
    public static int Read(int n) => n;
    public static int Test() { var Sensor = new Local(); return Sensor.Read(42); }
    class Local { public int Read(int n) => n; }
}
""",
    "Duplicate.cs": "namespace Other { static class Sensor { public static int Read(int n) => n; } }\n",
    "a.txt": "Scan mode: local\n",
    "b.md": "Scan budget: 4 seconds\n",
    "conflict.md": "Scan mode: remote\n",
    "stale.md": "Valid through: 2000-01-01\nScan budget: 4 seconds\n",
    "unknown.md": "Scan budget: unconfirmed\n",
    "irrelevant.md": "Scan budget override: 4 seconds\n",
    "one.txt": "Scan mode: local\nScan budget: 4 seconds\n",
}
COMPARE = "Compare project claims: Scan mode | Scan budget"


def loc(file, line, quote):
    return (file, line, quote)


# id, source files, question, expected verdict, answer citations (supporting definition omitted)
CASES = [
    ("real-cs-definition", ["Assets.cs"], "Where is Assets.Tokens(string) defined?", "supported", [loc("Assets.cs",34,"Tokens")]),
    ("real-cs-calls", ["Assets.cs","Library.cs","Routing.cs"], "Where is Assets.Tokens(string) called?", "supported",
     [loc("Library.cs",36,"Assets.Tokens(query)"),loc("Routing.cs",29,"Assets.Tokens(text)"),loc("Routing.cs",131,"Assets.Tokens(text)")]),
    ("real-py-arity", ["relativedelta.py"], "Where is relativedelta.normalized(1 parameter) defined?", "supported",
     [loc("relativedelta.py",284,"normalized")]),
    ("real-py-dynamic", ["relativedelta.py"], "Where is relativedelta.normalized(1 parameter) called?", "unsupported", []),
    ("real-research", ["Build.txt","Package.txt"], "Compare project claims: Build warnings | Verified package files", "supported",
     [loc("Build.txt",1,"0"),loc("Package.txt",1,"519")]),
    ("typed", ["Sensor.cs"], "Where is Sensor.Read(int) defined?", "supported",[loc("Sensor.cs",4,"Read")]),
    ("zero", ["Sensor.cs"], "Where is Sensor.Read() defined?", "supported",[loc("Sensor.cs",3,"Read")]),
    ("arity-tie", ["Sensor.cs"], "Where is Sensor.Read(1 parameter) defined?", "ambiguous",[]),
    ("owner-tie", ["Sensor.cs","Duplicate.cs"], "Where is Sensor.Read(int) defined?", "ambiguous",[]),
    ("bound-calls", ["Sensor.cs","Caller.cs","Shadow.cs"], "Where is Sensor.Read(int) called?", "supported",
     [loc("Caller.cs",3,"Sensor.Read(42)")]),
    ("shadowed", ["Sensor.cs","Shadow.cs"], "Where is Sensor.Read(int) called?", "unsupported",[]),
    ("literal-claims", ["a.txt","b.md"], COMPARE, "supported",[loc("a.txt",1,"local"),loc("b.md",1,"4 seconds")]),
    ("contradiction", ["a.txt","b.md","conflict.md"], COMPARE, "contradiction",[]),
    ("stale", ["a.txt","stale.md"], COMPARE, "stale",[]),
    ("unknown", ["a.txt","unknown.md"], COMPARE, "insufficient",[]),
    ("unrelated", ["a.txt","irrelevant.md"], COMPARE, "insufficient",[]),
    ("single-file", ["one.txt"], COMPARE, "insufficient",[]),
]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def sources():
    origin = {name: {"kind":"constructed_public_control"} for name in CODE}
    files = dict(CODE)

    def git_file(path):
        return subprocess.check_output(["git","show",f"{BASE}:{path}"],cwd=ROOT)

    for name in ("Assets.cs","Library.cs","Routing.cs"):
        path = "desktop/"+name
        data = git_file(path)
        files[name] = data.decode()
        origin[name] = {"kind":"observed_project_source","revision":BASE,"path":path,"sha256":sha(data)}
    dist = distribution("python-dateutil")
    path = "dateutil/relativedelta.py"
    data = Path(dist.locate_file(path)).read_bytes()
    if sha(data) != "73dc3c523083fb4a1b8300fb428a6196b4948caaaa0800789230c7fa360bf992":
        raise ValueError("Expected the pinned python-dateutil source")
    files["relativedelta.py"] = data.decode()
    origin["relativedelta.py"] = {"kind":"observed_project_source","distribution":"python-dateutil",
                                  "version":dist.version,"path":path,"sha256":sha(data)}
    for name, path, pattern, field in [
        ("Build.txt","docs/evidence/next-006/build.txt",r"^\s*(\d+) Warning\(s\)","Build warnings"),
        ("Package.txt","docs/evidence/next-006/verify-ps51.txt",r"^PASS (\d+) inventory hashes","Verified package files"),
    ]:
        # Verification files were committed after the accepted product.
        evidence_revision = "d07f42065ed7e4f8271a2a00a71931054f981d6d"
        data = subprocess.check_output(["git","show",f"{evidence_revision}:{path}"],cwd=ROOT)
        matches = re.findall(pattern, data.decode(), re.M)
        if len(matches) != 1:
            raise ValueError(f"Expected one observed measurement in {path}")
        files[name] = f"{field}: {matches[0]}\n"
        origin[name] = {"kind":"observed_measurement_projection","revision":evidence_revision,
                        "path":path,"raw_sha256":sha(data),"projection":pattern}
    for name, data in files.items():
        origin[name]["import_sha256"] = sha(data.encode())
    return files, origin


def anchor(files, name, offset, end):
    return {"source":name, **span(files[name],offset,end)}


def gold(files, locations):
    result = []
    for name, line, quote in locations:
        lines = files[name].splitlines(keepends=True)
        start = sum(map(len,lines[:line-1])) + lines[line-1].index(quote)
        result.append(anchor(files,name,start,start+len(quote)))
    return sorted(result,key=lambda x:(x["source"],x["start"]))


def search(files, request):
    if request.startswith("Compare project claims: "):
        citations = []
        subjects = request.split(": ",1)[1].split(" | ")
        for subject in subjects:
            matches = []
            for name, text in files.items():
                if not name.endswith((".txt",".md")):
                    continue
                for match in re.finditer(r"^"+re.escape(subject)+r":[ \t]+(\S[^\r\n]*)",text,re.I|re.M):
                    matches.append(anchor(files,name,match.start(1),match.end(1)))
            if not matches or any(m["text"].lower() in ("unknown","unconfirmed","n/a","tbd") for m in matches):
                return "insufficient", []
            if len({m["text"].strip().casefold() for m in matches}) > 1:
                return "contradiction", []
            if any(re.search(r"^Valid through: (?:19|2000)",files[m["source"]],re.M) for m in matches):
                return "stale", []
            citations.extend(matches)
        return ("supported",citations) if len({c["source"] for c in citations})>=2 else ("insufficient",[])
    owner,name,selector,operation = re.fullmatch(r"Where is (\w+)\.(\w+)\(([^()]*)\) (defined|called)\?",request).groups()
    declarations = []
    for file,text in files.items():
        if not file.endswith((".cs",".py")):
            continue
        owner_line = re.search(r"\bclass "+re.escape(owner)+r"\b",text)
        if not owner_line:
            continue
        for match in re.finditer(r"\b("+re.escape(name)+r")\(([^()\r\n]*)\)\s*(?::|=>|\{|$)",text[owner_line.end():],re.M):
            params = [p.strip().split()[0] for p in match[2].split(",") if p.strip()]
            selected = re.fullmatch(r"(\d+) parameters?",selector)
            if (len(params)==int(selected[1]) if selected else params==([p.strip() for p in selector.split(",")] if selector else [])):
                declarations.append(anchor(files,file,owner_line.end()+match.start(1),owner_line.end()+match.end(1)))
    if len(declarations)!=1:
        return ("ambiguous" if declarations else "unsupported"),[]
    if operation=="defined":
        return "supported",declarations
    citations = []
    for file,text in files.items():
        if file==declarations[0]["source"]:
            continue
        for match in re.finditer(r"\b"+re.escape(owner+"."+name)+r"\(([^()\r\n]*)\)",text):
            # Ordinary search can estimate arity and literal argument types.
            args = [p.strip() for p in match[1].split(",") if p.strip()]
            wanted = [p.strip() for p in selector.split(",")] if selector else []
            count = re.fullmatch(r"(\d+) parameters?",selector)
            if len(args)!=(int(count[1]) if count else len(wanted)):
                continue
            if not count and any((a.startswith('"') and t!="string") or (a.isdecimal() and t!="int")
                                 for a,t in zip(args,wanted)):
                continue
            if text[text.rfind("\n",0,match.start())+1:match.start()].lstrip().startswith("//"):
                continue
            citations.append(anchor(files,file,*match.span()))
    return ("supported",citations) if citations else ("unsupported",[])


def outcome(actual, expected, expected_spans):
    verdict, citations = actual
    sorted_citations = sorted(citations,key=lambda x:(x["source"],x["start"]))
    if verdict=="supported":
        return "exact_delivery" if expected==verdict and sorted_citations==expected_spans else "wrong_delivery"
    if expected=="supported":
        return "false_abstention"
    return "correct_withhold" if verdict==expected else "verdict_mismatch"


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("policy",choices=["search"])
    parser.add_argument("output",type=Path)
    args=parser.parse_args()
    files, origin = sources()
    trials=[]
    for identity,names,request,verdict,locations in CASES:
        selected={n:files[n] for n in names}
        timings=[]
        for _ in range(4):
            begin=time.perf_counter()
            actual=search(selected,request)
            timings.append((time.perf_counter()-begin)*1000)
        expect=gold(selected,locations)
        trials.append(dict(id=identity, question=request, provenance={n:origin[n] for n in names},
                           expected_verdict=verdict, expected_citations=expect, verdict=actual[0],citations=actual[1],
                           outcome=outcome(actual,verdict,expect), milliseconds=timings))
    report=dict(schema=1,policy=args.policy, baseline=BASE, external_inference_calls=0,
                first_ms=statistics.median(t["milliseconds"][0] for t in trials),
                warm_ms=statistics.median(v for t in trials for v in t["milliseconds"][1:]),
                working_set_bytes=working_set(),trials=trials)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print({result:sum(t["outcome"]==result for t in trials) for result in sorted({t["outcome"] for t in trials})})


if __name__=="__main__":
    main()
