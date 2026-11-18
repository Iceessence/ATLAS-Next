"""CPU-only document routing experiment. Candidate checkpoints are never loaded by the app."""
import argparse
from collections import Counter
import json
import math
import os
from pathlib import Path
import re
import time
import tracemalloc

import numpy as np
from outcomes import ROOT, PUBLIC, load_suite, probe, score, sha
from project_cases import cases
from project_audit import DATA

SEEDS = (11, 37, 73)
CUTOFFS = (0.35, 0.5, 0.65)
OUT = ROOT / "out/project-specialist"
CHECKPOINT = Path(__file__).with_name("project-specialist.shadow.json")


def tokens(text):
    return re.findall(r"\w+", text.casefold())


def idf_table(training):
    documents = {doc for case in training for doc in case["documents"]}
    frequencies = Counter(token for doc in documents for token in set(tokens(doc)))
    return {token: math.log((1+len(documents))/(1+n))+1 for token,n in sorted(frequencies.items())}


def cosine(q, d, idf):
    def vector(words):
        return {w: (1+math.log(n))*idf.get(w, 1.0) for w,n in Counter(words).items()}
    a, b = vector(q), vector(d)
    norm = math.sqrt(sum(x*x for x in a.values())*sum(x*x for x in b.values()))
    return sum(v*b.get(w,0) for w,v in a.items())/norm if norm else 0.0


def features(question, document, idf):
    q = tokens(question)
    words = set(q)
    scores = []
    for line in document.splitlines():
        key, sep, _ = line.partition(":")
        if not sep:
            continue
        ts = tokens(key)
        overlap = words.intersection(ts)
        grams = lambda s: {s[i:i+3] for i in range(len(s)-2)}
        a, b = grams(" ".join(q)), grams(" ".join(ts))
        scores.append([cosine(q, ts, idf), len(overlap)/max(1,len(set(ts))),
                       len(overlap)/max(1,len(words)), len(a&b)/max(1,len(a|b)),
                       float((" "+" ".join(ts)+" ") in (" "+" ".join(q)+" "))])
    return [cosine(q,tokens(document),idf), *np.max(scores or [[0]*5],axis=0)]


def observe(case, proposal):
    clock = time.perf_counter_ns()
    proc = probe(case["question"],case["documents"],proposed_document=proposal)
    wall = (time.perf_counter_ns()-clock)/1e6
    if proc.returncode:
        raise RuntimeError(f"{case['id']} probe failed: {proc.stderr[-500:]}")
    run = json.loads(proc.stdout)
    answer = run["answer"]
    checks = score(case,run)
    cite = answer.get("evidence") or {}
    proposed_id = answer.get("primaryCandidateId")
    return dict(primary_document=run["ids"].index(proposed_id) if proposed_id in run["ids"] else None,
                ok=all(checks.values()), state=answer["state"], result=answer["result"],
                verdict=(answer.get("researchReview") or {}).get("verdict"),
                selected=run["ids"].index(cite["id"]) if cite.get("id") in run["ids"] else None,
                citation=cite.get("span"), failed=[k for k,v in checks.items() if not v],
                engine_us=run["elapsedUs"], process_ms=wall, working_set=run["workingSetBytes"])


def replay_inputs(rows):
    # Every alternative goes through the real host with all documents visible to Research.
    # Identical proposed actions across policies reuse one measured counterfactual.
    return {row["id"]: {str(i): observe(row,i) for i in [None,-1,*range(len(row["documents"]))]} for row in rows}


def sigmoid(z):
    return 1/(1+np.exp(-np.clip(z,-50,50)))


