"""Score NEXT-008 conversational host observations and validate the multi-turn real-source outcome ledger."""
import argparse
import json
from pathlib import Path, PureWindowsPath
import re
import statistics
import subprocess
import tempfile
import time

from code_conversation import BASE, CASES, ROOT, gold, sha, sources, turn_outcome

FROZEN = ROOT / "docs/evidence/next-008/search-baseline.json"
EXE = ROOT / "checks/bin/Release/net10.0-windows/AtlasNext.Checks.exe"
PURPOSE = {"independent_evaluation": True, "training": False, "private_qualification": False}
ORIGINS = ("observed_project_source", "observed_measurement_projection")
AUTHOR_PROVENANCE = "public_author_requests_and_exact_source_oracle"


def check(condition, message):
    if not condition:
        raise ValueError(message)


def quote(files, ids, citation):
    name = citation["source"]
    anchor = citation["span"]
    start, length = anchor["start"], anchor["length"]
    path = PureWindowsPath(anchor["path"])
    payload = files[name].encode("utf-16-le")
    check(citation["id"] == ids[name] and citation["version"] == 1 and
          citation["digest"] == sha(files[name].encode("utf-8")) and path.name == name and
          path.parts == ("knowledge", ids[name], f"00000001-{citation['digest']}", name) and
          type(start) is type(length) is int and 0 <= start < start + length <= len(payload) // 2,
          "Wrong source identity or span")
    prefix = payload[:start * 2].decode("utf-16-le")
    check(anchor["line"] == len(re.findall(r"\r\n|\r|\n", prefix)) + 1, "Wrong source line")
    return dict(source=name, line=anchor["line"], start=start, length=length,
                text=payload[start * 2:(start + length) * 2].decode("utf-16-le"))


def inspect_turn(turn, files, ids, prev_id=None, expect_followup=False):
    receipt = turn["delivered"]
    review = receipt["researchReview"]
    check(receipt["proposal"]["qualification"] == "shadow", "Unexpected active model")
    if expect_followup and prev_id is not None:
        check(receipt["parentTaskId"] == prev_id, "Missing follow-up parentTaskId link")
    verdict = review["verdict"] if review else receipt["state"]
    citations = review["sources"] if review else []
    extracted = [quote(files, ids, c) for c in citations]
    if receipt["state"] != "completed":
        check(receipt["answerRelevance"] != "supported", "Withheld answer labeled supported")
        check(receipt["verification"] in ("answer withheld", "failed"), "Unexpected terminal state")
        check(verdict != "supported", "Supported verdict on withheld public outcome")
        return (verdict, []), receipt["id"]

    check(turn["submitted"] == "review" and receipt["capability"] == "read owner-imported text" and
          receipt["authorization"] == "allowed scoped record read" and receipt["scope"] == "owner-local" and
          receipt["sourceIntegrity"] == "passed" and receipt["answerRelevance"] == verdict == "supported" and
          receipt["evidence"] == citations[0], "Incomplete delivery authority or source integrity")

    specialist = receipt["specialist"]
    if specialist == "code.calls":
        check(citations[0]["type"] == "code.definition" and all(c["type"] == "code.calls" for c in citations[1:]),
              "Unbound call citations")
        if expect_followup and prev_id is not None:
            check(receipt["dependencies"] == [prev_id.replace("-", "")], "Missing call follow-up dependency")
        extracted = extracted[1:]
        body = "\n".join(f"{c['source']}, line {c['line']}: {c['text']}" for c in extracted)
    elif specialist == "research.compare":
        keys = [key.strip().lower() for key in receipt["request"].split(":", 1)[1].split("|")]
        claims = review["claims"]
        check(keys == [c["subject"] for c in claims] and all(c["verdict"] == "supported" and c["sources"] for c in claims)
              and sum((c["sources"] for c in claims), []) == citations, "Wrong claim grouping")
        body = "\n".join(f"{claim['subject']}: {quote(files, ids, claim['sources'][0])['text']}" for claim in claims)
    else:
        check(len(citations) == 1 and citations[0]["type"] == specialist, "Wrong code turn citation type")
        body = extracted[0]["text"]
    check(body == receipt["result"], "Displayed result differs from citation spans")
    return (verdict, extracted), receipt["id"]


