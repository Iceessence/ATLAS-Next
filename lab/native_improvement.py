"""Attempt a bounded original-only refit, with no qualification authority."""
import argparse
import json
import os

from native_data import DATASET, write_new
from native_models import OUTPUT, SPECIALIST, evaluate_specialist, fit_specialist, load_model
from native_task import SPECIALIST_SHA
from outcomes import sha


def compare(previous, proposal):
    old, new = previous["native"], proposal["native"]
    paired = {trial["id"]: trial for trial in old["trials"]}
    if len(paired) != len(new["trials"]) or set(paired) != {trial["id"] for trial in new["trials"]}:
        raise ValueError("Comparison requires matched task identities")
    changed = dict(gains=[], regressions=[], new_wrong=[])
    for trial in new["trials"]:
        prior = paired[trial["id"]]
        if trial["useful"] and not prior["useful"]:
            changed["gains"].append(trial["id"])
        if prior["useful"] and not trial["useful"]:
            changed["regressions"].append(trial["id"])
        if trial["state"] == "completed" and not trial["useful"] and (prior["state"] != "completed" or prior["useful"]):
            changed["new_wrong"].append(trial["id"])
    comparators = {f"previous_{key}": value["summary"] for key, value in previous.items()}
    comparators["refit_linear"] = proposal["linear"]["summary"]
    strongest = max((summary["useful"] for summary in comparators.values() if summary["wrong"] == 0), default=0)
    useful = new["summary"]["useful"]
    reasons = []
    if changed["regressions"]:
        reasons.append("lost previously verified completions")
    if changed["new_wrong"] or new["summary"]["wrong"]:
        reasons.append("incorrect delivery")
    if useful <= strongest:
        reasons.append("no verified completion gain over strongest baseline")
    return dict(disposition="rejected" if reasons else "requires independent evaluation", reasons=reasons,
                strongest_baseline_useful=strongest, candidate_useful=useful, **changed)


def trial(device):
    previous, digest = load_model(SPECIALIST, "source specialist", SPECIALIST_SHA)
    diagnostic = evaluate_specialist(previous, "validation")
    metrics = diagnostic["results"]
    native, host = metrics["native"]["summary"], metrics["host"]["summary"]
    report = dict(mode="native-original shadow", external_inference_calls=0,
                  public_validation=diagnostic, previous_sha256=digest,
                  protected_evaluation="not run; evaluator custody required",
                  promotion="blocked")
    if native["useful"] > host["useful"] and native["useful"] == native["tasks"] and native["wrong"] == 0:
        write_new(OUTPUT / "improvement.json", dict(report, fit_status="no preregistered weakness measured"))
        return
    manifest = json.loads((DATASET / "manifest.json").read_bytes())
    enrollment = manifest["splits"]["acquisition"]
    candidate, selection = fit_specialist(("train", "acquisition"), device)
    candidate_path = OUTPUT / "refit-candidate.json"
    write_new(candidate_path, candidate)
    frozen, candidate_sha = load_model(candidate_path, "source specialist")
    before, after = evaluate_specialist(previous), evaluate_specialist(frozen)
    decision = compare(before["results"], after["results"])
    if sha(SPECIALIST.read_bytes()) != digest:
        raise ValueError("Frozen specialist changed during the trial")
    report.update(fit_status="completed",
                  training_plan=dict(diagnostic="native validation did not beat unrestricted host",
                                     acquired=enrollment, partitions=list(candidate["dataset_pins"]),
                                     initialization="fresh random; no pretrained weights",
                                     selection="preregistered validation only"),
                  candidate_sha256=candidate_sha, fitting=selection, decision=decision,
                  before=before, after=after)
    write_new(OUTPUT / "improvement.json", report)
    print(decision)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--device", choices=("cpu", "cuda"), default="cpu")
    args = parser.parse_args()
    if os.environ.get("OPENBLAS_NUM_THREADS") != "1":
        parser.error("Set OPENBLAS_NUM_THREADS=1")
    trial(args.device)


if __name__ == "__main__":
    main()
