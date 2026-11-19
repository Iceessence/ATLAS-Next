"""Frozen NEXT-009 offline research recheck cases and ordinary-search comparison."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import statistics
import subprocess
import time

from code_navigation import ROOT, working_set

REPAIR = "112f9fb071698b5d6b256184f3debda54dd9e305"
ACCEPTED = "2011be05e61a7c7107bce3496cb0bf072e262341"
RECHECK = "Recheck these claims."
KEYS = ("check count", "inventory count")
QUERY = "Compare project claims: " + " | ".join(KEYS)
SYNTHETIC = {"Checks.txt":"Check count: 20\n", "Package.txt":"Inventory count: 40\n"}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def recorded(rev, path, pattern):
    source = subprocess.check_output(["git","show",f"{rev}:{path}"],cwd=ROOT)
    match, = re.findall(pattern,source.decode(),re.M)
    return match, dict(revision=rev,path=path,raw_sha256=sha(source),projection=pattern)


def cases():
    old, old_origin = recorded("c05b083064372621289fc5e9ed86e5782ab6b4af","docs/evidence/next-007/windows-checks.txt",
                                r"^All (\d+) checks passed\.")
    new, new_origin = recorded("b3f250a2ddeab87e4323a521f9ec796f3271fbeb","docs/evidence/next-008r-followup-review/windows.txt",
                                r"^All (\d+) checks passed\.")
    size, size_origin = recorded("c05b083064372621289fc5e9ed86e5782ab6b4af","docs/evidence/next-007/verify-ps51.txt",
                                  r"^PASS (\d+) inventory hashes")
    real = {"Checks.txt":f"Check count: {old}\n","Package.txt":f"Inventory count: {size}\n"}
    origin = dict(check_count_before=old_origin,check_count_after=new_origin,inventory=size_origin)
    return [
        dict(id="real-changed-audit", files=real, update={"Checks.txt":f"Check count: {new}\n"}, provenance=origin,
             verdict="supported", changed=["check count"], values=[new,size]),
        dict(id="real-repeat-audit", files=real, update={}, provenance=origin,
             verdict="supported", changed=[], values=[old,size]),
        dict(id="same-value-new-revision", files=SYNTHETIC, update={"Checks.txt":"# Report\nCheck count: 20\n"},
             verdict="supported", changed=["check count"], values=["20","40"]),
        dict(id="new-conflict", files=SYNTHETIC, update={"Additional.txt":"Check count: 999\n"},
             verdict="contradiction", changed=["check count"], values=[]),
        dict(id="missing-claim", files=SYNTHETIC, update={"Package.txt":"Inventory override: 55\n"},
             verdict="insufficient", changed=["inventory count"], values=[]),
        dict(id="expired-source", files=SYNTHETIC, update={"Checks.txt":"Valid through: 2000-01-01\nCheck count: 20\n"},
             verdict="stale", changed=["check count"], values=[]),
        dict(id="no-selection", files=SYNTHETIC, update={}, select=False,
             verdict="unsupported", changed=[], values=[]),
    ]


def search_snapshot(files):
    settled = []
    for key in KEYS:
        passages = []
        for name, text in files.items():
            for value in re.findall(r"^"+re.escape(key)+r":[ \t]+(\S[^\r\n]*)",text,re.I|re.M):
                passages.append((name,sha(text.encode()),value))
        values = {row[2] for row in passages}
        expired = any(files[row[0]].startswith("Valid through: 2000-01-01\n") for row in passages)
        status = ("stale" if expired else "contradiction" if len(values)>1 else
                  "insufficient" if not values else "supported")
        settled.append((status, sorted(passages)))
    return settled


def ordinary(case):
    if not case.get("select",True):
        return dict(verdict="unsupported",changed=[],values=[])
    prior=search_snapshot(case["files"])
    now=search_snapshot(case["files"] | case["update"])
    statuses={row[0] for row in now}
    overall=next((v for v in ("stale","contradiction","insufficient") if v in statuses),"supported")
    return dict(verdict=overall,changed=[k for k,a,b in zip(KEYS,prior,now) if a!=b],
                values=[r[1][0][2] for r in now] if overall=="supported" else [])


def expected(case):
    return {key:case[key] for key in ("verdict","changed","values")}


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("output",type=Path)
    args=parser.parse_args()
    rows=[]
    for case in cases():
        times=[]
        for _ in range(4):
            start=time.perf_counter_ns()
            result=ordinary(case)
            times.append((time.perf_counter_ns()-start)/1e6)
        rows.append(dict(case=case,result=result,complete=result==expected(case),ms=times))
    report=dict(provisional_repair=REPAIR,accepted=ACCEPTED,comparator="repeat ordinary exact field search and compare source digests",
                successful=sum(row["complete"] for row in rows),rows=rows,
                ordinary_query_characters=len(QUERY),context_query_characters=len(RECHECK),
                first_median_ms=statistics.median(row["ms"][0] for row in rows),sampled_working_set_bytes=working_set(),
                external_inference_calls=0)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_bytes((json.dumps(report,indent=2)+"\n").encode())
    print(f"{report['successful']}/{len(rows)} ordinary-search outcomes")


if __name__=="__main__":
    main()
