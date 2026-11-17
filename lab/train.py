"""CPU-only development experiment. Reads train and validation manifests exclusively."""
import json
import os
import platform
import subprocess
import time
import numpy as np
from corpus import ROOT, ROUTES, WIDTH, digest, features, rows

def train(x,y,width,seed):
    rng=np.random.default_rng(seed)
    w=rng.normal(0,0.12,(WIDTH,width or len(ROUTES)))
    b=np.zeros(w.shape[1])
    v=rng.normal(0,0.12,(width,len(ROUTES))) if width else None
    c=np.zeros(len(ROUTES))
    for _ in range(250):
        h=np.tanh(x@w+b) if width else None
        logits=h@v+c if width else x@w+b
        probs=np.exp(logits-logits.max(axis=1,keepdims=True))
        delta=(probs/probs.sum(axis=1,keepdims=True)-y)/len(y)
        if width:
            back=(delta@v.T)*(1-h*h)
            v-=0.55*(h.T@delta)
            c-=0.55*delta.sum(axis=0)
        else:
            back=delta
        w-=0.55*(x.T@back)
        b-=0.55*back.sum(axis=0)
    return dict(w=w.tolist(),b=b.tolist(),v=v.tolist() if width else [],c=c.tolist())

def predict(model,x):
    z=x@np.array(model["w"])+model["b"]
    return np.tanh(z)@np.array(model["v"])+model["c"] if model["v"] else z

def main():
    folder=ROOT/"lab"
    trainfile, valfile=(folder/f"{s}.json" for s in ("train","validation"))
    data,val=rows(trainfile),rows(valfile)
    assert {r["family"] for r in data}.isdisjoint(r["family"] for r in val)
    x=np.array([features(r["prompt"]) for r in data])
    y=np.eye(len(ROUTES))[[ROUTES.index(r["label"]) for r in data]]
    vx=np.array([features(r["prompt"]) for r in val])
    vy=np.array([ROUTES.index(r["label"]) for r in val])
    rev=subprocess.check_output(["git","rev-parse","HEAD"],cwd=ROOT,text=True).strip()
    for name,width,seeds in [("native",16,[19,37,73]),("linear",0,[19])]:
        trials=[]
        best=None
        for seed in seeds:
            start=time.perf_counter()
            model=train(x,y,width,seed)
            score=float((predict(model,vx).argmax(1)==vy).mean())
            trials.append(dict(seed=seed,validation_accuracy=score,seconds=time.perf_counter()-start))
            if best is None or score>best[0]:
                best=(score,seed,model)
        target=(ROOT/"assets" if width else folder)/f"{name}.json"
        target.parent.mkdir(exist_ok=True)
        target.write_text(json.dumps(best[2],separators=(",",":")))
        receipt=dict(id=f"atlas-{name}-route-v1",origin="original",qualification="shadow",routes=ROUTES,
            checkpoint_sha256=digest(target),parameters=WIDTH*(width or 3)+(width or 3)+(width*3+3 if width else 0),
            selected_seed=best[1],trials=trials,training_commit=rev,train_sha256=digest(trainfile),
            validation_sha256=digest(valfile),train_count=len(data),validation_count=len(val),label_origin="rule_label",
            architecture="hashed-binary-tanh-MLP" if width else "hashed-binary-linear-softmax",
            optimizer="full batch gradient descent, 250 steps, learning rate 0.55",device="CPU",
            python=platform.python_version(),numpy=np.__version__,threads=os.environ.get("OPENBLAS_NUM_THREADS"))
        target.with_suffix(".manifest.json").write_text(json.dumps(receipt,indent=2))
if __name__=="__main__":
    main()
