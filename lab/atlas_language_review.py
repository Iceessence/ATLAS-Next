"""External custodian evaluation for the frozen original text-model experiment."""
import argparse
from collections import Counter
from pathlib import Path
import re

from atlas_language import CHECKPOINT, load_checkpoint, policy_action, previous_router, tfidf_router
from atlas_planner import LANGUAGE_SHA
from atlas_refit import CONTROL_POLICIES, USEFUL
from foundry import FAMILIES, action_inputs, classify, inputs, load
from native_data import observe, sha, write_new
from native_review import protected_paths, validate_custody
from outcomes import load_suite


def profile_for(case):
    keys, docs = case.get("keys"), case["documents"]
    suffixes = case.get("extensions")
    if suffixes is None:
        suffixes = [".md"]*len(docs)
    if not isinstance(keys, list) or len(keys) != len(docs):
        raise ValueError("A protected source must have matching explicit keys")
    profile = {"sources":[]}
    for key, suffix, doc in zip(keys, suffixes, docs, strict=True):
        pattern = r"[\w -]{1,96}" if suffix == ".md" else r"[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)?"
        if not isinstance(key, str) or not re.fullmatch(pattern, key) or len(doc.encode()) > 256*1024:
            raise ValueError("Unexpected protected source key or byte budget")
        profile["sources"].append(dict(key=key, suffix=suffix, text=doc))
    return profile


def public_cases():
    result = []
    for part in FAMILIES:
        batch = load(part)
        for row in batch["rows"]:
            task = row["task"]
            project = batch["profiles"][task["family"]]
            result.append(dict(id=task["id"], family=task["family"], question=task["question"],
                               documents=[s["text"] for s in project["sources"]]))
    return result


def score_cases(cases, original, candidate, scratch):
    reports = {role: {policy: [] for policy in (*CONTROL_POLICIES, "native")} for role in ("incumbent", "candidate")}
    routers = {role: (model, tfidf_router(model), previous_router()) for role, model in
               (("incumbent", original), ("candidate", candidate))}
    for case in cases:
        profile, picks = profile_for(case), {}
        question = case["question"]
        task = dict(id=case["id"], question=question, oracle=case["expect"])
        for role, (model, tfidf, prior) in routers.items():
            picks[role] = {}
            for policy in reports[role]:
                # No expectations are supplied to any policy.
                picks[role][policy], _, _ = policy_action(policy, question, profile, model, tfidf, prior)
        trials = {}
        for action in sorted({choice for models in picks.values() for choice in models.values()}):
            formatted, restriction = action_inputs(task, profile, action)
            obs, costs = observe(inputs(task, profile, formatted), restriction, scratch=scratch)
            outcome, reason = classify(task, action, obs)
            trials[action] = dict(outcome=outcome, reason=reason, costs=costs)
        for role, decisions in picks.items():
            for policy, action in decisions.items():
                reports[role][policy].append(dict(id=case["id"], action=action, **trials[action]))
    return {role:{policy:dict(rows=rows, useful=sum(r["outcome"] in USEFUL for r in rows),
                             classes=dict(Counter(r["outcome"] for r in rows)))
                  for policy,rows in policies.items()} for role, policies in reports.items()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--candidate", type=Path, required=True)
    parser.add_argument("--candidate-sha", required=True)
    args = parser.parse_args()
    manifest, report = args.manifest.resolve(), args.report.resolve()
    protected_paths(manifest, report)
    cases, suite_sha = load_suite(manifest)
    validate_custody(cases, public_cases(), allowed_extensions=(".md",".py"))
    original, prior_sha = load_checkpoint(CHECKPOINT, LANGUAGE_SHA)
    candidate, candidate_sha = load_checkpoint(args.candidate, args.candidate_sha.lower())
    result = score_cases(cases, original, candidate, manifest.parent)
    write_new(report, dict(scope="external evaluator", suite_sha256=suite_sha, prior_sha256=prior_sha,
                           candidate_sha256=candidate_sha, results=result, promotion="separate independent verdict"))
    print(f"Scored {len(cases)} external cases. Consult the evaluator-owned report.")


if __name__ == "__main__":
    main()
