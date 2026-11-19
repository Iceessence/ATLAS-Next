"""Original shadow Primary, Research, and Engineering planning decisions."""
import argparse
from itertools import combinations
import json
import os
import time

import numpy as np

from atlas_language import OUTPUT, infer, load_checkpoint
from atlas_planner import (LANGUAGE_SHA, current_project, live_read, live_task, load_planner,
                           ordered, plan, source_features)
from foundry import DATA, FROZEN, candidate_key, inputs, load, load_knowledge
from master_spec import installed
from native_data import ROOT, host_run, sha, write_new
from project_specialist import fit, predict

TEAM = ROOT / "docs/evidence/next-024/masters.json"
PLANNER_SHA = "1834e999a7ab5268b98ffe7db4d6ab18d9327cb5ce584b62deefe4205d9a9f91"
SECTIONS = ("CH-015", "CH-074", "CH-138", "CH-163", "CH-174", "CH-175")
ROLES = ("Primary", "Research", "Engineering")
FAMILIES = {"Research": ".md", "Engineering": ".py"}


def fit_team():
    spec = installed()
    spec.consult(*SECTIONS)
    batch = load("train")
    model, metrics = {}, {}
    for role, suffix in FAMILIES.items():
        values, labels, refs = [], [], []
        for row in batch["rows"]:
            task = row["task"]
            profile = batch["profiles"][task["family"]]
            for i, source in enumerate(profile["sources"]):
                entry = row["outcomes"].get(candidate_key(i))
                if source["suffix"] != suffix or not entry or not entry["record"]["eligible_for_training"]:
                    continue
                values.append(source_features(task["question"], profile, i))
                labels.append(int(entry["record"]["outcome_class"] == "CORRECT_DELIVERY"))
                refs.append(entry["record"]["evaluation_receipt_sha256"])
        if set(labels) != {0, 1}:
            raise ValueError(role + " requires observed positive and negative action outcomes")
        seed = 11 if role == "Research" else 73
        start = time.perf_counter_ns()
        weights = fit(np.array(values), np.array(labels), hidden=6, seed=seed)
        metrics[role] = dict(samples=len(labels), positives=sum(labels), elapsed_ms=(time.perf_counter_ns()-start)/1e6)
        model[role] = dict(architecture=[8,6,1], parameters=61, seed=seed, weights=weights, receipts=refs,
                           capability="rank factual evidence" if role == "Research" else "rank declaration inspection")
    training = json.loads(FROZEN.read_bytes())["partitions"]["train"]["sha256"]
    return dict(schema="atlas.masters_shadow.v1", origin="ATLAS_ORIGINAL_SCRATCH", qualification="SHADOW",
                canonical_master_sha256=spec.sha256, sections_consulted=list(SECTIONS), fit_data_sha256=training,
                primary_sha256=LANGUAGE_SHA, roles=model, external_inference_calls=0), metrics


def load_team(path=TEAM, pin=None):
    data = path.read_bytes()
    team = json.loads(data)
    spec = installed()
    spec.consult(*SECTIONS)
    if (team["schema"], team["qualification"], team["origin"], team["canonical_master_sha256"],
            team["primary_sha256"], team["external_inference_calls"]) != (
            "atlas.masters_shadow.v1", "SHADOW", "ATLAS_ORIGINAL_SCRATCH", spec.sha256,
            LANGUAGE_SHA, 0) or pin and sha(data) != pin:
        raise ValueError("Unexpected Master checkpoint or dependency")
    return team, sha(data)


