"""Run original shadow decisions with independently authorized host delivery."""
import argparse
import json
from pathlib import Path
import time

from native_data import host_run, load_partition, write_new
from native_master import MASTER, gate_action, specialist_signal
from native_models import (OUTPUT, SPECIALIST, collect, costs_for, load_model, predict,
                           specialist_action)
from outcomes import sha

SPECIALIST_SHA = "2e67c6562dc76e35ed6c7a11af6491980d1d015296b9b0b0673ec98d73ff72d1"
MASTER_SHA = "c1199fd6a641621dd6d5beaf0baf25b67e8e2923e68cf63d176fbe8037b890ba"
MAX_BYTES = 256 * 1024


def frozen_models():
    specialist, digest = load_model(SPECIALIST, "source specialist", SPECIALIST_SHA)
    master, _ = load_model(MASTER, "Primary Master", MASTER_SHA)
    if master["specialist_sha256"] != digest:
        raise ValueError("Master checkpoint belongs to a different specialist")
    return specialist, master


def read_sources(paths):
    if not 1 <= len(paths) <= 32:
        raise ValueError("Supply one to 32 authorized Markdown sources")
    documents, supplied, hashes = [], [], set()
    for path in paths:
        if path.suffix.casefold() != ".md" or path.is_symlink() or not path.is_file():
            raise ValueError(f"Expected a local Markdown file: {path}")
        with path.open("rb") as stream:
            data = stream.read(MAX_BYTES + 1)
        if not data or len(data) > MAX_BYTES or b"\0" in data:
            raise ValueError(f"Unsupported source size or content: {path}")
        value = data.decode("utf-8")
        digest = sha(data)
        if digest in hashes:
            raise ValueError("Duplicate source bytes")
        hashes.add(digest)
        documents.append(value)
        supplied.append(dict(path=str(path.resolve()), sha256=digest))
    return documents, supplied


def host_receipt(run, process_ms, supplied, documents):
    answer = run["answer"]
    research = (answer.get("researchReview") or {}).get("verdict")
    result = dict(state=answer["state"], research_verdict=research, relevance=answer["answerRelevance"],
                  source_integrity=answer["sourceIntegrity"], engine_us=run["elapsedUs"],
                  process_ms=process_ms, working_set_bytes=run["workingSetBytes"])
    if answer["state"] != "completed":
        return dict(result, detail=answer["result"])
    cite = answer.get("evidence") or {}
    ids = run["ids"]
    if cite.get("id") not in ids or answer["scope"] != "owner-local":
        return dict(result, state="outside supplied sources", host_state=answer["state"])
    idx = ids.index(cite["id"])
    span = cite["span"]
    start, length = span["start"], span["length"]
    excerpt = documents[idx].encode("utf-16-le")[2*start:2*(start+length)].decode("utf-16-le")
    if (start < 0 or length <= 0 or excerpt != answer["result"] or
            cite["digest"] != supplied[idx]["sha256"] or cite["version"] != 1 or
            answer["authorization"] != "allowed scoped record read" or
            research != "supported" or answer["answerRelevance"] != "supported" or
            answer["sourceIntegrity"] != "passed"):
        raise ValueError("Completion differs from the authorized source snapshot")
    return dict(result, answer=excerpt, evidence=dict(supplied[idx], line=span["line"], start=start, length=length))


def describe_model(bundle, digest):
    model = bundle["policies"]["native"]
    return dict(role=bundle["role"], checkpoint_sha256=digest, architecture=model["architecture"],
                layers=[model["inputs"], model["hidden"], 1], parameters=model["parameters"],
                training_partitions=bundle["dataset_pins"], seed=model["seed"],
                origin=bundle["origin"], qualification=bundle["qualification"])


def live_task(question, paths):
    if not question.strip() or len(question.strip().encode("utf-16-le")) // 2 > 2048:
        raise ValueError("Supply a request of 1–2048 characters")
    documents, supplied = read_sources(paths)
    specialist, master = frozen_models()
    case = dict(id="owner-read-only", question=question, documents=documents)
    started = time.perf_counter_ns()
    top, signal = specialist_signal(case, specialist["idf"], specialist["policies"]["native"])
    gate = master["policies"]["native"]
    confidence = float(predict([signal], gate)[0])
    action = top if confidence >= gate["threshold"] else None
    scoring_us = (time.perf_counter_ns()-started)/1000

    primary, elapsed = host_run(case, None)
    authoritative = host_receipt(primary, elapsed, supplied, documents)
    shadow = authoritative
    if action is not None:
        observation, cost = host_run(case, action)
        shadow = host_receipt(observation, cost, supplied, documents)
    return dict(mode="native-original shadow", external_inference_calls=0, request=question.strip(),
                sources=supplied, models=[describe_model(specialist, SPECIALIST_SHA), describe_model(master, MASTER_SHA)],
                learned=dict(specialist_proposal=supplied[top], specialist_score=signal[0],
                             master_score=confidence, master_threshold=gate["threshold"],
                             workflow="focused source proposal" if action is not None else "unrestricted host",
                             scoring_us=scoring_us),
                deterministic_host=authoritative, shadow_observation=shadow)


def benchmark():
    specialist, master = frozen_models()
    rows, replay, pin = load_partition("evaluation")
    idf = specialist["idf"]
    policies = {
        "host": lambda row: (None, None),
        "tfidf": lambda row: specialist_action("tfidf", row, idf),
        "linear": lambda row: specialist_action("linear", row, idf, specialist["policies"]["linear"]),
        "original_specialist": lambda row: specialist_action("native", row, idf, specialist["policies"]["native"]),
        "confidence_gate": lambda row: gate_action(row, specialist, confidence=specialist["policies"]["native"]["threshold"]),
        "linear_gate": lambda row: gate_action(row, specialist, master["policies"]["linear"]),
        "original_cooperation": lambda row: gate_action(row, specialist, master["policies"]["native"]),
    }
    costs = costs_for("evaluation")
    results = {key: collect(rows, choose, replay, costs) for key, choose in policies.items()}
    return dict(scope="author-public measured action replay", external_inference_calls=0, dataset_sha256=pin,
                specialist_sha256=SPECIALIST_SHA, master_sha256=MASTER_SHA, results=results)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    query = commands.add_parser("query", help="Run a read-only task against the supplied Markdown files")
    query.add_argument("--question", required=True)
    query.add_argument("--source", action="append", type=Path, required=True)
    query.add_argument("--report", type=Path)
    commands.add_parser("benchmark")
    args = parser.parse_args()
    if args.command == "benchmark":
        report = benchmark()
        write_new(OUTPUT / "cooperation.json", report)
        print({name: value["summary"]["useful"] for name, value in report["results"].items()})
    else:
        report = live_task(args.question, args.source)
        if args.report:
            write_new(args.report, report)
        print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
