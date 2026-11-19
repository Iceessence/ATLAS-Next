"""Evaluator-only scoring. Input and output stay outside the author checkout."""
import argparse
from pathlib import Path

from native_data import ROLES, family_key, normalized, observe, public_cases, write_new
from native_improvement import compare
from native_master import gate_action
from native_models import SPECIALIST, collect, load_model, specialist_action
from native_task import SPECIALIST_SHA
from outcomes import ROOT, load_suite, sha


def protected_paths(manifest, report):
    for path in (manifest, report):
        if path.is_relative_to(ROOT) or path == ROOT:
            raise ValueError("Evaluator manifest and report must stay outside the checkout")
    if report.exists():
        raise ValueError("Choose a fresh evaluator report")


def validate_custody(cases, public, allowed_extensions=(".md",)):
    public_families = {family_key(case["family"]) for case in public}
    public_ids = {case["id"] for case in public}
    public_questions = {normalized(case["question"]) for case in public}
    public_docs = {sha(normalized(doc).encode()) for case in public for doc in case["documents"]}
    seen_questions = set()
    for case in cases:
        family = family_key(case.get("family"))
        question = normalized(case["question"])
        docs = case["documents"]
        if case["origin"] not in ("custodian_fixture", "observed_project_outcome"):
            raise ValueError("Unenrolled evaluator origin")
        if family in public_families or case["id"] in public_ids or question in public_questions:
            raise ValueError("Evaluator case overlaps author development")
        if not question or question in seen_questions or not 1 <= len(docs) <= 32:
            raise ValueError("Empty or repeated evaluator request, or invalid source count")
        seen_questions.add(question)
        if any(sha(normalized(doc).encode()) in public_docs for doc in docs):
            raise ValueError("Evaluator source overlaps author development")
        extensions = case.get("extensions")
        if extensions is None:
            extensions = [".md"] * len(docs)
        if not isinstance(extensions, list) or len(extensions) != len(docs) or any(
                ext not in allowed_extensions for ext in extensions):
            raise ValueError("Unexpected source extension in this evaluator suite")
        expected = case["expect"]
        if expected["state"] == "completed":
            doc_indices = [expected.get("document"), *expected.get("equivalent_documents", [])]
            if any(type(index) is not int or not 0 <= index < len(docs) for index in doc_indices):
                raise ValueError("Expected authorized evidence document for completion")
        elif expected["state"] != "needs clarification":
            raise ValueError("Expected verified completion or withholding")


def evaluate(cases, previous, candidate, scratch, master=None):
    # Predictions are made only with the question and the authorized source texts.
    designs = {}
    for role, bundle in (("previous", previous), ("candidate", candidate)):
        designs[role] = {}
        for policy in ("host", "tfidf", "linear", "native"):
            model = bundle["policies"].get(policy)
            designs[role][policy] = lambda row, policy=policy, model=model, idf=bundle["idf"]: specialist_action(policy, row, idf, model)
    if master is not None:
        designs["master"] = {
            "confidence_gate": lambda row: gate_action(row, previous, confidence=previous["policies"]["native"]["threshold"]),
            "linear_gate": lambda row: gate_action(row, previous, master["policies"]["linear"]),
            "original_master": lambda row: gate_action(row, previous, master["policies"]["native"]),
        }
    replay, costs = {}, {}
    for row in cases:
        inputs = {k: row[k] for k in ("question", "documents")}
        actions = {pick(inputs)[0] for design in designs.values() for pick in design.values()}
        trials, timings = {}, {}
        for action in sorted(actions, key=str):
            trials[str(action)], timings[str(action)] = observe(row, action, scratch)
        replay[row["id"]], costs[row["id"]] = trials, timings
    return {role: {policy: collect(cases, pick, replay, costs) for policy, pick in design.items()}
            for role, design in designs.items()}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--candidate", required=True, type=Path)
    parser.add_argument("--candidate-sha", required=True)
    parser.add_argument("--master", type=Path, help="Optional frozen Primary Master for the previous specialist")
    parser.add_argument("--master-sha")
    args = parser.parse_args()
    if bool(args.master) != bool(args.master_sha):
        parser.error("Supply both --master and --master-sha")
    manifest, report = args.manifest.resolve(), args.report.resolve()
    protected_paths(manifest, report)
    cases, digest = load_suite(manifest)
    validate_custody(cases, [case for role in ROLES for case in public_cases(role)])
    previous, previous_sha = load_model(SPECIALIST, "source specialist", SPECIALIST_SHA)
    candidate, candidate_sha = load_model(args.candidate, "source specialist", args.candidate_sha.lower())
    master, master_sha = (None, None)
    if args.master:
        master, master_sha = load_model(args.master, "Primary Master", args.master_sha.lower())
        if master["specialist_sha256"] != previous_sha:
            raise ValueError("Master belongs to a different specialist")
    results = evaluate(cases, previous, candidate, manifest.parent, master)
    decision = compare(results["previous"], results["candidate"])
    write_new(report, dict(mode="evaluator custody", manifest_sha256=sha(manifest.read_bytes()),
                           suite_sha256=digest, previous_sha256=previous_sha, candidate_sha256=candidate_sha,
                           master_sha256=master_sha,
                           outcomes=len(cases), results=results, diagnostic=decision,
                           promotion="separate independent verdict and owner authorization required"))
    print(f"Scored {len(cases)} evaluator-held cases. Review the external report.")


if __name__ == "__main__":
    main()
