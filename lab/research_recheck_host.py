"""Run the NEXT-009 recheck oracle against the clean Windows desktop engine."""
import argparse
import json
from pathlib import Path, PureWindowsPath
import statistics
import subprocess
import tempfile
import time

from research_recheck import ROOT, QUERY, RECHECK, KEYS, cases, expected, sha

EXE = ROOT/"checks/bin/Release/net10.0-windows/AtlasNext.Checks.exe"
FROZEN = ROOT/"docs/evidence/next-009/search-baseline.json"


def inspect(run, case):
    result=run["recheck"]
    previous=run["prior"]
    review=result["researchReview"]
    assert previous["state"]=="completed" and previous["request"]==QUERY
    assert result["request"]==RECHECK and result["specialist"]=="research.recheck"
    assert result["proposal"]["qualification"]=="shadow"
    selected=case.get("select",True)
    assert result["parentTaskId"]==(previous["id"] if selected else None)
    if selected:
        assert len(result["dependencies"])==2 and result["dependencies"][0]==previous["id"].replace("-","")
        assert len(result["dependencies"][1])==64
    else:
        assert result["dependencies"]==[] and review["sources"]==[] and result["evidence"] is None
    files=case["files"] | case["update"]
    values=[]
    if review["claims"]:
        assert [claim["subject"] for claim in review["claims"]]==list(KEYS)
        assert review["sources"]==[cite for claim in review["claims"] for cite in claim["sources"]]
        for claim in review["claims"]:
            quotes=[]
            for cite in claim["sources"]:
                file=cite["source"]
                text=files[file]
                expected_version=2 if file in case["files"] and text!=case["files"][file] else 1
                span=cite["span"]
                owned=run["imported"][file]
                assert cite["digest"]==sha(text.encode()) and cite["version"]==owned["version"]==expected_version
                assert cite["id"]==owned["id"]
                assert PureWindowsPath(span["path"]).parts==("knowledge",cite["id"],f"{expected_version:08}-{cite['digest']}",file)
                raw=text.encode("utf-16-le")
                assert 0<=span["start"]<span["start"]+span["length"]<=len(raw)//2
                prefix=raw[:span["start"]*2].decode("utf-16-le")
                quote=raw[span["start"]*2:(span["start"]+span["length"])*2].decode("utf-16-le")
                assert prefix.count("\n")+1==span["line"]
                assert prefix.split("\n")[-1].strip().casefold()==claim["subject"]+":"
                quotes.append(quote)
            if result["state"]=="completed":
                assert claim["verdict"]=="supported" and quotes and len(set(quotes))==1
                values.append(quotes[0])
    if result["state"]=="completed":
        assert run["submitted"]=="review" and review["verdict"]=="supported"
        assert result["evidence"]==review["sources"][0] and result["scope"]=="owner-local"
        assert result["authorization"]=="allowed scoped record read" and result["capability"]=="read owner-imported text"
        assert result["sourceIntegrity"]== "passed" and result["answerRelevance"]=="supported"
        assert result["result"]=="\n".join(f"{key}: {value}" for key,value in zip(KEYS,values,strict=True))
    else:
        assert result["answerRelevance"]!="supported" and result["evidence"] is None
    return dict(verdict=review["verdict"],changed=review["changedClaims"] or [],values=values)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("output",type=Path)
    args=parser.parse_args()
    baseline=json.loads(FROZEN.read_bytes())
    rows=[]
    for case,frozen in zip(cases(),baseline["rows"],strict=True):
        assert case==frozen["case"]
        t=time.perf_counter_ns()
        with tempfile.TemporaryDirectory() as scratch:
            result=subprocess.run([EXE,"--research-recheck-probe",scratch],
                                  input=json.dumps(dict(original=QUERY,follow=RECHECK,files=case["files"],
                                                        update=case["update"],select=case.get("select",True))),
                                  encoding="utf-8",capture_output=True,timeout=60,check=True)
        payload=json.loads(result.stdout)
        wall=(time.perf_counter_ns()-t)/1e6
        runs=payload["runs"]
        assert len(runs)==4
        outcomes=[inspect(run,case) for run in runs]
        assert all(item==outcomes[0] for item in outcomes)
        actual=outcomes[0]
        verdict=expected(case)
        wrong=actual["verdict"]=="supported" and (verdict["verdict"]!="supported" or actual["values"]!=verdict["values"])
        abstain=actual["verdict"]!="supported" and verdict["verdict"]=="supported"
        rows.append(dict(id=case["id"],origin="observed public measurements" if "provenance" in case else "constructed control",
                         actual=actual,expected=verdict,complete=actual==verdict,wrong_delivery=wrong,false_abstention=abstain,
                         changed_mismatch=actual["changed"]!=verdict["changed"],process_ms=wall,runs=runs))
    report=dict(product=subprocess.check_output(["git","rev-parse","HEAD"],cwd=ROOT,text=True).strip(),
                successful=sum(row["complete"] for row in rows),cases=len(rows),
                wrong_deliveries=sum(row["wrong_delivery"] for row in rows),
                false_abstentions=sum(row["false_abstention"] for row in rows),
                changed_mismatches=sum(row["changed_mismatch"] for row in rows),
                first_median_ms=statistics.median(row["runs"][0]["ms"] for row in rows),
                warm_median_ms=statistics.median(run["ms"] for row in rows for run in row["runs"][1:]),
                process_median_ms=statistics.median(row["process_ms"] for row in rows),
                max_sampled_working_set_bytes=max(run["workingSetBytes"] for row in rows for run in row["runs"]),
                external_inference_calls=0,rows=rows)
    args.output.write_bytes((json.dumps(report,indent=2)+"\n").encode())
    print(json.dumps({k:v for k,v in report.items() if k!="rows"},indent=2))
    if report["successful"]!=report["cases"]:
        raise SystemExit(1)


if __name__=="__main__":
    main()
