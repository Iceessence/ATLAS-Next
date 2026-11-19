"""Run one preregistered original text-model refit and retain the shadow verdict."""
import json
import os

from atlas_language import CHECKPOINT, OUTPUT, evaluate, load_checkpoint, train
from atlas_planner import LANGUAGE_SHA
from foundry import FROZEN
from master_spec import installed
from native_data import sha, write_new

SECTIONS = ("CH-015", "CH-075", "CH-140", "CH-162", "CH-163", "CH-181", "CH-184")
USEFUL = {"CORRECT_DELIVERY", "CORRECT_WITHHOLD"}
CONTROL_POLICIES = ("host_raw", "exact_typed", "tfidf", "linear", "previous_original")


def paired(old, new):
    original = {row["id"]: row for row in old["rows"]}
    if len(original) != len(new["rows"]) or set(original) != {row["id"] for row in new["rows"]}:
        raise ValueError("Model comparison requires the same frozen task identities")
    regressions, gains, new_wrong, action_changes = [], [], [], 0
    for proposed in new["rows"]:
        before = original[proposed["id"]]
        a, b = before["outcome"], proposed["outcome"]
        action_changes += proposed["action"] != before["action"]
        if a in USEFUL and b not in USEFUL:
            regressions.append(proposed["id"])
        if a not in USEFUL and b in USEFUL:
            gains.append(proposed["id"])
        if a != "INCORRECT_DELIVERY" and b == "INCORRECT_DELIVERY":
            new_wrong.append(proposed["id"])
    return dict(gains=gains, regressions=regressions, new_wrong=new_wrong, changed_actions=action_changes)


def calibration(rows):
    errors = []
    labels = ("read_fact", "locate_definition", "withhold")
    for row in rows:
        if row["scores"] is None:
            continue
        expected = labels.index(row["expected_intent"])
        errors.append(sum((p-int(i==expected))**2 for i,p in enumerate(row["scores"])))
    return sum(errors)/len(errors)


def decide(before, after):
    gate = {}
    for role in ("validation", "evaluation"):
        baseline = before[role]["results"]
        proposal = after[role]["results"]
        gate[role] = paired(baseline["native"], proposal["native"])
        gate[role]["incumbent_brier"] = calibration(baseline["native"]["rows"])
        gate[role]["candidate_brier"] = calibration(proposal["native"]["rows"])
        gate[role]["incumbent_useful"] = baseline["native"]["summary"]["useful"]
        gate[role]["candidate_useful"] = proposal["native"]["summary"]["useful"]
    strongest = max(before["evaluation"]["results"][name]["summary"]["useful"] for name in CONTROL_POLICIES)
    messages = []
    if any(result["new_wrong"] for result in gate.values()):
        messages.append("new incorrect shadow proposal")
    if any(result["regressions"] for result in gate.values()):
        messages.append("lost verified incumbent outcome")
    if gate["evaluation"]["candidate_useful"] <= strongest:
        messages.append("no verified unfamiliar-task gain over strongest control")
    return dict(disposition="rejected" if messages else "requires independent evaluation", reasons=messages,
                strongest_public_control_useful=strongest, matched=gate,
                protected_evaluation="pending external custodian", promotion="blocked")


def main():
    if os.environ.get("OPENBLAS_NUM_THREADS") != "1":
        raise ValueError("Set OPENBLAS_NUM_THREADS=1")
    spec = installed()
    spec.consult(*SECTIONS)
    incumbent, digest = load_checkpoint(CHECKPOINT, LANGUAGE_SHA)
    diagnosis = evaluate(incumbent, "validation")
    diag = diagnosis["results"]["native"]["summary"]
    control = diagnosis["results"]["exact_typed"]["summary"]
    if diag["useful"] > control["useful"]:
        raise ValueError("The preregistered weakness trigger was not observed")
    manifest = json.loads(FROZEN.read_bytes())
    pool = manifest["partitions"]["acquisition"]
    if not pool["classes"].get("CORRECT_DELIVERY",0) or not pool["classes"].get("FALSE_ABSTENTION",0):
        raise ValueError("Refit blocked without measured new positives and negatives")
    candidate, metrics = train(("train", "acquisition"))
    path = OUTPUT / "refit-025.json"
    write_new(path, candidate)
    candidate, candidate_pin = load_checkpoint(path)
    before = dict(validation=diagnosis, evaluation=evaluate(incumbent, "evaluation"))
    after = {role: evaluate(candidate, role) for role in ("validation", "evaluation")}
    decision = decide(before, after)
    if sha(CHECKPOINT.read_bytes()) != digest:
        raise ValueError("Immutable incumbent differs after trial")
    report = dict(schema="atlas.original_refit.v1", qualification="SHADOW",
                  sections_consulted=list(SECTIONS), canonical_master_sha256=spec.sha256,
                  incumbent_sha256=digest, candidate_sha256=candidate_pin, external_inference_calls=0,
                  diagnosis=dict(native_useful=diag["useful"], exact_typed_useful=control["useful"],
                                 missed=[r["id"] for r in diagnosis["results"]["native"]["rows"]
                                         if r["outcome"] == "FALSE_ABSTENTION"]),
                  acquired=pool, hypothesis="New verified local project observations may reduce missed intents.",
                  fit=metrics, before=before, after=after, decision=decision,
                  rollback="incumbent checkpoint hash verified; candidate never entered active policy")
    write_new(OUTPUT / "refit-025-evaluation.json", report)
    print(decision)


if __name__ == "__main__":
    main()
