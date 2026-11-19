"""Bounded shadow planning over pinned public facts and declaration procedures."""
import argparse
import json
import os
import subprocess
import time

import numpy as np

from atlas_language import OUTPUT, disallowed, infer, load_checkpoint, referenced, typed_baseline
from foundry import DATA, FROZEN, GRANT, action_inputs, candidate_key, inputs, load, load_knowledge
from foundry_sources import FAMILIES, project
from master_spec import installed
from native_data import ROOT, host_run, observe, sha, write_new
from project_specialist import features, fit, predict

PLAN_PATH = ROOT / "docs/evidence/next-023/planner.json"
LANGUAGE_SHA = "d8bfcc543cf7b2d803d3047277fa95956e589956d9a042a5c5e0b7769981111e"
SECTIONS = ("CH-015", "CH-073", "CH-136", "CH-163", "CH-182", "CH-183")


def source_features(question, profile, i):
    source = profile["sources"][i]
    return [*features(question, source["key"]+"\n"+source["text"], {}),
            float(i in referenced(question, profile)), float(source["suffix"] == ".py")]


def fit_planner():
    phase = installed()
    phase.consult(*SECTIONS)
    batch = load("train")
    training, labels, receipts = [], [], []
    for row in batch["rows"]:
        task = row["task"]
        profile = batch["profiles"][task["family"]]
        for i in range(len(profile["sources"])):
            entry = row["outcomes"].get(candidate_key(i))
            if not entry or not entry["record"]["eligible_for_training"]:
                continue
            training.append(source_features(task["question"], profile, i))
            labels.append(int(entry["record"]["outcome_class"] == "CORRECT_DELIVERY"))
            receipts.append(entry["record"]["evaluation_receipt_sha256"])
    if set(labels) != {0, 1}:
        raise ValueError("Planner needs verified positive and negative source actions")
    started = time.perf_counter_ns()
    weights = fit(np.asarray(training), np.asarray(labels), 6, 37)
    fit_ms = (time.perf_counter_ns()-started)/1e6
    model = dict(schema="atlas.planner_shadow.v1", origin="ATLAS_ORIGINAL_SCRATCH", qualification="SHADOW",
                 architecture=[8,6,1], parameters=61, seed=37, weights=weights,
                 canonical_master_sha256=phase.sha256, sections_consulted=list(SECTIONS),
                 training_sha256=json.loads(FROZEN.read_bytes())["partitions"]["train"]["sha256"],
                 observed_actions=len(labels), verified_positives=sum(labels), training_receipts=receipts,
                 external_inference_calls=0)
    return model, dict(fit_ms=fit_ms, samples=len(labels), positives=sum(labels), parameter_bytes=61*8)


def load_planner(path=PLAN_PATH, pin=None):
    data = path.read_bytes()
    model = json.loads(data)
    phase = installed()
    phase.consult(*SECTIONS)
    if (model["schema"], model["qualification"], model["origin"], model["canonical_master_sha256"]) != (
            "atlas.planner_shadow.v1", "SHADOW", "ATLAS_ORIGINAL_SCRATCH", phase.sha256) or pin and sha(data) != pin:
        raise ValueError("Unexpected planning checkpoint")
    return model


def ordered(question, profile, model, policy):
    count = len(profile["sources"])
    if policy == "fixed":
        return list(range(count))
    if policy == "lexical":
        exact = referenced(question, profile)
        return exact + [i for i in range(count) if i not in exact]
    x = np.asarray([source_features(question, profile, i) for i in range(count)])
    p = predict(x, model["weights"])
    return sorted(range(count), key=lambda i: (-p[i], i))


