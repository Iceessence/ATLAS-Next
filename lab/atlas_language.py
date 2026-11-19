"""Original raw-text intent inference with fixed read-only source grounding."""
import argparse
from collections import Counter
import json
import math
import os
import re
import time
import tracemalloc

import numpy as np

from foundry import DATA, FROZEN, candidate_key, load
from foundry_sources import MODES
from master_spec import installed
from native_data import ROOT, normalized, sha, write_new

OUTPUT = DATA.parent
CHECKPOINT = ROOT / "docs/evidence/next-022/language.json"
SECTIONS = ("CH-015", "CH-072", "CH-129", "CH-162", "CH-169", "CH-172")
SEED = 37
STEPS = 400
WIDTH = 16
MAX_VOCAB = 384


def grams(text):
    text = "^" + normalized(text) + "$"
    return {text[i:i+n] for n in (2, 3, 4) for i in range(len(text)-n+1)}


def vocabulary(texts):
    counts = Counter(g for text in texts for g in grams(text))
    return [g for g, _ in sorted(counts.items(), key=lambda item: (-item[1], item[0]))[:MAX_VOCAB]]


def vector(text, vocab):
    seen = grams(text)
    return np.array([float(token in seen) for token in vocab])


def matrix(texts, vocab):
    x = np.stack([vector(text, vocab) for text in texts])
    return x / np.sqrt(np.maximum(1, x.sum(axis=1, keepdims=True)))


def softmax(logits):
    exp = np.exp(logits - np.max(logits, axis=1, keepdims=True))
    return exp / exp.sum(axis=1, keepdims=True)


def probabilities(x, model):
    w = [np.asarray(p) for p in model["weights"]]
    h = x @ w[0] + w[1]
    return softmax(np.tanh(h) @ w[2] + w[3] if model["hidden"] else h)


def fit_weights(x, labels, outputs, hidden=WIDTH, seed=SEED):
    labels = np.asarray(labels, dtype=int)
    if set(labels) != set(range(outputs)):
        raise ValueError("Every declared class needs eligible examples")
    count = x.shape[1]
    rng = np.random.default_rng(seed)
    w = [rng.normal(0, count**-.5, (count, hidden or outputs)), np.zeros(hidden or outputs)]
    if hidden:
        w.extend([rng.normal(0, hidden**-.5, (hidden, outputs)), np.zeros(outputs)])
    initial = sha(b"".join(p.astype("<f8").tobytes() for p in w))
    y = np.eye(outputs)[labels]
    m, v = [np.zeros_like(p) for p in w], [np.zeros_like(p) for p in w]
    def run():
        h = x @ w[0] + w[1]
        if hidden:
            h = np.tanh(h)
            return h, softmax(h @ w[2]+w[3])
        return None, softmax(h)
    def loss(p):
        return float(-np.mean(np.log(p[np.arange(len(labels)), labels].clip(1e-12))))
    start_loss = loss(run()[1])
    class_scale = 1 / (outputs * np.bincount(labels)[labels])
    start = time.perf_counter_ns()
    tracemalloc.start()
    try:
        for step in range(1, STEPS+1):
            h, p = run()
            d = (p-y)*class_scale[:, None]
            if hidden:
                upstream = (d @ w[2].T)*(1-h*h)
                gradients = [x.T@upstream, upstream.sum(axis=0), h.T@d, d.sum(axis=0)]
            else:
                gradients = [x.T@d, d.sum(axis=0)]
            for i, g in enumerate(gradients):
                m[i] = .9*m[i]+.1*g
                v[i] = .999*v[i]+.001*g*g
                w[i] -= .03*(m[i]/(1-.9**step))/(np.sqrt(v[i]/(1-.999**step))+1e-8)
        peak = tracemalloc.get_traced_memory()[1]
    finally:
        tracemalloc.stop()
    checkpoint = dict(hidden=hidden, parameters=sum(p.size for p in w), seed=seed, initialization_sha256=initial,
                      weights=[p.tolist() for p in w],
                      optimizer=dict(kind="Adam", steps=STEPS, learning_rate=.03,
                                     momentum=[p.tolist() for p in m], variance=[p.tolist() for p in v]))
    metrics = dict(samples=len(labels), label_counts=np.bincount(labels).tolist(), loss_before=start_loss,
                   loss_after=loss(run()[1]), fit_ms=(time.perf_counter_ns()-start)/1e6,
                   peak_python_traced_bytes=peak, parameter_bytes=sum(p.nbytes for p in w))
    return checkpoint, metrics


