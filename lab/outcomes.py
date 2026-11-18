"""Score full desktop-engine outcomes. The probe only receives questions and source text."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time

ROOT = Path(__file__).resolve().parents[1]
PUBLIC = ROOT / "lab" / "outcomes.manifest.json"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def load_suite(path):
    manifest = json.loads(path.read_text(encoding="utf-8"))
    if manifest["schema"] != 1 or Path(manifest["file"]).name != manifest["file"]:
        raise ValueError("Expected a sibling suite file and schema 1")
    suite = path.parent / manifest["file"]
    if suite.resolve().parent != path.parent or suite.is_symlink():
        raise ValueError("Suite must be held alongside its manifest")
    data = suite.read_bytes()
    if sha(data) != manifest["sha256"]:
        raise ValueError("Suite digest mismatch")
    cases = json.loads(data)
    ids = [case["id"] for case in cases]
    if not ids or len(ids) != len(set(ids)):
        raise ValueError("Suite needs unique case IDs")
    allowed = {"author_fixture", "custodian_fixture", "observed_project_outcome"}
    for case in cases:
        origin = case["origin"]
        if origin not in allowed:
            raise ValueError("Unknown outcome provenance")
        if origin == "observed_project_outcome":
            receipt = case["outcome_receipt"]
            leaf = receipt["file"]
            if Path(leaf).name != leaf:
                raise ValueError("Expected a sibling outcome receipt")
            evidence = path.parent / leaf
            if evidence.is_symlink() or sha(evidence.read_bytes()) != receipt["sha256"]:
                raise ValueError("Outcome receipt digest mismatch")
    return cases, manifest["sha256"]


def score(case, run):
    work, expect = run["answer"], case["expect"]
    review = work.get("researchReview") or {}
    checks = {"state": work["state"] == expect["state"],
              "verdict": review.get("verdict") == expect["verdict"],
              "advisory": (work.get("proposal") or {}).get("qualification") == "shadow"}
    if expect["state"] == "completed":
        cite = work.get("evidence") or {}
        checks.update(integrity=work["sourceIntegrity"] == "passed",
                      relevance=work["answerRelevance"] == "supported",
                      answer=work["result"] == expect["answer"])
        if "document" in expect:
            allowed = [expect["document"], *expect.get("equivalent_documents", [])]
            idx = next((i for i in allowed if run["ids"][i] == cite.get("id")), expect["document"])
            source = case["documents"][idx]
            extension = case["extensions"][idx] if "extensions" in case else ".md"
            anchor = cite.get("span") or {}
            start, length = expect["start"], expect["length"]
            fragment = source.encode("utf-16-le")[2*start:2*(start+length)].decode("utf-16-le")
            checks.update(document=cite.get("id") == run["ids"][idx],
                          version=cite.get("version") == 1,
                          digest=cite.get("digest") == sha(source.encode("utf-8")),
                          path=Path(anchor.get("path", "")).name == f"source-{idx}{extension}",
                          span=(anchor.get("line"), anchor.get("start"), anchor.get("length")) ==
                               (expect["line"], start, length),
                          extract=fragment == work["result"])
        else:
            checks["record"] = cite.get("id") == expect["record"]
    else:
        checks["withheld"] = work["answerRelevance"] != "supported" and work["state"] != "completed"
        if "sources" in expect:
            checks["source_count"] = len(review.get("sources", [])) == expect["sources"]
    return checks


def probe(question, documents, scratch_dir=None, proposed_document=None, extensions=None):
    """Exercise the real host. A proposal may only narrow the available Primary documents."""
    exe = ROOT / "checks/bin/Release/net10.0-windows/AtlasNext.Checks.exe"
    payload = {"question": question, "documents": documents, "proposedDocument": proposed_document, "extensions": extensions}
    scratch = str(scratch_dir or Path(tempfile.gettempdir()).resolve())
    # The owned revision includes two full digests. On Windows nested custodian
    # folders can exceed MAX_PATH during Python's recursive temporary cleanup.
    extended = "\\\\?\\"
    if os.name == "nt" and not scratch.startswith("\\\\"):
        scratch = extended + scratch
    with tempfile.TemporaryDirectory(dir=scratch) as workspace:
        work_path = workspace.removeprefix(extended) if os.name == "nt" else workspace
        run = subprocess.run([str(exe), "--outcome"], capture_output=True, encoding="utf-8",
                             input=json.dumps(payload),
                             env={**os.environ, "ATLAS_OUTCOME_WORK": work_path}, timeout=60)
    return run


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=PUBLIC)
    parser.add_argument("--report", type=Path)
    args = parser.parse_args()
    manifest = args.manifest.resolve()
    external = manifest != PUBLIC
    report = args.report.resolve() if args.report else ROOT / "out" / "outcomes.json"
    if external and (manifest.is_relative_to(ROOT) or report.is_relative_to(ROOT)):
        parser.error("Custodian supplies both manifest and report outside the checkout")
    if report == manifest or report.exists() and external:
        parser.error("Use a fresh external report path")
    cases, digest = load_suite(manifest)
    trials = []
    for case in cases:
        started = time.perf_counter()
        run = probe(case["question"], case["documents"], manifest.parent if external else None, extensions=case.get("extensions"))
        if run.returncode:
            trials.append(dict(id=case["id"], passed=False, origin=case["origin"], process_ms=1000*(time.perf_counter()-started), error=run.stderr[-1500:]))
            continue
        result = json.loads(run.stdout)
        checks = score(case, result)
        trials.append(dict(id=case["id"], passed=all(checks.values()), checks=checks,
                           origin=case["origin"], outcome_receipt=case.get("outcome_receipt"),
                           state=result["answer"]["state"], expected=case["expect"],
                           result=result["answer"]["result"], evidence=result["answer"].get("evidence"),
                           research=result["answer"].get("researchReview"), engine_us=result["elapsedUs"],
                           process_ms=1000*(time.perf_counter()-started), working_set_bytes=result["workingSetBytes"]))
    passed = sum(t["passed"] for t in trials)
    report.parent.mkdir(parents=True, exist_ok=True)
    report.write_text(json.dumps(dict(suite_sha256=digest, scope="external" if external else "author-public",
                                      passed=passed, total=len(trials), trials=trials), indent=2), encoding="utf-8")
    print(f"{passed}/{len(trials)} end-to-end outcomes passed. Report: {report}")
    return int(passed != len(trials))


if __name__ == "__main__":
    raise SystemExit(main())