def rank_policy(question, profile, model, policy, language, knowledge):
    ranked = ordered(question, profile, model, policy)
    card = None
    if policy == "procedure":
        mode, _ = infer(language, question)
        card = knowledge["procedures"].get(mode)
        if card is not None:
            if card["effects"] != "none" or card["trust"] != "observed-public":
                raise ValueError("Procedure trust check failed")
            # Procedure hints prioritize candidates. Independent checks decide
            # success, and alternate scoped sources remain available for recovery.
            ranked.sort(key=lambda i: profile["sources"][i]["suffix"] != card["source_suffix"])
    return ranked, card


def plan(question, profile, candidates, read, *, card=None, max_steps=5, wall_seconds=60, cancelled=lambda: False):
    if not 2 <= max_steps <= 5 or not 0 < wall_seconds <= 60:
        raise ValueError("Planning budget is outside supported bounds")
    if (profile["consent_grant_id"] != GRANT or any(
            sha(source["text"].encode()) != source["sha256"] for source in profile["sources"])):
        return dict(state="QUARANTINED", reason="source or scope mismatch", steps=[], external_inference_calls=0)
    deadline = time.monotonic()+wall_seconds
    steps = []
    state, reason, evidence = "ABSTAINED", "evidence unavailable within budget", None

    def add(kind, **payload):
        steps.append(dict(step=len(steps)+1, depends_on=[len(steps)] if steps else [], action=kind, **payload))

    add("SELECT_PROCEDURE" if card else "SELECT_READ_PATH", procedure=card["id"] if card else None)
    if cancelled():
        state, reason = "CANCELED", "owner stop"
    elif disallowed(question) or len(referenced(question, profile)) != 1:
        reason = "unsupported or ambiguous owner request"
    else:
        visited = set()
        for i in candidates:
            if type(i) is not int or not 0 <= i < len(profile["sources"]) or i in visited:
                state, reason = "QUARANTINED", "proposal outside scoped action menu"
                break
            visited.add(i)
            remaining = deadline-time.monotonic()
            if cancelled():
                state, reason = "CANCELED", "owner stop"
                break
            if len(steps)+2 > max_steps or remaining <= 0:
                reason = "step or wall budget reached"
                break
            src = profile["sources"][i]
            add("READ", source_id=src["source_id"], source_revision=src["source_revision"], sha256=src["sha256"])
            try:
                seen, cost = read(i, remaining)
            except (TimeoutError, subprocess.TimeoutExpired):
                add("ASSERT_EVIDENCE", verified=False, outcome="timeout")
                reason = "wall budget reached"
                break
            except (ValueError, OSError, RuntimeError):
                add("ASSERT_EVIDENCE", verified=False, outcome="read failed")
                continue
            if cancelled() or time.monotonic() > deadline:
                state, reason = ("CANCELED", "owner stop") if cancelled() else ("ABSTAINED", "wall budget reached")
                break
            ok = seen["useful"] and seen["state"] == "completed" and seen["selected"] == i and seen["digest"] == src["sha256"]
            add("ASSERT_EVIDENCE", verified=ok, outcome=seen["state"], cost=cost)
            if ok:
                evidence = dict(source_id=src["source_id"], sha256=src["sha256"],
                                span=seen["citation"], extracted=seen["result"])
                state, reason = "COMPLETED_SHADOW", "exact scoped evidence verified"
                break
    return dict(state=state, reason=reason, steps=steps, evidence=evidence, scope=profile["family"],
                external_inference_calls=0, authority="read-only shadow")


