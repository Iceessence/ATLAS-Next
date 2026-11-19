"""Original ATLAS learning primitives and an offline source-specialist experiment."""
import argparse
import json
import os
from pathlib import Path
import time
import tracemalloc

import numpy as np

from native_data import DATASET, load_partition, write_new
from outcomes import ROOT, sha
from project_specialist import cosine, features, idf_table, tokens

SEEDS = (11, 37, 73)
THRESHOLDS = (0.35, 0.5, 0.65)
STEPS = 600
LEARNING_RATE = 0.03
OUTPUT = ROOT / "out/native-first"
SPECIALIST = ROOT / "docs/evidence/next-017/checkpoint.json"


def backend(device):
    if device == "cpu":
        return np
    if device != "cuda":
        raise ValueError("Unknown training device")
    try:
        import cupy
        cupy.cuda.Device().synchronize()
    except (ImportError, RuntimeError) as error:
        raise ValueError("CUDA requires an installed CuPy runtime and available NVIDIA device") from error
    return cupy


def probability(x, weights, xp=np):
    w, b = weights[:2]
    scores = x @ w + b
    if len(weights) == 4:
        scores = xp.tanh(scores) @ weights[2] + weights[3]
    return 1 / (1 + xp.exp(-xp.clip(scores.ravel(), -50, 50)))


def predict(x, checkpoint):
    return probability(np.asarray(x), [np.asarray(p) for p in checkpoint["weights"]])


def loss(y, p):
    p = np.clip(p, 1e-12, 1-1e-12)
    return float(-np.mean(y*np.log(p) + (1-y)*np.log(1-p)))


def initialize(inputs, hidden, seed):
    rng = np.random.default_rng(seed)
    weights = [rng.normal(0, np.sqrt(1/inputs), (inputs, hidden or 1)), np.zeros(hidden or 1)]
    if hidden:
        weights.extend([rng.normal(0, np.sqrt(1/hidden), (hidden, 1)), np.zeros(1)])
    return weights


def fit(x, y, hidden, seed, device="cpu"):
    if not len(y) or set(y) != {0, 1}:
        raise ValueError("Training requires observed positive and negative outcomes")
    xp = backend(device)
    to_cpu = np.asarray if device == "cpu" else xp.asnumpy
    initial = initialize(x.shape[1], hidden, seed)
    initial_loss = loss(y, probability(x, initial))
    x_dev, y_dev = xp.asarray(x), xp.asarray(y)
    weights = [xp.asarray(p.copy()) for p in initial]
    m = [xp.zeros_like(p) for p in weights]
    v = [xp.zeros_like(p) for p in weights]
    class_scale = xp.where(y_dev == 1, .5/y.sum(), .5/(len(y)-y.sum()))
    start = time.perf_counter_ns()
    tracemalloc.start()
    try:
        for step in range(1, STEPS+1):
            delta = ((probability(x_dev, weights, xp)-y_dev)*class_scale)[:, None]
            if hidden:
                h = xp.tanh(x_dev @ weights[0]+weights[1])
                d = (delta @ weights[2].T)*(1-h*h)
                derivatives = [x_dev.T @ d, d.sum(axis=0), h.T @ delta, delta.sum(axis=0)]
            else:
                derivatives = [x_dev.T @ delta, delta.sum(axis=0)]
            for i, gradient in enumerate(derivatives):
                m[i] = .9*m[i]+.1*gradient
                v[i] = .999*v[i]+.001*gradient*gradient
                weights[i] -= LEARNING_RATE*(m[i]/(1-.9**step))/(xp.sqrt(v[i]/(1-.999**step))+1e-8)
        final = [to_cpu(p) for p in weights]
        momentum = [to_cpu(p).tolist() for p in m]
        variance = [to_cpu(p).tolist() for p in v]
        _, peak = tracemalloc.get_traced_memory()
    finally:
        tracemalloc.stop()
    fit_ms = (time.perf_counter_ns()-start)/1e6
    checkpoint = dict(seed=seed, inputs=x.shape[1], hidden=hidden, parameters=sum(p.size for p in final),
                      initialization=dict(kind="random_normal", sha256=sha(b"".join(p.astype("<f8").tobytes() for p in initial))),
                      weights=[p.tolist() for p in final],
                      optimizer=dict(name="Adam", steps=STEPS, learning_rate=LEARNING_RATE, m=momentum, v=variance),
                      architecture="tanh MLP" if hidden else "logistic regression")
    metrics = dict(samples=len(y), positive=int(y.sum()), initial_log_loss=initial_loss,
                   final_log_loss=loss(y, probability(x, final)), fit_ms=fit_ms,
                   peak_python_traced_bytes=peak, parameter_bytes=sum(p.nbytes for p in final))
    return checkpoint, metrics


def feature_matrix(row, idf):
    return np.asarray([features(row["question"], document, idf) for document in row["documents"]])


def specialist_inputs(rows, replay, idf):
    x = np.concatenate([feature_matrix(row, idf) for row in rows])
    y = np.array([int(replay[row["id"]][str(i)]["useful"])
                  for row in rows for i in range(len(row["documents"]))])
    return x, y


def rank(p, cutoff):
    index = int(np.argmax(p))
    return (index if p[index] >= cutoff else -1), float(p[index])


def specialist_action(policy, row, idf, model=None):
    if policy == "host":
        return None, None
    if policy == "tfidf":
        query = tokens(row["question"])
        scores = [cosine(query, tokens(doc), idf) for doc in row["documents"]]
        return rank(scores, 1e-12)
    return rank(predict(feature_matrix(row, idf), model), model["threshold"])


def costs_for(role):
    return json.loads((DATASET / (role + "-cost.json")).read_bytes())


