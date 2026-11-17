"""A deterministic exemplar retrieval control limited to the training corpus."""
from corpus import tokens

def exemplar_router(training):
    subjects={r["family"] for r in training}
    examples=[(set(tokens(r["prompt"]))-subjects,r["label"]) for r in training]
    def nearest(prompt):
        words=set(tokens(prompt))
        scores={}
        for example,label in examples:
            score=len(words & example)/len(words | example)
            scores[label]=max(scores.get(label,0),score)
        best=max(scores.values())
        winners=[label for label,score in scores.items() if score==best]
        return winners[0] if best>0 and len(winners)==1 else "clarify"
    return nearest
