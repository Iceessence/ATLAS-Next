"""Public author measurements only. The fit entry point never takes a suite path."""
import json
import random
from project_audit import DATA, PROJECTS, digest


def verified_file(split, name, pins):
    path = DATA / split / name
    if path.is_symlink() or path.resolve().parent != (DATA / split).resolve():
        raise ValueError("Receipt outside public split")
    data = path.read_bytes()
    if digest(data) != pins[name]:
        raise ValueError(f"Public split digest mismatch: {name}")
    return json.loads(data)


def validate_receipt(receipt, family):
    if (receipt["origin"], receipt["custody"], receipt["family"]) != ("observed_project_outcome", "author-public", family):
        raise ValueError("Ineligible project observation")
    if any(len(receipt[key]) != 64 for key in ("metadata_sha256","wheel_sha256","record_sha256","implementation_sha256")):
        raise ValueError("Incomplete source provenance")


def cases(split):
    families = PROJECTS[split]
    pins = json.loads((DATA / split / "pins.json").read_bytes())
    if set(pins) != {"requests.json", *(name+".json" for name in families)}:
        raise ValueError("Unexpected split member")
    requests = verified_file(split, "requests.json", pins)
    if set(requests) != set(families):
        raise ValueError("Wrong project families")
    result = []
    for family in families:
        receipt = verified_file(split, family+".json", pins)
        validate_receipt(receipt, family)
        for n, (question, field) in enumerate(requests[family]):
            groups = list(receipt["groups"].items())
            random.Random(f"{family}/{n}").shuffle(groups)
            docs = []
            expected = None
            for title, fields in groups:
                lines = [f"# {family} — {title}"]
                for key, value in fields.items():
                    if key == field:
                        start = len(("\n".join(lines)+"\n"+key+": ").encode("utf-16-le"))//2
                        expected = dict(state="completed", verdict="supported", document=len(docs),
                                        line=len(lines)+1, start=start, length=len(value.encode("utf-16-le"))//2, answer=value)
                    lines.append(key+": "+value)
                docs.append("\n".join(lines))
            if expected is None:
                raise ValueError(f"Unmeasured field: {field}")
            result.append(dict(id=f"{family}-{n+1}",family=family,origin=receipt["origin"],
                               outcome_receipt=dict(file=family+".json",sha256=pins[family+".json"]),
                               question=question, documents=docs, expect=expected))
    return result
