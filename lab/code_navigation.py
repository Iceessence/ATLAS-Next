"""Public NEXT-005 benchmark. Read installed source as data. Never import target modules."""
import argparse
import ast
import ctypes
from ctypes import wintypes
import hashlib
from importlib.metadata import distribution
import io
import json
from pathlib import Path
import re
import statistics
import subprocess
import tempfile
import time
import tokenize

ROOT = Path(__file__).resolve().parents[1]
FILES = [
    ("pyhumps", "3.8.0", "humps/main.py", "d026ffe1c1642424f4195ed52b17569e802f32ebef664061181aa28adad7bb43",
     ["pascalize", "decamelize", "_process_keys", "absent_symbol"]),
    ("pdf2image", "1.17.0", "pdf2image/pdf2image.py", "3118a70fcc5375b312909808537db2fb1683c81bba4e8eb84696f182e669dd8f",
     ["convert_from_path", "pdfinfo_from_path", "_parse_format", "absent_symbol"]),
]


def span(source, start, end):
    def units(s):
        return len(s.encode("utf-16-le")) // 2
    return dict(line=source[:start].count("\n")+1, start=units(source[:start]),
                length=units(source[start:end]), text=source[start:end])


def oracle(source, name):
    """Independent standard-library AST identity plus tokenize header/identifier boundaries."""
    matches = [n for n in ast.walk(ast.parse(source)) if
               isinstance(n, (ast.ClassDef, ast.FunctionDef, ast.AsyncFunctionDef)) and n.name == name]
    if not matches:
        return [None, None]
    node, = matches
    lines = source.splitlines(keepends=True)
    def offset(pos):
        return sum(map(len, lines[:pos[0]-1]))+pos[1]
    header = sum(map(len, lines[:node.lineno-1])) + len(lines[node.lineno-1].encode()[:node.col_offset].decode())
    tokens = iter(tokenize.generate_tokens(io.StringIO(source[header:]).readline))
    first = next(tokens)
    if first.string == "async":
        next(tokens)
    ident = next(tokens)
    assert ident.string == name
    depth = 0
    for token in tokens:
        if token.type == tokenize.OP:
            if token.string == ":" and depth == 0:
                end = (node.lineno + token.end[0]-1, token.end[1] + (node.col_offset if token.end[0]==1 else 0))
                break
            depth += int(token.string in ("(", "[", "{")) - int(token.string in (")", "]", "}"))
    return [span(source, header+ident.start[1], header+ident.end[1]), span(source, header, offset(end))]


def local_search(source, name):
    """Ordinary anchored declaration-line search with a uniqueness check and one line of context."""
    matches = list(re.finditer(r"(?m)^[ \t]*((?:async[ \t]+)?(?:class|def)[ \t]+("+re.escape(name)+r")\b[^\r\n]*)", source))
    if len(matches) != 1:
        return [None, None]
    m = matches[0]
    return [span(source,*m.span(2)), span(source,*m.span(1))]


def working_set():
    class Counters(ctypes.Structure):
        _fields_ = [("size",wintypes.DWORD),("faults",wintypes.DWORD)] + [
            (name,ctypes.c_size_t) for name in ("peak","current","pp","p","np","n","page","peakpage")]
    data = Counters()
    data.size = ctypes.sizeof(data)
    if not ctypes.windll.psapi.GetProcessMemoryInfo(ctypes.c_void_p(-1),ctypes.byref(data),data.size):
        raise OSError("GetProcessMemoryInfo")
    return data.current


def run(policy):
    trials = []
    for project,version,relative,digest,names in FILES:
        dist = distribution(project)
        raw = Path(dist.locate_file(relative)).read_bytes()
        assert dist.version == version and hashlib.sha256(raw).hexdigest() == digest
        source = raw.decode("utf-8")
        for name in names:
            expected = oracle(source,name)
            started = time.perf_counter_ns()
            if policy == "search":
                actual = local_search(source,name)
                rss = working_set()
                times = None
            else:
                payload = dict(name=Path(relative).name,source=source,symbol=name)
                exe = ROOT/"checks/bin/Release/net10.0-windows/AtlasNext.Checks.exe"
                with tempfile.TemporaryDirectory() as scratch:
                    proc = subprocess.run([str(exe),"--code-probe",scratch],input=json.dumps(payload),
                                          encoding="utf-8",capture_output=True,check=True,timeout=60)
                row = json.loads(proc.stdout)
                actual = []
                for task in row["turns"]:
                    if task["state"] == "needs clarification" and task["researchReview"]["verdict"] == "unsupported" and task["evidence"] is None:
                        actual.append(None)
                        continue
                    cite = task["evidence"]
                    assert task["state"]=="completed" and task["answerRelevance"]=="supported" and task["sourceIntegrity"]=="passed"
                    assert cite["digest"]==digest and cite["version"]==1 and cite["source"]==Path(relative).name
                    actual.append({k:cite["span"][k] for k in ("line","start","length")} | {"text":task["result"]})
                rss,times = row["workingSetBytes"],row["turnMs"]
            wall = (time.perf_counter_ns()-started)/1e6
            trials.append(dict(project=project,version=version,path=relative,source_sha256=digest,symbol=name,
                               expected=expected,actual=actual,wall_ms=wall,turn_ms=times,working_set=rss))
    pairs=[(a,e) for t in trials for a,e in zip(t["actual"],t["expected"],strict=True)]
    return dict(policy=policy,origin="author-public source observation",external_inference_calls=0,
                author_and_evaluator_assistance="external developer authored requests, harness and implementation",
                turns=len(pairs),verified_deliveries=sum(a==e and e is not None for a,e in pairs),
                wrong_deliveries=sum(a is not None and a!=e for a,e in pairs),
                correct_abstentions=sum(a is None and e is None for a,e in pairs),
                conversations_correct=sum(t["actual"]==t["expected"] for t in trials),
                median_wall_ms=statistics.median(t["wall_ms"] for t in trials),
                max_working_set=max(t["working_set"] for t in trials),trials=trials)


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("policy",choices=["search","host"])
    parser.add_argument("report",type=Path)
    args=parser.parse_args()
    report=run(args.policy)
    args.report.parent.mkdir(parents=True,exist_ok=True)
    args.report.write_text(json.dumps(report,indent=2)+"\n",encoding="utf-8")
    print({k:v for k,v in report.items() if k!="trials"})
