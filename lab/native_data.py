"""Acquire versioned author-public training evidence through the real Windows host."""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
from tempfile import TemporaryDirectory
import time
import unicodedata

from outcomes import ROOT, probe, score, sha
from project_audit import inspect
from project_cases import cases, project_rows

SOURCES = ROOT / "lab/native_sources"
DATASET = ROOT / "out/native-first/dataset"
ROLES = ("train", "validation", "evaluation", "acquisition")
ORIGIN = "observed_project_outcome"
PERMISSION = "owner-authorized public distribution audit; experimental learning only"
ADDITIONS = {
    "pandas": ("train", "pandas/io/parsers/readers.py", "read_csv", "CSV reader", [
        ("List the CSV reader parameters.", "CSV reader parameters"),
        ("Report the release version of this distribution.", "Release version"),
        ("Which count was recorded for checked payload files?", "Checked payload files"),
        ("Tell me the wheel generator used here.", "Wheel generator"),
    ]),
    "python-dateutil": ("train", "dateutil/parser/_parser.py", "parse", "Date parser", [
        ("Open the record for the date parser declaration line.", "Date parser declaration line"),
        ("What is the declared Python requirement for the installed package?", "Python requirement"),
        ("How many missing payload files were measured?", "Missing payload files"),
        ("What wheel compatibility tags were registered during the audit?", "Wheel compatibility tags"),
    ]),
    "pycparser": ("evaluation", "pycparser/c_parser.py", "CParser.parse", "C parser entry", [
        ("Where is the C parser entry declaration line recorded?", "C parser entry declaration line"),
        ("Read the published name from installed release metadata.", "Published name"),
        ("Which value did the audit report for the pure library flag?", "Pure library flag"),
        ("Return the restore manifest digest recorded by the audit.", "Restore manifest digest"),
    ]),
    "reportlab": ("acquisition", "reportlab/pdfgen/canvas.py", "Canvas.__init__", "PDF canvas constructor", [
        ("Read the PDF canvas constructor source digest.", "PDF canvas constructor source digest"),
        ("Which release version was used for the report?", "Release version"),
        ("How many payload hash mismatches did the audit find?", "Payload hash mismatches"),
        ("What value does the audit record for the wheel generator?", "Wheel generator"),
    ]),
    "pdfplumber": ("acquisition", "pdfplumber/pdf.py", "PDF.open", "PDF opener", [
        ("Name the PDF opener parameters listed in the interface audit.", "PDF opener parameters"),
        ("Report the declared dependency count for the distribution.", "Declared dependency count"),
        ("Look up the checked payload files recorded by the audit.", "Checked payload files"),
        ("What is the recorded wheel compatibility tags value?", "Wheel compatibility tags"),
    ]),
}