def inspect_run(run, files, ids, turn_specs):
    actuals = []
    prev_id = None
    for idx, (turn, (req, _, _)) in enumerate(zip(run["turns"], turn_specs, strict=True)):
        is_followup = idx > 0 and req in ("Where is it called?", "What are its parameters?", "What is its return type?")
        act, cur_id = inspect_turn(turn, files, ids, prev_id, expect_followup=is_followup)
        actuals.append(act)
        prev_id = cur_id
    return actuals


def collect():
    original = json.loads(FROZEN.read_text(encoding="utf-8"))
    search = {t["id"]: t for t in original["trials"]}
    files, origin = sources()
    results = []
    for cid, names, turn_specs in CASES:
        selected = {name: files[name] for name in names}
        old = search[cid]
        check(old["provenance"] == {name: origin[name] for name in names}, "Preregistered source provenance mismatch")
        questions = [q for q, _, _ in turn_specs]
        with tempfile.TemporaryDirectory() as tmp:
            start = time.perf_counter()
            raw = subprocess.run([EXE, "--conversation-probe", tmp],
                                 input=json.dumps(dict(turns=questions, files=selected)),
                                 encoding="utf-8", capture_output=True, check=True, timeout=60)
            process_ms = (time.perf_counter() - start) * 1000
        probe = json.loads(raw.stdout)
        runs = probe["runs"]
        check(len(runs) == 4, "Expected four conversation repeats")
        actual_runs = [inspect_run(run, selected, probe["ids"], turn_specs) for run in runs]
        check(all(a == actual_runs[0] for a in actual_runs), "Non-reproducible conversational host outputs")

        turns_out = []
        for idx, ((q, exp_v, exp_locs), act, old_t) in enumerate(zip(turn_specs, actual_runs[0], old["turns"], strict=True)):
            exp_spans = gold(selected, exp_locs)
            check(old_t["question"] == q and old_t["expected_verdict"] == exp_v and old_t["expected_citations"] == exp_spans,
                  "Preregistered turn oracle mismatch")
            turns_out.append({
                "turn": idx + 1,
                "question": q,
                "expected_verdict": exp_v,
                "expected_citations": exp_spans,
                "search_verdict": old_t["verdict"],
                "search_outcome": old_t["outcome"],
                "host_verdict": act[0],
                "host_citations": act[1],
                "host_outcome": turn_outcome(act, exp_v, exp_spans),
            })

        results.append({
            "id": cid,
            "provenance": old["provenance"],
            "ids": probe["ids"],
            "search_complete": old["complete"],
            "host_complete": all(t["host_outcome"] in ("exact_delivery", "correct_withhold") for t in turns_out),
            "turns": turns_out,
            "process_ms": process_ms,
            "runs": runs,
        })
    return results


def ledger_record(row):
    observed = all(value["kind"] in ORIGINS for value in row["provenance"].values())
    if not observed:
        return None
    return {key: row[key] for key in ("id", "provenance", "ids", "search_complete", "host_complete", "turns", "process_ms", "runs")}