def language_training(roles=("train",)):
    if roles not in (("train",), ("train", "acquisition")):
        raise ValueError("Only preregistered training roles are eligible")
    examples, pins = [], {}
    manifest = json.loads(FROZEN.read_bytes())
    for role in roles:
        batch = load(role)
        pins[role] = manifest["partitions"][role]["sha256"]
        for row in batch["rows"]:
            task = row["task"]
            target = task["oracle"].get("document")
            key = "withhold" if target is None else candidate_key(target)
            envelope = row["outcomes"].get(key)
            if not envelope:
                continue
            record = envelope["record"]
            if record["eligible_for_training"] and record["outcome_class"] in ("CORRECT_DELIVERY", "CORRECT_WITHHOLD"):
                examples.append(dict(text=task["question"], target=task["kind"], origin=task["case_class"],
                                     receipt=record["evaluation_receipt_sha256"]))
    return examples, pins


def train(roles=("train",)):
    spec = installed()
    spec.consult(*SECTIONS)
    examples, pins = language_training(roles)
    texts = [e["text"] for e in examples]
    vocab = vocabulary(texts)
    x = matrix(texts, vocab)
    labels = [MODES.index(e["target"]) for e in examples]
    models, meters = {}, {}
    for name, hidden in (("native", WIDTH), ("linear", 0)):
        models[name], meters[name] = fit_weights(x, labels, len(MODES), hidden)
    bundle = dict(schema="atlas.language_shadow.v1", origin="ATLAS_ORIGINAL_SCRATCH", qualification="SHADOW",
                  canonical_master_sha256=spec.sha256, sections_consulted=list(SECTIONS), external_inference_calls=0,
                  vocab=vocab, tokenizer="train-only NFKC casefold character 2/3/4-grams, binary sqrt-normalized",
                  numpy_version=np.__version__, data_pins=pins, examples=examples, classes=MODES, models=models)
    return bundle, meters


def load_checkpoint(path=CHECKPOINT, pin=None):
    data = path.read_bytes()
    result = json.loads(data)
    if (result["schema"], result["origin"], result["qualification"], result["external_inference_calls"]) != (
            "atlas.language_shadow.v1", "ATLAS_ORIGINAL_SCRATCH", "SHADOW", 0) or pin and sha(data) != pin:
        raise ValueError("Unexpected language checkpoint")
    spec = installed()
    if spec.sha256 != result["canonical_master_sha256"]:
        raise ValueError("Language checkpoint uses a different canonical plan")
    spec.consult(*result["sections_consulted"])
    return result, sha(data)


def compatible(source, mode):
    return ((source["suffix"] == ".md" and mode == "read_fact")
            or (source["suffix"] == ".py" and mode == "locate_definition"))


def referenced(question, profile):
    text = normalized(question)
    return [i for i,s in enumerate(profile["sources"])
            if re.search(r"(?<![\w.])"+re.escape(s["key"].casefold())+r"(?!\w|\.\w)", text)
            or s["suffix"] == ".py" and re.search(
                r"(?<![\w.])"+re.escape(s["key"].split(".")[-1].casefold())+r"(?!\w|\.\w)", text)]


def disallowed(question):
    # This guard controls shadow action eligibility. It never re-authorizes the
    # original request or grants a claimed answer delivery to a model.
    text = normalized(question).replace("’", "'")
    return re.search(r"\b(?:if|unless|not|never|no|instead|skip|omit|change|rebuild|build|install|execute|run|don't)\b", text) is not None


def grounded_action(question, profile, mode):
    if mode == "withhold" or disallowed(question):
        return "withhold"
    matches = referenced(question, profile)
    if len(matches) != 1 or not compatible(profile["sources"][matches[0]], mode):
        return "withhold"
    return candidate_key(matches[0])


def typed_baseline(question, profile):
    matches = referenced(question, profile)
    if disallowed(question) or len(matches) != 1:
        return "withhold"
    return candidate_key(matches[0])


