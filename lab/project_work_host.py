"""Score NEXT-007 host observations and validate the public real-source outcome ledger."""
import argparse
import json
from pathlib import Path, PureWindowsPath
import re
import statistics
import subprocess
import tempfile
import time

from project_work import BASE, CASES, ROOT, gold, outcome, sha, sources

FROZEN = ROOT / "docs/evidence/next-007/search-baseline.json"
EXE = ROOT / "checks/bin/Release/net10.0-windows/AtlasNext.Checks.exe"
PURPOSE = {"independent_evaluation": True, "training": False, "private_qualification": False}
ORIGINS = ("observed_project_source","observed_measurement_projection")
AUTHOR_PROVENANCE = "public_author_requests_and_exact_source_oracle"


def check(condition, message):
    if not condition:
        raise ValueError(message)


def quote(files, ids, citation):
    name = citation["source"]
    anchor = citation["span"]
    start,length = anchor["start"],anchor["length"]
    path = PureWindowsPath(anchor["path"])
    payload = files[name].encode("utf-16-le")
    check(citation["id"] == ids[name] and citation["version"] == 1 and
          citation["digest"] == sha(files[name].encode()) and path.name == name and
          path.parts == ("knowledge",ids[name],f"00000001-{citation['digest']}",name) and
          type(start) is type(length) is int and 0 <= start < start+length <= len(payload)//2,
          "Wrong source identity or span")
    prefix = payload[:start*2].decode("utf-16-le")
    check(anchor["line"]==len(re.findall(r"\r\n|\r|\n",prefix))+1,"Wrong source line")
    return dict(source=name,line=anchor["line"],start=start,length=length,
                text=payload[start*2:(start+length)*2].decode("utf-16-le"))


def inspect(run, files, ids, expected_request=None):
    receipt = run["delivered"]
    review = receipt["researchReview"]
    check(receipt["proposal"]["qualification"]=="shadow","Unexpected active model")
    if expected_request is not None:
        check(receipt.get("request")==expected_request,"Delivered receipt request differs from frozen case")
    verdict = review["verdict"] if review else receipt["state"]
    citations = review["sources"] if review else []
    extracted = [quote(files,ids,c) for c in citations]
    if receipt["state"] != "completed":
        check(receipt["answerRelevance"]!="supported","Withheld answer labeled supported")
        check(receipt["verification"] in ("answer withheld","failed"),"Unexpected terminal state")
        check(verdict!="supported","Supported verdict on withheld public outcome")
        return (verdict,[])

    check(run["submitted"]=="review" and receipt["capability"]=="read owner-imported text" and
          receipt["authorization"]=="allowed scoped record read" and receipt["scope"]=="owner-local" and
          receipt["sourceIntegrity"]=="passed" and receipt["answerRelevance"]==verdict=="supported" and
          receipt["evidence"]==citations[0],"Incomplete delivery authority or source integrity")
    if receipt["specialist"]=="code.calls":
        check(citations[0]["type"]=="code.definition" and all(c["type"]=="code.calls" for c in citations[1:]),
              "Unbound call citations")
        extracted = extracted[1:]
        body = "\n".join(f"{c['source']}, line {c['line']}: {c['text']}" for c in extracted)
    elif receipt["specialist"]=="research.compare":
        keys = [key.strip().lower() for key in receipt["request"].split(":",1)[1].split("|")]
        claims=review["claims"]
        check(keys==[c["subject"] for c in claims] and all(c["verdict"]=="supported" and c["sources"] for c in claims)
              and sum((c["sources"] for c in claims),[])==citations,"Wrong claim grouping")
        body = "\n".join(f"{claim['subject']}: {quote(files,ids,claim['sources'][0])['text']}" for claim in claims)
    else:
        check(len(citations)==1 and citations[0]["type"]=="code.definition","Wrong declaration evidence")
        body = extracted[0]["text"]
    check(body==receipt["result"],"Displayed result differs from citation spans")
    return verdict,extracted


def collect():
    original=json.loads(FROZEN.read_text(encoding="utf-8"))
    search={t["id"]:t for t in original["trials"]}
    files, origin=sources()
    results=[]
    for identity,names,question,verdict,locations in CASES:
        selected={name:files[name] for name in names}
        expected=gold(selected,locations)
        old=search[identity]
        check(old["expected_verdict"]==verdict and old["expected_citations"]==expected and
              old["question"]==question and old["provenance"]=={name:origin[name] for name in names},"Preregistered source or oracle mismatch")
        with tempfile.TemporaryDirectory() as tmp:
            start=time.perf_counter()
            raw=subprocess.run([EXE,"--project-probe",tmp],input=json.dumps(dict(question=question,files=selected)),
                               encoding="utf-8",capture_output=True,check=True,timeout=60)
            process_ms=(time.perf_counter()-start)*1000
        probe=json.loads(raw.stdout)
        runs=probe["runs"]
        check(len(runs)==4,"Expected four attempts")
        actual=[inspect(run,selected,probe["ids"],question) for run in runs]
        check(all(a==actual[0] for a in actual),"Non-reproducible host outputs")
        results.append(dict(id=identity,question=question,provenance=old["provenance"],ids=probe["ids"],
                            expected_verdict=verdict,expected_citations=expected,
                            search_verdict=old["verdict"],search_outcome=old["outcome"],
                            host_verdict=actual[0][0],host_citations=actual[0][1],
                            host_outcome=outcome(actual[0],verdict,expected),process_ms=process_ms,runs=runs))
    return results