def validate(ledger):
    expected_keys = {"schema", "baseline", "product", "source_report_sha256", "observations", "eligibility", "external_inference_calls", "labels"}
    check(set(ledger) == expected_keys and ledger["schema"] == 1 and ledger["baseline"] == BASE and
          ledger["eligibility"] == PURPOSE and ledger["external_inference_calls"] == 0 and
          ledger["labels"] == AUTHOR_PROVENANCE, "Unexpected ledger schema or eligibility")
    check(re.fullmatch(r"[0-9a-f]{40}", ledger["product"]) and
          re.fullmatch(r"[0-9a-f]{64}", ledger["source_report_sha256"]), "Missing immutable report provenance")
    files, origins = sources()
    by_id = {cid: (names, turn_specs) for cid, names, turn_specs in CASES
             if all(origins[name]["kind"] in ORIGINS for name in names)}
    rows = ledger["observations"]
    check(isinstance(rows, list) and len(rows) == len(by_id) and set(row["id"] for row in rows) == set(by_id),
          "Missing, duplicate or constructed ledger observations")
    frozen = {row["id"]: row for row in json.loads(FROZEN.read_text(encoding="utf-8"))["trials"]}
    for row in rows:
        names, turn_specs = by_id[row["id"]]
        check(ledger_record(row) == row and set(row["ids"]) == set(names) and
              row["provenance"] == {name: origins[name] for name in names}, "Source or request provenance mismatch")
        for idx, ((q, exp_v, exp_locs), rt, ft) in enumerate(zip(turn_specs, row["turns"], frozen[row["id"]]["turns"], strict=True)):
            expected = gold(files, exp_locs)
            check(rt["question"] == q and rt["expected_citations"] == expected and rt["expected_verdict"] == exp_v and
                  rt["search_outcome"] == ft["outcome"] and rt["search_verdict"] == ft["verdict"],
                  "Ledger oracle or comparator mismatch")
        check(type(row["process_ms"]) in (int, float) and 0 < row["process_ms"] < 60000 and len(row["runs"]) == 4,
              "Invalid process measurements")
        for run in row["runs"]:
            check(type(run["workingSetBytes"]) == int and run["workingSetBytes"] > 0 and len(run["turns"]) == len(turn_specs),
                  "Invalid run resource measurements")
            actual = inspect_run(run, {name: files[name] for name in names}, row["ids"], turn_specs)
            for act, rt, (q, exp_v, exp_locs) in zip(actual, row["turns"], turn_specs, strict=True):
                expected = gold(files, exp_locs)
                check(act == (rt["host_verdict"], rt["host_citations"]) and
                      turn_outcome(act, exp_v, expected) == rt["host_outcome"],
                      "Invalid observed multi-turn outcome")


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("report", type=Path)
    parser.add_argument("ledger", type=Path)
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    if args.verify_only:
        ledger = json.loads(args.ledger.read_bytes())
        validate(ledger)
        report = json.loads(args.report.read_bytes())
        check(ledger["source_report_sha256"] == sha(args.report.read_bytes()) and report["product"] == ledger["product"] and
              ledger["observations"] == [row for row in report["trials"] if ledger_record(row) is not None],
              "Ledger differs from host report")
        print(f"PASS {len(ledger['observations'])} real-source multi-turn outcomes and eligibility")
        return
    rows = collect()
    product = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    flat = {policy: [t[f"{policy}_outcome"] for r in rows for t in r["turns"]] for policy in ("search", "host")}
    counts = {policy: {k: flat[policy].count(k) for k in ("exact_delivery", "wrong_delivery", "false_abstention", "correct_withhold", "verdict_mismatch")}
              for policy in ("search", "host")}
    turn1_times = [r["runs"][0]["turns"][0]["ms"] for r in rows]
    warm_turn_times = [t["ms"] for r in rows for run in r["runs"][1:] for t in run["turns"]]
    report = {
        "schema": 1,
        "baseline": BASE,
        "product": product,
        "external_inference_calls": 0,
        "conversations": {
            "search": f"{sum(r['search_complete'] for r in rows)}/{len(rows)}",
            "host": f"{sum(r['host_complete'] for r in rows)}/{len(rows)}",
        },
        "counts": counts,
        "first_turn_ms": statistics.median(turn1_times),
        "warm_turn_ms": statistics.median(warm_turn_times),
        "process_ms": statistics.median(r["process_ms"] for r in rows),
        "max_sampled_working_set": max(run["workingSetBytes"] for r in rows for run in r["runs"]),
        "trials": rows,
    }
    write(args.report, report)
    ledger = {
        "schema": 1,
        "baseline": BASE,
        "product": product,
        "source_report_sha256": sha(args.report.read_bytes()),
        "labels": AUTHOR_PROVENANCE,
        "eligibility": PURPOSE,
        "external_inference_calls": 0,
        "observations": [row for row in rows if ledger_record(row) is not None],
    }
    validate(ledger)
    write(args.ledger, ledger)
    print(json.dumps({key: value for key, value in report.items() if key != "trials"}, indent=2))


if __name__ == "__main__":
    main()