def fit(x,y,hidden,seed):
    rng = np.random.default_rng(seed)
    w = rng.normal(0,0.15,(x.shape[1],hidden or 1))
    b = np.zeros(hidden or 1)
    v = rng.normal(0,0.15,hidden)
    c = 0.0
    for _ in range(600):
        h = np.tanh(x@w+b) if hidden else None
        p = sigmoid(h@v+c if hidden else (x@w+b).ravel())
        delta = (p-y)/len(y)
        if hidden:
            dw = delta[:,None]*v*(1-h*h)
            v -= 0.25*(h.T@delta + 0.001*v)
            c -= 0.25*delta.sum()
        else:
            dw = delta[:,None]
        w -= 0.25*(x.T@dw + 0.001*w)
        b -= 0.25*dw.sum(axis=0)
    return dict(w=w.tolist(),b=b.tolist(),v=v.tolist(),c=c)


def predict(x, model):
    scores = x@np.array(model["w"])+np.array(model["b"])
    return sigmoid(np.tanh(scores)@model["v"]+model["c"] if model["v"] else scores.ravel())


def evaluate(rows, replay, idf, model=None, policy="native", threshold=0.5):
    trials = []
    for row in rows:
        t = time.perf_counter_ns()
        if policy == "host":
            choice = None
            probability = None
        else:
            x = np.array([features(row["question"],d,idf) for d in row["documents"]])
            p = x[:,0] if policy=="tfidf" else predict(x,model)
            choice = int(np.argmax(p))
            probability = float(p[choice])
            if probability < threshold:
                choice = -1
        overhead = (time.perf_counter_ns()-t)/1000
        result = replay[row["id"]][str(choice)]
        expected = row["expect"]["document"]
        chosen = result["primary_document"] if policy=="host" else choice
        trials.append(dict(id=row["id"],family=row["family"],expected_document=expected,proposal=chosen,
                           route_correct=chosen==expected,confidence=probability,
                           scoring_us=overhead,**result))
    n = len(trials)
    delivered = [t for t in trials if t["state"]=="completed"]
    summary = dict(total=n,route_correct=sum(t["route_correct"] for t in trials),
                   useful=sum(t["ok"] for t in trials),delivered=len(delivered),
                   wrong_deliveries=sum(not t["ok"] for t in delivered),
                   proposed_abstentions=sum(t["proposal"]==-1 for t in trials),
                   host_withheld=n-len(delivered),coverage=len(delivered)/n,
                   p50_scoring_us=float(np.median([t["scoring_us"] for t in trials])),
                   p95_scoring_us=float(np.percentile([t["scoring_us"] for t in trials],95)),
                   p50_engine_us=float(np.median([t["engine_us"] for t in trials])),
                   p95_engine_us=float(np.percentile([t["engine_us"] for t in trials],95)),
                   p50_process_ms=float(np.median([t["process_ms"] for t in trials])),
                   peak_host_working_set=max(t["working_set"] for t in trials))
    # Calibration target is useful delivery of the top-ranked document, before thresholding.
    if policy in ("linear","native"):
        forecasts = []
        for row in rows:
            x = np.array([features(row["question"],d,idf) for d in row["documents"]])
            p = predict(x,model)
            top = int(np.argmax(p))
            forecasts.append((float(p[top]),int(replay[row["id"]][str(top)]["ok"])))
        summary["top_candidate_brier"] = float(np.mean([(p-y)**2 for p,y in forecasts]))
        bins = []
        for lower in (0.,0.25,0.5,0.75):
            members=[(p,y) for p,y in forecasts if lower<=p and (p<lower+0.25 or lower==0.75)]
            if members:
                bins.append(dict(lower=lower,n=len(members),confidence=float(np.mean([p for p,_ in members])),
                                 success=float(np.mean([y for _,y in members]))))
        summary["calibration_bins"]=bins
        summary["ece"]=sum(b["n"]*abs(b["confidence"]-b["success"]) for b in bins)/n
    return dict(summary=summary,trials=trials)


def save(path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(obj, indent=2,allow_nan=False)+"\n").encode())


