"""Generate public golden vectors for independent C# runtime parity checks."""
import json
import numpy as np
from corpus import ROOT, ROUTES, features, rows
from train import predict

model=json.loads((ROOT/"assets/native.json").read_text())
cases=[]
for row in rows(ROOT/"lab/holdout.json"):
    z=predict(model,np.array(features(row["prompt"])))
    p=np.exp(z-z.max())
    cases.append(dict(prompt=row["prompt"],route=ROUTES[z.argmax()],probability=float(p.max()/p.sum())))
dest=ROOT/"checks/golden.json"
dest.write_text(json.dumps(cases,indent=2))
