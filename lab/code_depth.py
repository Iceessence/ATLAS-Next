"""Frozen NEXT-006 public comparator and scoring. No target module or project is executed."""
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
import zipfile
from importlib.metadata import distribution
from code_navigation import ROOT, span, working_set

CS = "RandomNumberTools.cs"
PY = "relativedelta.py"
CONTROL = "Owners.cs"
FIXTURE = """namespace Trial;
class Alpha
{
    /// <summary>Alpha count.</summary>
    public int Count() => 1;
}
class Beta
{
    public int Count() => 2;
    public int Scan() => 0;
    public int Scan(int n) => n;
}
namespace Other { class Alpha { public void Paint() {} } }
"""
DOC = "Show its documentation."
OWNER = "What is its enclosing type?"
# Gold locations were inspected in the pinned upstream files before product changes.
# (start line, first string, last line, last string), including original line endings.
def at(line, text):
    return (line,text,line,text)


CASES = [
    (CS,"RandomNumberTools.GetRandomNumber",OWNER,at(12,"GetRandomNumber"),at(8,"RandomNumberTools")),
    (CS,"RandomNumberTools.GetRandomNumber",DOC,at(12,"GetRandomNumber"),"unsupported"),
    (CS,"RandomNumberTools",DOC,at(8,"RandomNumberTools"),(4,"/// <summary>",7,"/// </summary>")),
    (CS,"Other.GetRandomNumber",OWNER,"unsupported","unsupported"),
    (PY,"relativedelta.normalized",DOC,at(284,"normalized"),(285,'"""',294,'"""')),
    (PY,"relativedelta.normalized",OWNER,at(284,"normalized"),at(16,"relativedelta")),
    (PY,"relativedelta._set_months",DOC,at(274,"_set_months"),"unsupported"),
    (PY,"relativedelta.weeks",OWNER,"ambiguous","unsupported"),
    (CONTROL,"Alpha.Count",DOC,at(5,"Count"),at(4,"/// <summary>Alpha count.</summary>")),
    (CONTROL,"Beta.Count",OWNER,at(9,"Count"),at(7,"Beta")),
    (CONTROL,"Count",DOC,"ambiguous","unsupported"),
    (CONTROL,"Beta.Scan",OWNER,"ambiguous","unsupported"),
    (CONTROL,"Alpha",DOC,"ambiguous","unsupported"),
]


def sources():
    pack=Path(os.environ["ProgramFiles"])/"dotnet/templates/10.0.12/microsoft.mcpserver.projecttemplates.10.0.10.0.12.nupkg"
    with zipfile.ZipFile(pack) as z:
        cs=z.read("content/McpServer-CSharp/common/Tools/"+CS)
    dist=distribution("python-dateutil")
    assert dist.version=="4.0.0"
    py=Path(dist.locate_file("dateutil/"+PY)).read_bytes()
    assert hashlib.sha256(cs).hexdigest()=="481650c139bcdd26f6e23d90d154ca599c50291c75b307d1387129132f3311e1"
    assert hashlib.sha256(py).hexdigest()=="73dc3c523083fb4a1b8300fb428a6196b4948caaaa0800789230c7fa360bf992"
    return {CS:cs.decode(),PY:py.decode(),CONTROL:FIXTURE}


def gold(source, spec):
    if isinstance(spec,str):
        return spec
    line,first,last,final=spec
    lines=source.splitlines(keepends=True)
    start=sum(map(len,lines[:line-1]))+lines[line-1].index(first)
    end=sum(map(len,lines[:last-1]))+lines[last-1].index(final)+len(final)
    return span(source,start,end)