def collect(rows, choose, replay, costs=None):
    trials = []
    for row in rows:
        start = time.perf_counter_ns()
        action, prediction = choose({k: row[k] for k in ("question", "documents")})
        scoring_us = (time.perf_counter_ns()-start)/1000
        result = replay[row["id"]][str(action)]
        cost = {} if costs is None else costs[row["id"]][str(action)]
        selected = result["primary"] if action is None else action
        expected = row["expect"].get("document")
        trials.append(dict(id=row["id"], family=row["family"], action=action, prediction=prediction,
                           route_matches=expected is not None and selected in (expected, *row["expect"].get("equivalent_documents", [])),
                           scoring_us=scoring_us, **result, **cost))
    families = {}
    for family in sorted({trial["family"] for trial in trials}):
        families[family] = counts([trial for trial in trials if trial["family"] == family])
    summary = dict(**counts(trials), families=families,
                   p50_scoring_us=percentile(trials, "scoring_us", 50),
                   p95_scoring_us=percentile(trials, "scoring_us", 95))
    if costs is not None:
        summary.update(p50_engine_us=percentile(trials, "engine_us", 50),
                       p95_engine_us=percentile(trials, "engine_us", 95),
                       p50_process_ms=percentile(trials, "process_ms", 50),
                       peak_host_working_set=max(t["host_working_set"] for t in trials))
    return dict(summary=summary, trials=trials)


def counts(trials):
    return dict(tasks=len(trials), useful=sum(t["useful"] for t in trials),
                route_matches=sum(t["route_matches"] for t in trials),
                delivered=sum(t["state"] == "completed" for t in trials),
                wrong=sum(t["state"] == "completed" and not t["useful"] for t in trials),
                withheld=sum(t["state"] != "completed" for t in trials),
                explicit_abstentions=sum(t["action"] == -1 for t in trials))


def percentile(trials, key, q):
    return float(np.percentile([t[key] for t in trials], q))


def fit_specialist(roles, device):
    if roles not in (("train",), ("train", "acquisition")):
        raise ValueError("Only preregistered training partitions are eligible")
    train, replay, pins = [], {}, {}
    for role in roles:
        rows, runs, pins[role] = load_partition(role)
        train.extend(rows)
        replay.update(runs)
    valid, valid_runs, pins["validation"] = load_partition("validation")
    idf = idf_table(train)
    x, y = specialist_inputs(train, replay, idf)
    vx, vy = specialist_inputs(valid, valid_runs, idf)
    policies, candidates = {}, []
    for policy, hidden, seeds in (("native", 8, SEEDS), ("linear", 0, SEEDS[:1])):
        ranked = []
        for seed in seeds:
            model, fit_metrics = fit(x, y, hidden, seed, device)
            p = predict(vx, model)
            brier = float(np.mean((p-vy)**2))
            for cutoff in THRESHOLDS:
                candidate = dict(model, threshold=cutoff)
                report = collect(valid, lambda row: specialist_action(policy, row, idf, candidate), valid_runs)
                metric = report["summary"]
                validation = dict(brier=brier, log_loss=loss(vy, p), **metric)
                candidates.append(dict(policy=policy, seed=seed, cutoff=cutoff, fit=fit_metrics, validation=validation))
                key = (metric["useful"], -metric["wrong"], -brier, -abs(.5-cutoff), -seed)
                ranked.append((key, candidate))
        policies[policy] = max(ranked, key=lambda item: item[0])[1]
    bundle = dict(schema=1, role="source specialist", origin="ATLAS-original", qualification="shadow",
                  device=device, numpy_version=np.__version__, external_inference_calls=0,
                  dataset_pins=pins, idf=idf, policies=policies)
    return bundle, candidates


def load_model(path, role, expected_sha=None):
    data = path.read_bytes()
    digest = sha(data)
    bundle = json.loads(data)
    if (bundle["schema"], bundle["role"], bundle["origin"], bundle["qualification"], bundle["external_inference_calls"]) != (
            1, role, "ATLAS-original", "shadow", 0) or expected_sha is not None and digest != expected_sha:
        raise ValueError("Invalid or mismatched shadow checkpoint")
    return bundle, digest


def evaluate_specialist(bundle, role="evaluation"):
    rows, replay, pin = load_partition(role)
    costs = costs_for(role)
    results = {}
    for policy in ("host", "tfidf", "linear", "native"):
        model = bundle["policies"].get(policy)
        results[policy] = collect(rows, lambda row: specialist_action(policy, row, bundle["idf"], model), replay, costs)
    return dict(scope="author-public", partition=role, partition_sha256=pin, results=results)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("fit", "evaluate"))
    parser.add_argument("--device", choices=("cpu", "cuda"), default="cpu")
    parser.add_argument("--checkpoint", type=Path, default=OUTPUT / "specialist.json")
    args = parser.parse_args()
    if os.environ.get("OPENBLAS_NUM_THREADS") != "1":
        parser.error("Set OPENBLAS_NUM_THREADS=1")
    if args.command == "fit":
        bundle, selection = fit_specialist(("train",), args.device)
        write_new(args.checkpoint, bundle)
        write_new(OUTPUT / "specialist-fit.json", selection)
        print({policy: (p["seed"], p["threshold"], p["parameters"]) for policy,p in bundle["policies"].items()})
    else:
        bundle, digest = load_model(args.checkpoint, "source specialist")
        result = evaluate_specialist(bundle)
        write_new(OUTPUT / "specialist-evaluation.json", dict(checkpoint_sha256=digest, **result))
        print({policy: report["summary"] for policy, report in result["results"].items()})


if __name__ == "__main__":
    main()