def route(question, profile, active, language, team, checkpoint, knowledge):
    count = len(profile["sources"])
    scores = [-1.] * count
    messages = []
    mode = None
    card = None
    if "Primary" in active:
        mode, probabilities = infer(language, question)
        card = knowledge["procedures"].get(mode)
        messages.append(dict(from_role="Primary", to_role="host", checkpoint_sha256=LANGUAGE_SHA,
                             proposal=mode, evidence_ids=[], score=max(probabilities), remaining_read_steps=2))
    credible = set()
    for role, suffix in FAMILIES.items():
        if role not in active:
            continue
        indexes = [i for i, source in enumerate(profile["sources"]) if source["suffix"] == suffix]
        if not indexes:
            continue
        prob = predict(np.asarray([source_features(question, profile, i) for i in indexes]), team["roles"][role]["weights"])
        for i, p in zip(indexes, prob, strict=True):
            scores[i] = float(p)
        best = max(indexes, key=lambda i: scores[i])
        messages.append(dict(from_role=role, to_role="host", checkpoint_sha256=checkpoint, proposal="source:"+str(best),
                             evidence_ids=[profile["sources"][best]["source_id"]], score=scores[best], remaining_read_steps=2))
        if scores[best] >= .5:
            credible.add(best)
    disagreement = len(credible) > 1
    if card and (card["effects"] != "none" or card["trust"] != "observed-public"):
        raise ValueError("Master-selected procedure was not validated")
    if disagreement:
        messages.append(dict(from_role="host", to_role="verifier", proposal="REQUEST_REVIEW",
                             evidence_ids=[profile["sources"][i]["source_id"] for i in sorted(credible)],
                             remaining_read_steps=2))
    order = sorted(range(count), key=lambda i: (-scores[i], i))
    if card:
        order.sort(key=lambda i: profile["sources"][i]["suffix"] != card["source_suffix"])
    return order, card, messages


def summary(rows):
    return dict(tasks=len(rows), verified=sum(r["useful"] for r in rows),
                wrong=sum(r["wrong"] for r in rows),
                completed=sum(r["state"]=="COMPLETED_SHADOW" and r["useful"] for r in rows),
                reads=sum(s["action"]=="READ" for r in rows for s in r["steps"]),
                host_process_ms=sum(r["process_ms"] for r in rows),
                role_proposals=sum(r["model_calls"] for r in rows),
                p95_policy_us=float(np.percentile([r["scoring_us"] for r in rows],95)))


def evaluate(language, planner, team, checkpoint):
    data, library = load("evaluation"), load_knowledge()
    costs = json.loads((DATA / "evaluation-cost.json").read_bytes())
    arms = {"host_raw": (), "fixed": (), "lexical": (), "incumbent": ()}
    for n in (1,2,3):
        for active in combinations(ROLES,n):
            arms["+".join(active)] = active
    reports = {}
    for name, active in arms.items():
        trials = []
        for row in data["rows"]:
            task = row["task"]
            profile = data["profiles"][task["family"]]
            start = time.perf_counter_ns()
            expected = task["oracle"]["state"] == "completed"
            if name == "host_raw":
                measured = row["outcomes"]["raw"]["receipt"]["observation"]
                passed = measured["useful"]
                trace = dict(state="COMPLETED_SHADOW" if measured["state"] == "completed" else "ABSTAINED",
                             steps=[], evidence=None)
                elapsed = costs[task["id"]]["raw"]["process_ms"]
                order, messages = [], []
            else:
                if active:
                    order, card, messages = route(task["question"], profile, active, language, team, checkpoint, library)
                else:
                    order = ordered(task["question"], profile, planner, "learned" if name == "incumbent" else name)
                    card, messages = None, []
                def read(i, _):
                    action = candidate_key(i)
                    return row["outcomes"][action]["receipt"]["observation"], costs[task["id"]][action]
                trace = plan(task["question"], profile, order, read, card=card)
                passed = trace["state"] == ("COMPLETED_SHADOW" if expected else "ABSTAINED")
                elapsed = sum(s.get("cost", {}).get("process_ms",0.) for s in trace["steps"])
            chosen = next((s["source_id"] for s in trace["steps"] if s["action"] == "READ"), None)
            trials.append(dict(id=task["id"], use_case=task["kind"], family=task["family"], useful=passed,
                               wrong=trace["state"]=="COMPLETED_SHADOW" and not passed, scoring_us=(time.perf_counter_ns()-start)/1000,
                               process_ms=elapsed, order=order, first_read=chosen, messages=messages,
                               model_calls=sum(m["from_role"] in ROLES for m in messages), **trace))
        reports[name] = dict(summary=summary(trials), trials=trials)
    deterministic = {row["id"]: row for row in reports["lexical"]["trials"]}
    for output in reports.values():
        diffs = [(row, deterministic[row["id"]]) for row in output["trials"]]
        output["summary"]["changed_first_actions"] = sum(a["first_read"] != b["first_read"] for a,b in diffs)
        output["summary"]["paired_net_gain_vs_lexical"] = sum(int(a["useful"])-int(b["useful"]) for a,b in diffs)
    return reports