def search(source, query, follow):
    """Ordinary line search with nearest preceding class and adjacent documentation."""
    name=query.split(".")[-1]
    owner=query.partition(".")[0] if "." in query else None
    lines=source.splitlines(keepends=True)
    offsets=[0]
    for line in lines:
        offsets.append(offsets[-1]+len(line))
    hits=[]
    enclosing=None
    class_re=re.compile(r"\bclass\s+(\w+)")
    member_re=re.compile(r"^\s*(?:def\s+|(?:(?:public|protected|private|internal|static|virtual)\s+)+[\w<>?]+\s+)(\w+)\s*\(")
    for i,line in enumerate(lines):
        for m in [class_re.search(line),member_re.search(line)]:
            if m is None:
                continue
            if m[1]==name and (owner is None or enclosing and enclosing[1]==owner):
                hits.append((i,m,enclosing))
            if m.re is class_re:
                enclosing=(i,m[1],m)
    if len(hits)!=1:
        return ["ambiguous" if hits else "unsupported","unsupported"]
    i,m,c=hits[0]
    location=span(source,offsets[i]+m.start(1),offsets[i]+m.end(1))
    evidence="unsupported"
    if follow==OWNER and c:
        j,_,decl=c
        evidence=span(source,offsets[j]+decl.start(1),offsets[j]+decl.end(1))
    elif follow==DOC:
        j=i
        while j and lines[j-1].lstrip().startswith("///"):
            j-=1
        if j<i:
            evidence=span(source,offsets[j]+lines[j].index("///"),offsets[i]-len(lines[i-1])+len(lines[i-1].rstrip("\r\n")))
        elif i+1<len(lines) and lines[i+1].lstrip().startswith(('"""',"'''")):
            start=offsets[i+1]+len(lines[i+1])-len(lines[i+1].lstrip())
            end=source.find(source[start:start+3],start+3)
            if end!=-1:
                evidence=span(source,start,end+3)
    return [location,evidence]


def host(name,source,symbol,follow):
    exe=ROOT/"checks/bin/Release/net10.0-windows/AtlasNext.Checks.exe"
    with tempfile.TemporaryDirectory() as tmp:
        result=subprocess.run([exe,"--code-probe",tmp],input=json.dumps(dict(name=name,source=source,symbol=symbol,
            follow=follow,repeats=4)),encoding="utf-8",capture_output=True,timeout=60)
    if result.returncode:
        raise RuntimeError(result.stderr)
    row=json.loads(result.stdout)
    results=[]
    for run in row["runs"]:
        pair=[]
        for t in run["turns"]:
            if t["state"]=="needs clarification" and t["evidence"] is None:
                pair.append(t["researchReview"]["verdict"])
            else:
                c=t["evidence"]
                assert t["state"]=="completed" and t["answerRelevance"]=="supported" and t["sourceIntegrity"]=="passed"
                assert c["source"]==name and c["version"]==1 and c["digest"]==hashlib.sha256(source.encode()).hexdigest()
                pair.append({k:c["span"][k] for k in ("line","start","length")} | {"text":t["result"]})
        assert run["turns"][1]["parentTaskId"]==run["turns"][0]["id"]
        results.append(pair)
    assert len(results)==4 and all(pair==results[0] for pair in results)
    return results[0],[run["turnMs"] for run in row["runs"]],row["workingSetBytes"]


def run(policy):
    data=sources()
    rows=[]
    for filename,symbol,question,loc,info in CASES:
        source=data[filename]
        expected=[gold(source,loc),gold(source,info)]
        start=time.perf_counter_ns()
        if policy=="host":
            answers,times,rss=host(filename,source,symbol,question)
        else:
            times=[]
            for _ in range(4):
                t=time.perf_counter_ns()
                answers=search(source,symbol,question)
                times.append([(time.perf_counter_ns()-t)/1e6])
            rss=working_set()
        rows.append(dict(file=filename,symbol=symbol,follow=question,expected=expected,actual=answers,ms=times,
                         four_conversations_wall_ms=(time.perf_counter_ns()-start)/1e6,rss=rss,
                         source_sha256=hashlib.sha256(source.encode()).hexdigest(),
                         origin="author_fixture" if filename==CONTROL else "observed_source_author_request"))
    pairs=[(a,e) for row in rows for a,e in zip(row["actual"],row["expected"],strict=True)]
    return dict(policy=policy,conversations=len(rows),complete=sum(r["actual"]==r["expected"] for r in rows),
                exact_deliveries=sum(isinstance(a,dict) and a==e for a,e in pairs),
                wrong_deliveries=sum(isinstance(a,dict) and a!=e for a,e in pairs),
                correct_withholds=sum(isinstance(a,str) and a==e for a,e in pairs),
                first_ms=[statistics.median(r["ms"][0][i] for r in rows) for i in range(len(rows[0]["ms"][0]))],
                warm_ms=[statistics.median(t[i] for r in rows for t in r["ms"][1:]) for i in range(len(rows[0]["ms"][0]))],
                max_sampled_working_set=max(r["rss"] for r in rows),external_inference_calls=0,trials=rows)


if __name__=="__main__":
    p=argparse.ArgumentParser()
    p.add_argument("policy",choices=["search","host"])
    p.add_argument("report",type=Path)
    args=p.parse_args()
    result=run(args.policy)
    args.report.parent.mkdir(parents=True,exist_ok=True)
    args.report.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
    print({k:v for k,v in result.items() if k!="trials"})