def evaluate(model, language, role="evaluation"):
    knowledge = load_knowledge()
    batch = load(role)
    costs = json.loads((DATA / (role+"-cost.json")).read_bytes())
    reports = {}
    for name in ("fixed", "lexical", "learned", "procedure"):
        trials = []
        for row in batch["rows"]:
            task = row["task"]
            profile = batch["profiles"][task["family"]]
            begin = time.perf_counter_ns()
            order, card = rank_policy(task["question"], profile, model, name, language, knowledge)
            def read(i, _):
                action = candidate_key(i)
                return row["outcomes"][action]["receipt"]["observation"], costs[task["id"]][action]
            result = plan(task["question"], profile, order, read, card=card)
            correct = result["state"] == "COMPLETED_SHADOW" if task["oracle"]["state"] == "completed" else (
                result["state"] == "ABSTAINED")
            trials.append(dict(id=task["id"], useful=correct, decisions_us=(time.perf_counter_ns()-begin)/1000,
                               order=order, **result))
        reports[name] = dict(trials=trials, summary=dict(
            useful=sum(r["useful"] for r in trials), total=len(trials),
            completed=sum(r["state"]=="COMPLETED_SHADOW" and r["useful"] for r in trials),
            wrong=sum(r["state"]=="COMPLETED_SHADOW" and not r["useful"] for r in trials),
            false_abstentions=sum(r["state"]!="COMPLETED_SHADOW" and not r["useful"] for r in trials),
            recovered=sum(r["state"]=="COMPLETED_SHADOW" and len(r["steps"])==5 for r in trials),
            reads=sum(s["action"]=="READ" for r in trials for s in r["steps"]),
            measured_host_ms=sum(s.get("cost",{}).get("process_ms",0.) for r in trials for s in r["steps"]),
            p95_policy_us=float(np.percentile([r["decisions_us"] for r in trials],95))))
    return reports


def current_project(family):
    roles = [role for role, names in FAMILIES.items() if family in names]
    if len(roles) != 1:
        raise ValueError("Select an enrolled public project")
    role = roles[0]
    batch = load(role)
    frozen = batch["profiles"][family]
    current, group = project(role, family)
    if current != frozen:
        raise ValueError("Recorded public project no longer matches installed evidence")
    return current, group


def live(family, question, language, model, policy="procedure"):
    profile, group = current_project(family)
    target = typed_baseline(question, profile)
    oracle = dict(state="needs clarification", verdict="unsupported")
    if target.startswith("source:"):
        oracle = group[int(target.split(":")[1])]["oracle"]
    task = dict(id="live-"+sha(question.encode())[:16], question=question, oracle=oracle)
    authority, cost = host_run(inputs(task, profile, question), None)
    order, card = rank_policy(question, profile, model, policy, language, load_knowledge())
    def read(i, remaining):
        formatted, restriction = action_inputs(task, profile, candidate_key(i))
        return observe(inputs(task, profile, formatted), restriction, timeout=remaining)
    trace = plan(question, profile, order, read, card=card)
    receipt = authority["answer"]
    return dict(canonical_master_sha256=model["canonical_master_sha256"], sections_consulted=list(SECTIONS),
                request_sha256=sha(question.encode()), request=question, project=family,
                original_host=dict(state=receipt["state"], relevance=receipt["answerRelevance"], process_ms=cost),
                shadow=trace, external_inference_calls=0)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("fit", "evaluate", "query"))
    parser.add_argument("--project")
    parser.add_argument("--question")
    parser.add_argument("--policy", choices=("fixed", "lexical", "learned", "procedure"), default="procedure")
    args = parser.parse_args()
    if os.environ.get("OPENBLAS_NUM_THREADS") != "1":
        parser.error("Set OPENBLAS_NUM_THREADS=1")
    if args.command == "fit":
        planner, metrics = fit_planner()
        write_new(OUTPUT / "planner.json", planner)
        write_new(OUTPUT / "planner-fit.json", metrics)
        print(metrics)
    else:
        language, _ = load_checkpoint(pin=LANGUAGE_SHA)
        planner = load_planner(OUTPUT / "planner.json" if args.command == "evaluate" else PLAN_PATH)
        if args.command == "evaluate":
            results = evaluate(planner, language)
            write_new(OUTPUT / "planner-evaluation.json", results)
            print({k:v["summary"] for k,v in results.items()})
        else:
            if not args.project or not args.question or len(args.question) > 2048:
                parser.error("Select an enrolled project and a single bounded request")
            print(json.dumps(live(args.project, args.question, language, planner, args.policy), indent=2))


if __name__ == "__main__":
    main()