def live_team(profile, group, question, language, team, digest, *, max_steps=5, deadline=None):
    task = live_task(question, profile, group)
    raw, elapsed = host_run(inputs(task, profile, question), None, timeout=60 if deadline is None else max(.001, deadline-time.monotonic()))
    ranked, card, messages = route(question, profile, ROLES, language, team, digest, load_knowledge())
    remaining = 60 if deadline is None else deadline-time.monotonic()
    if remaining <= 0:
        result = dict(state="ABSTAINED", evidence=None, steps=[], reason="shared wall budget reached")
    else:
        result = plan(question, profile, ranked, lambda i, seconds: live_read(task, profile, i, seconds),
                      card=card, max_steps=max_steps, wall_seconds=remaining)
    return dict(schema="atlas.shadow_task_trace.v1", canonical_master_sha256=team["canonical_master_sha256"],
                sections_consulted=list(SECTIONS), request=question, request_sha256=sha(question.encode()),
                scope=profile["family"], model_origin=team["origin"], qualification="SHADOW",
                team_sha256=digest, primary_sha256=LANGUAGE_SHA, messages=messages, shadow=result,
                authoritative_host=dict(state=raw["answer"]["state"], relevance=raw["answer"]["answerRelevance"],
                                        process_ms=elapsed), external_inference_calls=0)


def combined(profile, group, fact, definition, language, team, digest):
    from atlas_language import typed_baseline
    profiles = [typed_baseline(q, profile) for q in (fact, definition)]
    if not (profiles[0] in ("source:0", "source:1", "source:2") and profiles[1] == "source:3"):
        raise ValueError("Supply one explicit audited fact and one explicit declaration question")
    priorities = [(q, max(infer(language, q)[1])) for q in (fact, definition)]
    trace, remaining, deadline = [], 8, time.monotonic()+60
    for question, confidence in sorted(priorities, key=lambda item: -item[1]):
        if remaining < 3 or time.monotonic() >= deadline:
            break
        # Reserve three steps for a second independent goal before spending retries.
        budget = min(5, remaining - 3 if not trace else remaining)
        receipt = live_team(profile, group, question, language, team, digest, max_steps=budget, deadline=deadline)
        remaining -= len(receipt["shadow"]["steps"])
        trace.append(dict(prior=trace[-1]["receipt"]["request_sha256"] if trace else None,
                          primary_scheduling_score=confidence, receipt=receipt))
    complete = len(trace)==2 and all(node["receipt"]["shadow"]["state"]=="COMPLETED_SHADOW" for node in trace)
    return dict(schema="atlas.shadow_compound_trace.v1", scope=profile["family"], steps_used=8-remaining,
                qualification="SHADOW", state="COMPLETED_SHADOW" if complete else "ABSTAINED", goals=trace,
                external_inference_calls=0)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("fit", "evaluate", "query"))
    parser.add_argument("--project")
    parser.add_argument("--question")
    parser.add_argument("--also-definition")
    args = parser.parse_args()
    if os.environ.get("OPENBLAS_NUM_THREADS") != "1":
        parser.error("Set OPENBLAS_NUM_THREADS=1")
    if args.command == "fit":
        team, metrics = fit_team()
        write_new(OUTPUT / "masters.json", team)
        write_new(OUTPUT / "masters-fit.json", metrics)
        print(metrics)
        return
    language, _ = load_checkpoint(pin=LANGUAGE_SHA)
    team, digest = load_team(OUTPUT / "masters.json" if args.command == "evaluate" else TEAM)
    if args.command == "evaluate":
        planner = load_planner(pin=PLANNER_SHA)
        reports = evaluate(language, planner, team, digest)
        write_new(OUTPUT / "masters-evaluation.json", dict(checkpoint_sha256=digest, results=reports))
        print({name: value["summary"] for name,value in reports.items()})
    else:
        if not args.project or not args.question or len(args.question) > 2048:
            parser.error("Select an enrolled public project and a bounded question")
        profile, group = current_project(args.project)
        if args.also_definition:
            if len(args.also_definition) > 2048:
                parser.error("Use one bounded declaration question")
            result = combined(profile, group, args.question, args.also_definition, language, team, digest)
        else:
            result = live_team(profile, group, args.question, language, team, digest)
        print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
