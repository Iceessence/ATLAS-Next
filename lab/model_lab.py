"""Offline author-public evaluation and eligibility receipt. This entry point accepts no suite path."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time

from outcomes import ROOT, sha
from project_cases import cases
from project_specialist import CHECKPOINT, benchmark
from project_audit import DATA

PROTOCOL = "dc681ca58b6aa3be6eb5a996dbb527657254fc99"
LEDGER_REVISION = "ca4926d2bc69bebbe1086f6298938be2782a351d"
LEDGER_PATH = "docs/evidence/next-008/eligible-outcomes.json"
ROLES = ("train","validation","evaluation")
POLICIES = ("host","tfidf","linear","native")


def validate_splits(splits):
    inventory = {}
    seen_families = set()
    seen_questions = set()
    seen_receipts = set()
    for role in ROLES:
        rows = splits[role]
        if not rows or any(row["origin"]!="observed_project_outcome" for row in rows):
            raise ValueError("Split must contain pinned public observed outcomes.")
        families = {row["family"].casefold() for row in rows}
        questions = {" ".join(row["question"].casefold().split()) for row in rows}
        receipts = {row["outcome_receipt"]["sha256"] for row in rows}
        if seen_families & families or seen_questions & questions or seen_receipts & receipts:
            raise ValueError("Public split leakage detected.")
        seen_families.update(families)
        seen_questions.update(questions)
        seen_receipts.update(receipts)
        inventory[role] = dict(outcomes=len(rows), families=sorted(families), sourceReceipts=len(receipts))
    return inventory


def inspect_ledger(snapshot):
    ledger = json.loads(snapshot)
    eligibility = ledger["eligibility"]
    if eligibility != {"independent_evaluation": True, "training": False, "private_qualification": False}:
        raise ValueError("Unexpected public conversation ledger permissions.")
    observations = ledger["observations"]
    origins = {"observed_project_source","observed_measurement_projection"}
    if not observations or any(not row["provenance"] or any(p["kind"] not in origins for p in row["provenance"].values())
                               for row in observations):
        raise ValueError("Unexpected public conversation provenance.")
    return len(observations)


def collect():
    pinned = subprocess.check_output(["git","show",f"{LEDGER_REVISION}:{LEDGER_PATH}"],cwd=ROOT)
    preregistered = subprocess.check_output(["git","show",f"{PROTOCOL}:lab/next-batch-protocol.md"],cwd=ROOT)
    split = validate_splits({role:cases(role) for role in ROLES})
    for role in ROLES:
        split[role]["pinsSha256"] = sha((DATA/role/"pins.json").read_bytes())
    count = inspect_ledger(pinned)
    checkpoint = CHECKPOINT.read_bytes()
    started = time.perf_counter()
    evaluations = benchmark()
    runtime = time.perf_counter()-started
    if checkpoint != CHECKPOINT.read_bytes():
        raise ValueError("Evaluation changed the checkpoint.")
    metrics = {policy:evaluations[policy]["summary"] for policy in POLICIES}
    return dict(schema=1, name="NEXT-014 specialist trial",
                qualification="shadow", fitStatus="blocked",
                fitReason="The newly measured conversation ledger grants no training eligibility. A fresh independently held comparison has not enrolled.",
                observedWeakness=f"Native assisted retrieval completed {metrics['native']['useful']}/{metrics['native']['total']} prior public trials. "
                f"The deterministic host completed {metrics['host']['useful']}/{metrics['host']['total']}.",
                publicConversationRows=count, newlyEligibleTrainingRows=0, protocol=PROTOCOL,
                protocolSha256=sha(preregistered), ledgerSha256=sha(pinned), checkpointSha256=sha(checkpoint),
                splits=split, evaluationSeconds=runtime, metrics=metrics, externalInferenceCalls=0)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("report", type=Path)
    p.add_argument("--fit",action="store_true",help="Ask whether this trial can fit. If blocked, keep the diagnostic receipt.")
    args=p.parse_args()
    if os.environ.get("OPENBLAS_NUM_THREADS")!="1":
        p.error("Set OPENBLAS_NUM_THREADS=1 for reproducible CPU evaluation.")
    if args.report.exists():
        p.error("Choose a new report path to preserve experiment history.")
    report=collect()
    with args.report.open("x",encoding="utf-8",newline="\n") as stream:
        json.dump(report,stream,indent=2,allow_nan=False)
        stream.write("\n")
    print(f"{report['fitStatus']}: {report['fitReason']}")
    return 2 if args.fit else 0


if __name__=="__main__":
    sys.exit(main())