def ledger_record(row):
    observed=all(value["kind"] in ORIGINS for value in row["provenance"].values())
    if not observed:
        return None
    return {key:row[key] for key in ("id","question","provenance","ids","expected_verdict","expected_citations",
            "search_verdict","search_outcome","host_verdict","host_citations","host_outcome","process_ms","runs")}


def validate(ledger):
    expected_keys={"schema","baseline","product","source_report_sha256","observations","eligibility","external_inference_calls","labels"}
    check(set(ledger)==expected_keys and ledger["schema"]==1 and ledger["baseline"]==BASE and
          ledger["eligibility"]==PURPOSE and ledger["external_inference_calls"]==0 and
          ledger["labels"]==AUTHOR_PROVENANCE,"Unexpected ledger schema or eligibility")
    check(re.fullmatch(r"[0-9a-f]{40}",ledger["product"]) and
          re.fullmatch(r"[0-9a-f]{64}",ledger["source_report_sha256"]),"Missing immutable report provenance")
    files, origins=sources()
    by_id={identity:(names,question,verdict,locations) for identity,names,question,verdict,locations in CASES
           if all(origins[name]["kind"] in ORIGINS for name in names)}
    rows=ledger["observations"]
    check(isinstance(rows,list) and len(rows)==len(by_id) and set(row["id"] for row in rows)==set(by_id),
          "Missing, duplicate or constructed ledger observations")
    frozen={row["id"]:row for row in json.loads(FROZEN.read_text(encoding="utf-8"))["trials"]}
    for row in rows:
        names,question,verdict,locations=by_id[row["id"]]
        check(ledger_record(row)==row and row["question"]==question and set(row["ids"])==set(names) and
              row["provenance"]=={name:origins[name] for name in names},"Source or request provenance mismatch")
        expected=gold(files,locations)
        check(row["expected_citations"]==expected and row["expected_verdict"]==verdict and
              row["search_outcome"]==frozen[row["id"]]["outcome"] and row["search_verdict"]==frozen[row["id"]]["verdict"],
              "Ledger oracle or comparator mismatch")
        check(type(row["process_ms"]) in (int,float) and 0<row["process_ms"]<60000 and len(row["runs"])==4,
              "Invalid process measurements")
        for run in row["runs"]:
            check(type(run["ms"]) in (int,float) and 0<=run["ms"]<60000 and
                  type(run["workingSetBytes"])==int and run["workingSetBytes"]>0,"Invalid resource measurements")
            actual=inspect(run,{name:files[name] for name in names},row["ids"],question)
            check(actual==(row["host_verdict"],row["host_citations"]) and outcome(actual,verdict,expected)==row["host_outcome"],
                  "Invalid observed outcome")


def write(path,value):
    path.write_bytes((json.dumps(value,ensure_ascii=False,indent=2)+"\n").encode())


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("report",type=Path)
    parser.add_argument("ledger",type=Path)
    parser.add_argument("--verify-only",action="store_true")
    args=parser.parse_args()
    if args.verify_only:
        ledger=json.loads(args.ledger.read_bytes())
        validate(ledger)
        report=json.loads(args.report.read_bytes())
        check(ledger["source_report_sha256"]==sha(args.report.read_bytes()) and report["product"]==ledger["product"] and
              ledger["observations"]==[row for row in report["trials"] if ledger_record(row) is not None],"Ledger differs from host report")
        print(f"PASS {len(ledger['observations'])} real-source outcomes and eligibility")
        return
    rows=collect()
    product=subprocess.check_output(["git","rev-parse","HEAD"],cwd=ROOT,text=True).strip()
    counts={policy:{result:sum(row[f"{policy}_outcome"]==result for row in rows)
                   for result in ("exact_delivery","wrong_delivery","false_abstention","correct_withhold","verdict_mismatch")}
            for policy in ("search","host")}
    report=dict(schema=1,baseline=BASE,product=product,external_inference_calls=0,counts=counts,
                first_ms=statistics.median(r["runs"][0]["ms"] for r in rows),
                warm_ms=statistics.median(run["ms"] for r in rows for run in r["runs"][1:]),
                process_ms=statistics.median(r["process_ms"] for r in rows),
                max_sampled_working_set=max(run["workingSetBytes"] for r in rows for run in r["runs"]),trials=rows)
    write(args.report,report)
    ledger=dict(schema=1,baseline=BASE,product=product,source_report_sha256=sha(args.report.read_bytes()),
                labels=AUTHOR_PROVENANCE,eligibility=PURPOSE,external_inference_calls=0,
                observations=[row for row in rows if ledger_record(row) is not None])
    validate(ledger)
    write(args.ledger,ledger)
    print(json.dumps({key:value for key,value in report.items() if key!="trials"},indent=2))


if __name__=="__main__":
    main()
