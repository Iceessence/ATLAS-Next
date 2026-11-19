"""Train a bounded ATLAS-original Primary Master for selective source proposals."""
import argparse
from collections import Counter
import os

import numpy as np

from native_data import load_partition, write_new
from native_models import (OUTPUT, SEEDS, SPECIALIST, THRESHOLDS, collect, costs_for, feature_matrix, fit,
                           idf_table, load_model, loss, predict, rank, specialist_action, specialist_inputs)

MASTER = SPECIALIST.parents[1] / "next-018/checkpoint.json"


def specialist_signal(row, idf, model):
    documents = feature_matrix(row, idf)
    p = predict(documents, model)
    top = int(np.argmax(p))
    rest = np.delete(p, top)
    gap = float(p[top] - rest.max()) if rest.size else 0.
    x = [float(p[top]), gap, float(documents[top, 2]), len(p)/32]
    return top, x


def master_examples(rows, replay, idf, specialist):
    signals, labels = [], []
    for row in rows:
        top, signal = specialist_signal(row, idf, specialist)
        signals.append(signal)
        labels.append(int(replay[row["id"]][str(top)]["useful"]))
    return np.asarray(signals), np.asarray(labels)


def crossfit(rows, replay, specialist, device):
    matrices, labels, folds = [], [], []
    for family in sorted({row["family"] for row in rows}):
        train = [row for row in rows if row["family"] != family]
        held = [row for row in rows if row["family"] == family]
        idf = idf_table(train)
        tx, ty = specialist_inputs(train, replay, idf)
        model, metrics = fit(tx, ty, specialist["hidden"], specialist["seed"], device)
        x, y = master_examples(held, replay, idf, model)
        matrices.append(x)
        labels.append(y)
        folds.append(dict(held_family=family, fit_families=sorted({row["family"] for row in train}),
                          held_requests=len(y), held_positive=int(y.sum()), fit_cost=metrics))
    return np.concatenate(matrices), np.concatenate(labels), folds


def gate_action(row, specialist, checkpoint=None, confidence=None):
    top, x = specialist_signal(row, specialist["idf"], specialist["policies"]["native"])
    decision = x[0] if checkpoint is None else float(predict([x], checkpoint)[0])
    threshold = confidence if checkpoint is None else checkpoint["threshold"]
    return (top if decision >= threshold else None), decision


def gate_diagnostics(trials, rows, replay, specialist):
    eligible = {}
    for row in rows:
        top, _ = specialist_signal(row, specialist["idf"], specialist["policies"]["native"])
        eligible[row["id"]] = replay[row["id"]][str(top)]["useful"]
    counters = Counter()
    for trial in trials:
        proposal = trial["action"] is not None
        useful = eligible[trial["id"]]
        label = ("true" if proposal == useful else "false") + ("_positive" if proposal else "_negative")
        counters[label] += 1
    return {key: counters[key] for key in ("true_positive", "true_negative", "false_positive", "false_negative")}


def fit_master(specialist, specialist_sha, device):
    train, train_runs, train_pin = load_partition("train")
    valid, valid_runs, valid_pin = load_partition("validation")
    source_model = specialist["policies"]["native"]
    x, y, folds = crossfit(train, train_runs, source_model, device)
    if set(y) != {0, 1}:
        raise ValueError("Out-of-family proposals lack both measured outcomes; master fitting is blocked")
    vx, vy = master_examples(valid, valid_runs, specialist["idf"], source_model)
    candidates, policies = [], {}
    for name, hidden, seeds in (("native", 6, SEEDS), ("linear", 0, SEEDS[:1])):
        ranked = []
        for seed in seeds:
            model, metrics = fit(x, y, hidden, seed, device)
            p = predict(vx, model)
            brier = float(np.mean((p-vy)**2))
            for threshold in THRESHOLDS:
                selected = dict(model, threshold=threshold)
                report = collect(valid, lambda row: gate_action(row, specialist, selected), valid_runs)
                summary = report["summary"]
                gates = gate_diagnostics(report["trials"], valid, valid_runs, specialist)
                candidates.append(dict(policy=name, seed=seed, threshold=threshold, fit=metrics, gates=gates,
                                       validation=dict(brier=brier, log_loss=loss(vy,p), **summary)))
                score = (summary["useful"], -summary["wrong"], -brier, -abs(.5-threshold), -seed)
                ranked.append((score, selected))
        policies[name] = max(ranked, key=lambda item: item[0])[1]
    checkpoint = dict(schema=1, role="Primary Master", origin="ATLAS-original", qualification="shadow",
                      device=device, external_inference_calls=0, specialist_sha256=specialist_sha,
                      dataset_pins=dict(train=train_pin, validation=valid_pin), policies=policies)
    return checkpoint, dict(crossfit=folds, candidates=candidates)


def evaluate_master(specialist, master):
    rows, replay, pin = load_partition("evaluation")
    source_model = specialist["policies"]["native"]
    policies = {
        "host": lambda row: (None, None),
        "specialist": lambda row: specialist_action("native", row, specialist["idf"], source_model),
        "confidence_gate": lambda row: gate_action(row, specialist, confidence=source_model["threshold"]),
        "linear_gate": lambda row: gate_action(row, specialist, master["policies"]["linear"]),
        "original_master": lambda row: gate_action(row, specialist, master["policies"]["native"]),
    }
    costs = costs_for("evaluation")
    reports = {name: collect(rows, choose, replay, costs) for name, choose in policies.items()}
    for name in ("confidence_gate", "linear_gate", "original_master"):
        report = reports[name]
        report["summary"]["gates"] = gate_diagnostics(report["trials"], rows, replay, specialist)
        report["summary"]["host_fallbacks"] = sum(t["action"] is None for t in report["trials"])
    return dict(scope="author-public", partition_sha256=pin, results=reports)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("fit", "evaluate"))
    parser.add_argument("--device", choices=("cpu", "cuda"), default="cpu")
    args = parser.parse_args()
    if os.environ.get("OPENBLAS_NUM_THREADS") != "1":
        parser.error("Set OPENBLAS_NUM_THREADS=1")
    specialist, digest = load_model(SPECIALIST, "source specialist")
    path = OUTPUT / "master.json"
    if args.command == "fit":
        master, evidence = fit_master(specialist, digest, args.device)
        write_new(path, master)
        write_new(OUTPUT / "master-fit.json", evidence)
        print({policy: (p["seed"], p["threshold"], p["parameters"]) for policy,p in master["policies"].items()})
    else:
        master, master_digest = load_model(path, "Primary Master")
        if master["specialist_sha256"] != digest:
            raise ValueError("Master belongs to a different specialist")
        report = evaluate_master(specialist, master)
        write_new(OUTPUT / "master-evaluation.json", dict(checkpoint_sha256=master_digest, **report))
        print({policy: result["summary"] for policy,result in report["results"].items()})


if __name__ == "__main__":
    main()