def fit_experiment():
    train, valid = cases("train"), cases("validation")
    idf = idf_table(train)
    train_runs, valid_runs = replay_inputs(train), replay_inputs(valid)
    x, y = [], []
    for row in train:
        for i,d in enumerate(row["documents"]):
            x.append(features(row["question"],d,idf))
            y.append(float(train_runs[row["id"]][str(i)]["ok"]))
    x, y = np.array(x), np.array(y)
    if set(y)!={0.,1.}:
        raise ValueError("Need both measured successes and rejections")
    candidates, selection = [], {}
    for name, hidden, seeds in [("linear",0,(0,)),("native",6,SEEDS)]:
        ranked = []
        for seed in seeds:
            tracemalloc.start()
            start = time.perf_counter_ns()
            weights = fit(x,y,hidden,seed)
            fit_ms = (time.perf_counter_ns()-start)/1e6
            _, peak = tracemalloc.get_traced_memory()
            tracemalloc.stop()
            for cutoff in CUTOFFS:
                evaluated = evaluate(valid,valid_runs,idf,weights,name,cutoff)
                metrics = evaluated["summary"]
                candidates.append(dict(policy=name,seed=seed,threshold=cutoff,fit_ms=fit_ms,fit_peak_traced_bytes=peak,
                                       **evaluated))
                ranked.append(((metrics["useful"],-metrics["wrong_deliveries"],-metrics["top_candidate_brier"],
                                -abs(0.5-cutoff),-seed),dict(seed=seed,threshold=cutoff,weights=weights)))
        selection[name]=max(ranked,key=lambda pair:pair[0])[1]
    bundle=dict(qualification="shadow",specialist="project document proposal",idf=idf,policies=selection,
                source_pins={s:sha((DATA/s/"pins.json").read_bytes()) for s in ["train","validation"]})
    save(OUT/"checkpoint.json",bundle)
    save(OUT/"fitting.json",dict(qualification="shadow",training_proposals=len(y),training_passes=int(y.sum()),
                                 source_pins=bundle["source_pins"],training_outcomes=train_runs,validation_trials=candidates))
    print(f"Trained on {len(y)} measured proposals across four upstream families, {int(y.sum())} successful.")


def stress_suite(bundle):
    # These are public constructed regressions. Their results never enter fitting or seed selection.
    rows, digest = load_suite(PUBLIC)
    report = {}
    for case in rows:
        cache = {}
        trials = {}
        for policy in ("host","tfidf","linear","native"):
            choice = None
            if policy != "host" and case["documents"]:
                x = np.array([features(case["question"],d,bundle["idf"]) for d in case["documents"]])
                spec = bundle["policies"].get(policy,{})
                scores = x[:,0] if policy=="tfidf" else predict(x,spec["weights"])
                choice = int(np.argmax(scores)) if max(scores)>=spec.get("threshold",0) else -1
            if choice not in cache:
                cache[choice] = observe(case,choice)
            trials[policy] = dict(proposal=choice,**cache[choice])
        report[case["id"]]=trials
    return dict(origin="author_fixture",suite_sha256=digest,trials=report)


def benchmark():
    bundle=json.loads(CHECKPOINT.read_bytes())
    if bundle["qualification"]!="shadow":
        raise ValueError("Unqualified checkpoint")
    rows=cases("evaluation")
    replay=replay_inputs(rows)
    result={}
    for policy in ["host","tfidf","linear","native"]:
        spec=bundle["policies"].get(policy,{})
        result[policy]=evaluate(rows,replay,bundle["idf"],spec.get("weights"),policy,spec.get("threshold",0.0))
        print(policy,result[policy]["summary"])
    save(OUT/"benchmark.json",dict(qualification="shadow",checkpoint_sha256=sha(CHECKPOINT.read_bytes()),
                                  evaluation_pins=sha((DATA/"evaluation/pins.json").read_bytes()),results=result,
                                  constructed_stress_suite=stress_suite(bundle)))
    return result


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command",choices=["fit","evaluate"])
    args=parser.parse_args()
    if os.environ.get("OPENBLAS_NUM_THREADS")!="1":
        parser.error("Set OPENBLAS_NUM_THREADS=1 for repeatable CPU conditions")
    fit_experiment() if args.command=="fit" else benchmark()
