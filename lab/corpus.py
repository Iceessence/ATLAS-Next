"""Public project fixtures. Labels derive from the declared intent of each template."""
import hashlib
import json
import re
from pathlib import Path

ROUTES = ("fact", "procedure", "clarify")
ORIGINS = {"observed", "human_label", "rule_label", "model_label", "simulation", "independently_verified"}
WIDTH = 256
ROOT = Path(__file__).resolve().parents[1]

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def tokens(text):
    return re.findall("[a-z0-9]+", text.lower())

def features(text):
    vector = [0.] * WIDTH
    for word in tokens(text):
        h = 2166136261
        for byte in word.encode("ascii"):
            h = ((h ^ byte) * 16777619) & 0xffffffff
        vector[h % WIDTH] = 1.
    length = sum(vector) ** .5 or 1.
    return [x / length for x in vector]

def rows(path):
    spec = json.loads(path.read_text())
    if spec["origin"] not in ORIGINS or spec["origin"] != "rule_label":
        raise ValueError("Only declared rule-labeled public templates are supported")
    if spec["rights"] != "ATLAS-original synthetic fixture" or set(spec["templates"]) != set(ROUTES):
        raise ValueError("Unexpected rights or route set")
    return [dict(prompt=t.format(topic=p), label=r, family=p, origin=spec["origin"],
                 source_sha256=digest(path), template_id=f"{r}-{i}", rights=spec["rights"])
            for p in spec["projects"] for r in ROUTES for i,t in enumerate(spec["templates"][r])]

def rule(text):
    words=set(tokens(text))
    if words & {"how","steps","procedure","restore","rebuild","guide","instructions","recover","walkthrough","configure"}:
        return "procedure"
    if words & {"what","which","limit","policy","duration","where","size","version","when","retention","default"}:
        return "fact"
    return "clarify"