def encode(data):
    return (json.dumps(data, sort_keys=True, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n").encode()


def write_new(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("xb") as stream:
        stream.write(encode(data))


def snapshot():
    if (SOURCES / "pins.json").exists():
        raise ValueError("An enrolled upstream snapshot already exists")
    pins = {}
    for family, (_, path, symbol, label, _) in ADDITIONS.items():
        receipt = inspect(family, (path, symbol, label))
        leaf = family + ".json"
        # Keep insertion order in the original measurement projection.
        data = (json.dumps(receipt, indent=2, ensure_ascii=False) + "\n").encode()
        SOURCES.mkdir(parents=True, exist_ok=True)
        with (SOURCES / leaf).open("xb") as stream:
            stream.write(data)
        pins[leaf] = sha(data)
    write_new(SOURCES / "pins.json", pins)


def audit():
    pins = json.loads((SOURCES / "pins.json").read_bytes())
    for family, (_, path, symbol, label, _) in ADDITIONS.items():
        leaf = family + ".json"
        data = (SOURCES / leaf).read_bytes()
        if sha(data) != pins[leaf] or json.loads(data) != inspect(family, (path, symbol, label)):
            raise ValueError(f"Installed audit differs for {family}")
        print(family, json.loads(data)["version"], pins[leaf])


def family_key(name):
    if not isinstance(name, str) or not re.fullmatch(r"[a-zA-Z0-9][a-zA-Z0-9._-]{0,95}", name):
        raise ValueError("Cases require a stable project family")
    return re.sub(r"[-_.]+", "-", name.casefold())


def normalized(question):
    return " ".join(unicodedata.normalize("NFKC", question).casefold().split())


def input_identity(case):
    documents = [sha(doc.encode()) for doc in case["documents"]]
    return sha(encode([case["id"], case["family"], case["question"], documents,
                       case["outcome_receipt"], PERMISSION]))


def public_cases(role):
    original = [] if role == "acquisition" else cases(role)
    pins = json.loads((SOURCES / "pins.json").read_bytes())
    if set(pins) != {family + ".json" for family in ADDITIONS}:
        raise ValueError("Unknown supplemental project")
    for family, (partition, _, _, _, requests) in ADDITIONS.items():
        if partition != role:
            continue
        leaf = family + ".json"
        path = SOURCES / leaf
        if path.is_symlink():
            raise ValueError("Native audit receipt must be a local pinned file")
        payload = path.read_bytes()
        if sha(payload) != pins[leaf]:
            raise ValueError("Native audit receipt digest mismatch")
        receipt = json.loads(payload)
        if receipt["family"] != family:
            raise ValueError("Native audit family mismatch")
        original.extend(project_rows(receipt, requests, pins[leaf]))
    return original


def validate_cases(splits):
    requests, identities, families, receipts, documents = set(), set(), {}, {}, {}
    for role, rows in splits.items():
        if not rows:
            raise ValueError("Empty dataset partition")
        for case in rows:
            receipt = case["outcome_receipt"]["sha256"]
            if case["origin"] != ORIGIN or not re.fullmatch("[a-f0-9]{64}", receipt):
                raise ValueError("Non-observed or unpinned example")
            family = family_key(case["family"])
            if families.setdefault(family, role) != role or receipts.setdefault(receipt, family) != family:
                raise ValueError("Family or receipt overlaps")
            question = normalized(case["question"])
            if not question or case["id"] in identities or question in requests:
                raise ValueError("Duplicate request identity")
            identities.add(case["id"])
            requests.add(question)
            for doc in case["documents"]:
                if documents.setdefault(sha(normalized(doc).encode()), family) != family:
                    raise ValueError("Duplicate source across project families")


def host_run(case, action, scratch=None):
    started = time.perf_counter_ns()
    child = probe(case["question"], case["documents"], scratch_dir=scratch,
                  proposed_document=action, extensions=case.get("extensions"))
    elapsed_ms = (time.perf_counter_ns() - started) / 1e6
    if child.returncode:
        raise RuntimeError(f"Outcome probe failed for {case['id']}: {child.stderr[-600:]}")
    run = json.loads(child.stdout)
    answer, ids = run["answer"], run["ids"]
    if answer["request"] != case["question"].strip() or len(ids) != len(case["documents"]) or len(set(ids)) != len(ids):
        raise ValueError("Probe did not bind to the submitted request and sources")
    candidate = answer.get("primaryCandidateId")
    if candidate in ids and action is not None and (action == -1 or candidate != ids[action]):
        raise ValueError("Probe escaped its proposed source restriction")
    return run, elapsed_ms


def observe(case, action, scratch=None):
    run, elapsed_ms = host_run(case, action, scratch)
    answer = run["answer"]
    checks = score(case, run)
    if case["expect"]["state"] == "completed" and answer["state"] == "completed":
        checks.update(owner_scope=answer["scope"] == "owner-local" and
                      answer["capability"] == "read owner-imported text" and
                      answer["authorization"] == "allowed scoped record read")
    if answer["state"] not in ("completed", "needs clarification"):
        raise ValueError("Unusable host outcome state")
    citation = answer.get("evidence") or {}
    candidate = answer.get("primaryCandidateId")
    ids = run["ids"]
    selected = ids.index(citation["id"]) if citation.get("id") in ids else None
    span = citation.get("span")
    if span:
        span = {key: span[key] for key in ("line", "start", "length")}
    stable = dict(useful=all(checks.values()), state=answer["state"], result=answer["result"],
                  verdict=(answer.get("researchReview") or {}).get("verdict"), selected=selected,
                  primary=ids.index(candidate) if candidate in ids else None, citation=span,
                  catalog_candidate=candidate if candidate not in ids else None,
                  digest=citation.get("digest"), failed=sorted(k for k, v in checks.items() if not v))
    cost = dict(engine_us=run["elapsedUs"], process_ms=elapsed_ms, host_working_set=run["workingSetBytes"])
    return stable, cost


def measure():
    splits = {role: public_cases(role) for role in ROLES}
    validate_cases(splits)
    if DATASET.exists():
        raise ValueError("Dataset already exists; preserve the previous measurements before capturing again")
    DATASET.parent.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(prefix="capture-", dir=DATASET.parent) as temporary:
        staged = Path(temporary) / "dataset"
        capture(splits, staged)
        staged.rename(DATASET)


def capture(splits, staged):
    manifest = dict(schema=1, permission=PERMISSION, origin=ORIGIN, external_inference_calls=0, splits={})
    for role, cases_in_role in splits.items():
        outcomes, timings = {}, {}
        counts = Counter()
        for case in cases_in_role:
            runs, costs = {}, {}
            for action in (None, -1, *range(len(case["documents"]))):
                run, cost = observe(case, action)
                runs[str(action)], costs[str(action)] = run, cost
                counts["useful" if run["useful"] else "wrong" if run["state"] == "completed" else "withheld"] += 1
            outcomes[case["id"]] = dict(input_sha256=input_identity(case), actions=runs)
            timings[case["id"]] = costs
        data = dict(schema=1, partition=role, rows=outcomes)
        write_new(staged / (role + ".json"), data)
        write_new(staged / (role + "-cost.json"), timings)
        manifest["splits"][role] = dict(sha256=sha(encode(data)), tasks=len(cases_in_role),
                                        families=sorted({c["family"] for c in cases_in_role}),
                                        outcomes={key: counts[key] for key in ("useful", "wrong", "withheld")})
        print(role, manifest["splits"][role], flush=True)
    write_new(staged / "manifest.json", manifest)


def load_partition(role):
    if role not in ROLES:
        raise ValueError("Expected an author-public partition")
    path = DATASET / (role + ".json")
    manifest = json.loads((DATASET / "manifest.json").read_bytes())
    pin = manifest["splits"][role]["sha256"]
    frozen = ROOT / "docs/evidence/next-016/dataset-manifest.json"
    if frozen.exists() and json.loads(frozen.read_bytes())["splits"][role]["sha256"] != pin:
        raise ValueError("Dataset differs from the frozen author measurement")
    data = path.read_bytes()
    if sha(data) != pin or manifest["permission"] != PERMISSION or manifest["origin"] != ORIGIN:
        raise ValueError("Dataset integrity or permissions mismatch")
    payload = json.loads(data)
    inputs = public_cases(role)
    validate_cases({role: inputs})
    rows = payload["rows"]
    if payload["schema"] != 1 or payload["partition"] != role or set(rows) != {c["id"] for c in inputs}:
        raise ValueError("Unexpected dataset members")
    for case in inputs:
        row = rows[case["id"]]
        if row["input_sha256"] != input_identity(case) or set(row["actions"]) != {"None", "-1", *map(str, range(len(case["documents"])))}:
            raise ValueError("Outcome belongs to different inputs or actions")
    return inputs, {key: value["actions"] for key, value in rows.items()}, pin


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["snapshot", "audit", "measure"])
    args = parser.parse_args()
    {"snapshot": snapshot, "audit": audit, "measure": measure}[args.command]()


if __name__ == "__main__":
    main()