def tfidf_router(bundle):
    # All statistics and reference prompts are from the fit partition only.
    samples = bundle["examples"]
    terms = [set(re.findall(r"\w+", normalized(e["text"]))) for e in samples]
    idf = {word: math.log((1+len(terms))/(1+sum(word in terms_i for terms_i in terms)))+1
           for words in terms for word in words}
    norms = [sum(idf[t]**2 for t in sorted(words)) for words in terms]
    def predict(text):
        query = set(re.findall(r"\w+", normalized(text)))
        q = sum(idf.get(t, 1)**2 for t in sorted(query))
        def similarity(i):
            shared = sum(idf[t]**2 for t in sorted(terms[i] & query))
            return shared/(q*norms[i])**.5 if q*norms[i] else 0.
        best = max(range(len(samples)), key=similarity)
        return samples[best]["target"]
    return predict


def infer(bundle, question, policy="native"):
    p = probabilities(matrix([question], bundle["vocab"]), bundle["models"][policy])[0]
    return MODES[int(np.argmax(p))], p.tolist()


def previous_router():
    # The deployed original router has no declaration class.
    from corpus import ROUTES, features
    data = (ROOT / "assets/native.json").read_bytes()
    if sha(data) != "6b9b23fcf890807f2a1d989f1b9870660bcf3030ce6e729674844e9f722076c4":
        raise ValueError("Historical original router checkpoint differs")
    params = {k: np.asarray(v) for k,v in json.loads(data).items()}
    def predict(text):
        h = np.tanh(np.asarray(features(text)) @ params["w"]+params["b"])
        logits = h @ params["v"]+params["c"]
        return "read_fact" if ROUTES[int(np.argmax(logits))] == "fact" else "withhold"
    return predict


def summarize(records):
    totals = Counter(row["outcome"] for row in records)
    return dict(tasks=len(records), useful=totals["CORRECT_DELIVERY"]+totals["CORRECT_WITHHOLD"],
                completed=totals["CORRECT_DELIVERY"], safe_withholds=totals["CORRECT_WITHHOLD"],
                wrong_shadow_proposals=totals["INCORRECT_DELIVERY"], false_abstentions=totals["FALSE_ABSTENTION"],
                unsupported=totals["UNSUPPORTED_SCOPE"], unavailable=totals["UNAVAILABLE"],
                model_only_intent_matches=sum(row["intent_match"] for row in records),
                p95_decision_us=float(np.percentile([row["decision_us"] for row in records],95)))


def evaluate(bundle, role):
    batch = load(role)
    costs = json.loads((DATA / (role+"-cost.json")).read_bytes())
    results = {}
    tfidf, incumbent = tfidf_router(bundle), previous_router()
    for policy in ("host_raw", "exact_typed", "tfidf", "linear", "previous_original", "native"):
        rows = []
        for row in batch["rows"]:
            task = row["task"]
            question, profile = task["question"], batch["profiles"][task["family"]]
            start = time.perf_counter_ns()
            mode, values = None, None
            if policy == "host_raw":
                action = "raw"
            elif policy == "exact_typed":
                action = typed_baseline(question, profile)
            else:
                if policy == "tfidf":
                    mode = tfidf(question)
                elif policy == "previous_original":
                    mode = incumbent(question)
                else:
                    mode, values = infer(bundle, question, policy)
                action = grounded_action(question, profile, mode)
            decision_us = (time.perf_counter_ns()-start)/1000
            env = row["outcomes"].get(action)
            outcome = env["record"]["outcome_class"] if env else "UNAVAILABLE"
            rows.append(dict(id=task["id"], family=task["family"], case_class=task["case_class"], action=action,
                             expected_intent=task["kind"], mode=mode, intent_match=mode==task["kind"],
                             scores=values, outcome=outcome, decision_us=decision_us,
                             cost=costs[task["id"]].get(action)))
        results[policy] = dict(summary=summarize(rows), rows=rows)
    return dict(partition=role, qualification="SHADOW", external_inference_calls=0, results=results)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("fit", "evaluate"))
    args = parser.parse_args()
    if os.environ.get("OPENBLAS_NUM_THREADS") != "1":
        parser.error("Set OPENBLAS_NUM_THREADS=1")
    candidate = OUTPUT / "language.json"
    if args.command == "fit":
        bundle, costs = train()
        write_new(candidate, bundle)
        write_new(OUTPUT / "language-fit.json", costs)
        print(costs)
    else:
        bundle, pin = load_checkpoint(candidate)
        reports = {part: evaluate(bundle, part) for part in ("validation", "evaluation")}
        write_new(OUTPUT / "language-evaluation.json", dict(checkpoint_sha256=pin, **reports))
        for part, report in reports.items():
            print(part, {policy: view["summary"] for policy, view in report["results"].items()})


if __name__ == "__main__":
    main()
