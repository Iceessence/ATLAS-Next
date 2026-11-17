"""Evaluate frozen public research suites, never update model parameters."""
import json
import time
import numpy as np
from corpus import ROOT, ROUTES, digest, features, rows, rule
from compare import exemplar_router

def main():
    folder=ROOT/"lab"
    sources=[folder/f"{name}.json" for name in ("train","validation","holdout")]
    splits=[rows(p) for p in sources]
    for field in ("prompt","family"):
        groups=[{r[field] for r in s} for s in splits]
        assert not any(a&b for i,a in enumerate(groups) for b in groups[i+1:])
    test=splits[-1]
    report={"protocol":"public synthetic routing, disjoint topic and template splits",
            "provenance":"rule_label","heldout_sha256":digest(sources[-1]),"n":len(test),"results":{}}
    predictors={"rule":rule, "exemplar":exemplar_router(splits[0])}
    for name,path in [("native",ROOT/"assets/native.json"),("linear",folder/"linear.json")]:
        manifest=json.loads(path.with_suffix(".manifest.json").read_text())
        assert manifest["checkpoint_sha256"]==digest(path)
        model={k:np.array(v) for k,v in json.loads(path.read_text()).items()}
        def classify(prompt,m=model):
            z=np.array(features(prompt))@m["w"]+m["b"]
            if m["v"].size:
                z=np.tanh(z)@m["v"]+m["c"]
            return ROUTES[z.argmax()]
        predictors[name]=classify
    for name, fn in predictors.items():
        matrix=np.zeros((3,3),dtype=int)
        errors=[]
        times=[]
        for row in test:
            start=time.perf_counter_ns()
            result=fn(row["prompt"])
            times.append((time.perf_counter_ns()-start)/1000)
            matrix[ROUTES.index(row["label"]),ROUTES.index(result)]+=1
            if result!=row["label"]:
                errors.append(dict(prompt=row["prompt"],expected=row["label"],actual=result))
        f1=2*matrix.diagonal()/(matrix.sum(axis=1)+matrix.sum(axis=0))
        report["results"][name]=dict(correct=int(matrix.trace()),accuracy=matrix.trace()/len(test),
            macro_f1=f1.mean(),confusion=matrix.tolist(),p50_us=np.median(times),p95_us=np.percentile(times,95),
            failures=errors)
    report["qualified"]=False
    report["qualification_reason"]="Public synthetic labels and related templates. Independent outcome qualification unavailable."
    report["native_minus_rule_pp"]=100*(report["results"]["native"]["accuracy"]-report["results"]["rule"]["accuracy"])
    report["native_minus_linear_pp"]=100*(report["results"]["native"]["accuracy"]-report["results"]["linear"]["accuracy"])
    dest=ROOT/"out/evaluation.json"
    dest.parent.mkdir(exist_ok=True)
    dest.write_text(json.dumps(report,indent=2))
    for k,v in report["results"].items(): print(k,v["correct"],len(test),v["macro_f1"],v["p95_us"])
if __name__=="__main__": main()
