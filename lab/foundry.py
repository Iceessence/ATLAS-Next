"""Acquire and verify public project observations for the original-model lab."""
import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import re
from tempfile import TemporaryDirectory

from foundry_sources import FAMILIES, MODES, project, typed_request
from master_spec import installed
from native_data import ROOT, encode, family_key, normalized, observe, sha, write_new

DATA = ROOT / "out/next-021-025/foundry"
FROZEN = ROOT / "docs/evidence/next-021/foundry-manifest.json"
SCHEMA = "atlas.outcome_record.v1"
ORACLE = "distribution-metadata-ast-v1"
GRANT = "atlas-founder-public-local-v1"
SECTIONS = ("CH-015", "CH-039", "CH-071", "CH-161", "CH-172", "APPENDIX-S")
KEYS = {"schema_version", "outcome_id", "task_id", "task_request_sha256", "project_id", "project_family_id",
        "template_family_id", "author_id", "source_snapshots", "model_origin", "checkpoint_sha256",
        "action_proposed", "action_executed", "host_overrode_proposal", "verifier_id", "verifier_version",
        "evaluation_receipt_sha256", "outcome_class", "failure_taxonomy", "data_rights_class",
        "consent_grant_id", "eligible_for_training", "recorded_at_utc"}
# Freeze acquisition epoch in the public artifact. Host timestamps are retained only
# in disposable task logs; no clock input changes the stable dataset.
EPOCH = "2026-11-19T00:00:00Z"


def overlaps(prior, other):
    def shingles(value, n):
        words = re.findall(r"\w+", normalized(value))
        return {tuple(words[i:i+n]) for i in range(len(words)-n+1)}
    if shingles(prior, 13) & shingles(other, 13):
        return "LEAK_13GRAM"
    left, right = shingles(prior, 5), shingles(other, 5)
    if left and right and len(left & right) / len(left | right) >= .15:
        return "LEAK_NEAR_DUP"
    return None


def intake(profiles, tasks):
    valid, quarantine = [], []
    owners, prompts, templates, sources, identities = {}, {}, {}, {}, set()
    for task in tasks:
        profile = profiles[task["family"]]
        family, role, template = family_key(task["family"]), task["partition"], task["template_family"]
        identity = task["id"]
        request = normalized(task["question"])
        reason = None
        source_pins = [s["sha256"] for s in profile["sources"]]
        if (role not in FAMILIES or profile["partition"] != role or profile["consent_grant_id"] != GRANT
                or family not in FAMILIES[role] or task["oracle_id"] != ORACLE or task["kind"] not in MODES):
            reason = "UNENROLLED_OR_REVOKED"
        elif identity in identities or request in prompts:
            reason = "DUPLICATE_REQUEST"
        elif (owners.get(family, role) != role or templates.get(template, role) != role or
                any(sources.get(pin, role) != role for pin in source_pins)):
            reason = "LEAK_FAMILY_TEMPLATE_OR_SOURCE"
        else:
            reason = next((flag for q, part in prompts.items() if part != role
                           if (flag := overlaps(q, request))), None)
        if reason:
            quarantine.append(dict(task=task, reason=reason))
            continue
        valid.append(task)
        identities.add(identity)
        owners[family] = role
        templates[template] = role
        prompts[request] = role
        for pin in source_pins:
            sources[pin] = role
    return valid, quarantine


def source_snapshots(profile):
    return [{key: s[key] for key in ("source_id", "source_revision", "sha256")} for s in profile["sources"]]


def inputs(task, profile, question):
    return dict(id=task["id"], question=question, documents=[s["text"] for s in profile["sources"]],
                extensions=[s["suffix"] for s in profile["sources"]], expect=task["oracle"])


def classify(task, action, observation):
    delivered = observation["state"] == "completed"
    expected = task["oracle"]["state"] == "completed"
    if observation["useful"]:
        return ("CORRECT_DELIVERY" if delivered else "CORRECT_WITHHOLD"), "NONE"
    if delivered:
        return "INCORRECT_DELIVERY", "WRONG_SOURCE_SELECTION"
    if expected:
        return "FALSE_ABSTENTION", "UNSUPPORTED_HOST_GRAMMAR" if action == "raw" else "WRONG_SOURCE_SELECTION"
    return "UNSUPPORTED_SCOPE", "EVALUATOR_MISMATCH"


def candidate_key(index):
    return f"source:{index}"


def action_inputs(task, profile, action):
    if action == "raw":
        return task["question"], None
    if action == "withhold":
        return task["question"], -1
    index = int(action.removeprefix("source:"))
    if action != candidate_key(index) or not 0 <= index < len(profile["sources"]):
        raise ValueError("Invalid typed action")
    source = profile["sources"][index]
    mode = "read_fact" if source["suffix"] == ".md" else "locate_definition"
    return typed_request(mode, source["key"]), index


def record_for(task, profile, action, observation):
    effective, restriction = action_inputs(task, profile, action)
    request_sha = sha(task["question"].encode())
    receipt = dict(task_id=task["id"], task_request_sha256=request_sha, project_id=profile["family"],
                   effective_request_sha256=sha(effective.encode()), restriction=restriction,
                   source_snapshots=source_snapshots(profile), oracle_sha256=sha(encode(task["oracle"])),
                   observation=observation)
    outcome, failure = classify(task, action, observation)
    eligible = task["partition"] in ("train", "acquisition") and outcome in (
        "CORRECT_DELIVERY", "CORRECT_WITHHOLD", "FALSE_ABSTENTION", "INCORRECT_DELIVERY")
    ref = sha(encode(receipt))
    record = dict(schema_version=SCHEMA, outcome_id="out-"+ref[:40], task_id=task["id"],
                  task_request_sha256=request_sha, project_id=profile["family"], project_family_id=family_key(profile["family"]),
                  template_family_id=task["template_family"], author_id="atlas-public-audit-author",
                  source_snapshots=receipt["source_snapshots"], model_origin="DETERMINISTIC_0B", checkpoint_sha256=None,
                  action_proposed=action, action_executed=action, host_overrode_proposal=False,
                  verifier_id=ORACLE, verifier_version="1", evaluation_receipt_sha256=ref, outcome_class=outcome,
                  failure_taxonomy=failure, data_rights_class="SYNTHETIC_VERIFIED" if task["case_class"] ==
                  "authored_safety_control" else profile["rights"], consent_grant_id=profile["consent_grant_id"],
                  eligible_for_training=eligible, recorded_at_utc=EPOCH)
    return {"record": record, "receipt": receipt}


def validate_record(task, profile, envelope, *, revoked=frozenset(), active=None):
    if set(envelope) != {"record", "receipt"} or set(envelope["record"]) != KEYS:
        raise ValueError("Unexpected outcome record fields")
    record, receipt = envelope["record"], envelope["receipt"]
    if profile["consent_grant_id"] != GRANT or record["consent_grant_id"] in revoked:
        raise ValueError("Training consent revoked or missing")
    current = source_snapshots(profile)
    if any(sha(s["text"].encode()) != s["sha256"] for s in profile["sources"]) or active is not None and active != current:
        raise ValueError("Source missing or stale")
    observation = receipt["observation"]
    answer = task["oracle"]
    if observation["useful"]:
        if observation["failed"] or observation["state"] != answer["state"] or observation["verdict"] != answer["verdict"]:
            raise ValueError("Verified label disagrees with the independent oracle")
        if answer["state"] == "completed":
            source = current[answer["document"]]
            span = {key: answer[key] for key in ("line", "start", "length")}
            if (observation["selected"], observation["result"], observation["digest"], observation["citation"]) != (
                    answer["document"], answer["answer"], source["sha256"], span):
                raise ValueError("Claimed success lacks the exact oracle evidence")
    expected = record_for(task, profile, record["action_executed"], observation)
    if envelope != expected:
        raise ValueError("Request, source, outcome, or oracle receipt binding mismatch")
    return record


def measure_case(task, profile):
    actions = ("raw", "withhold", *(candidate_key(i) for i in range(len(profile["sources"]))))
    outcomes, costs, quarantine = {}, {}, []
    for action in actions:
        question, restriction = action_inputs(task, profile, action)
        try:
            actual, cost = observe(inputs(task, profile, question), restriction)
            result = record_for(task, profile, action, actual)
            validate_record(task, profile, result)
        except (ValueError, RuntimeError) as error:
            quarantine.append(dict(task_id=task["id"], action=action, reason="PROBE_UNAVAILABLE", detail=str(error)))
            continue
        outcomes[action] = result
        costs[action] = cost
    return dict(task=task, outcomes=outcomes), costs, quarantine


def knowledge(rows, profiles):
    table, witnessed = {}, {}
    for row in rows:
        task = row["task"]
        profile = profiles[task["family"]]
        expected = task["oracle"].get("document")
        if expected is None:
            continue
        ref = row["outcomes"].get(candidate_key(expected))
        if not ref or ref["record"]["outcome_class"] != "CORRECT_DELIVERY":
            continue
        source = profile["sources"][expected]
        card_id = "knowledge-" + sha(encode([source["source_id"], source["sha256"], task["kind"]]))[:24]
        table[card_id] = dict(id=card_id, kind=task["kind"], project_id=task["family"], trust="observed-public",
                              source=source["source_id"], revision=source["source_revision"], source_sha256=source["sha256"],
                              evidence=ref["receipt"]["observation"]["citation"], receipt=ref["record"]["evaluation_receipt_sha256"])
        if task["partition"] == "train":
            witnessed.setdefault(task["kind"], []).append(card_id)
    procedures = {mode: dict(id=mode+"-v1", trust="observed-public", version=1, effects="none",
                             source_suffix=".md" if mode == "read_fact" else ".py",
                             preconditions=["scope", "immutable-source", "owner-local"], witnesses=sorted(ids))
                  for mode, ids in witnessed.items() if len(ids) >= 2}
    return dict(schema="atlas.knowledge_projection.v1", cards=table, procedures=procedures)


def acquire():
    spec = installed()
    spec.consult(*SECTIONS)
    profiles, tasks = {}, []
    for role, members in FAMILIES.items():
        for family in members:
            profiles[family], group = project(role, family)
            tasks.extend(group)
    tasks, quarantine = intake(profiles, tasks)
    if DATA.exists():
        raise ValueError("Choose a fresh checkout or preserve the existing Foundry artifact")
    DATA.parent.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(prefix="foundry-", dir=DATA.parent) as temporary:
        staged = Path(temporary) / "data"
        protected = dict(held_external=True, accessible_to_training=False, manifest=None)
        manifest = dict(schema="atlas.foundry.v1", canonical_master_sha256=spec.sha256, sections_consulted=list(SECTIONS),
                        protected_evaluation=protected, external_inference_calls=0, partitions={})
        output = []
        with ThreadPoolExecutor(max_workers=2) as pool:
            for role in FAMILIES:
                group = [task for task in tasks if task["partition"] == role]
                rows, costs, classes, failures = [], {}, Counter(), Counter()
                for row, cost, failed in pool.map(lambda t: measure_case(t, profiles[t["family"]]), group):
                    rows.append(row)
                    costs[row["task"]["id"]] = cost
                    quarantine.extend(failed)
                    classes.update(e["record"]["outcome_class"] for e in row["outcomes"].values())
                    failures.update(e["record"]["failure_taxonomy"] for e in row["outcomes"].values())
                members = {t["family"] for t in group}
                payload = dict(rows=rows, profiles={k: profiles[k] for k in sorted(members)})
                write_new(staged / (role+".json"), payload)
                write_new(staged / (role+"-cost.json"), costs)
                manifest["partitions"][role] = dict(sha256=sha(encode(payload)), tasks=len(group), families=len(members),
                    observed_tasks=sum(t["case_class"] == "observed" for t in group),
                    actions=sum(classes.values()), classes=dict(classes), failures=dict(failures))
                output.extend(rows)
                print(role, manifest["partitions"][role], flush=True)
        if (manifest["partitions"]["train"]["families"] < 3 or manifest["partitions"]["evaluation"]["families"] < 2 or
                not manifest["partitions"]["train"]["classes"].get("CORRECT_DELIVERY", 0)):
            raise ValueError("Insufficient project-family diversity or verified positives")
        index = knowledge(output, profiles)
        write_new(staged / "knowledge.json", index)
        manifest["knowledge_sha256"] = sha(encode(index))
        write_new(staged / "quarantine.json", quarantine)
        manifest["quarantine"] = dict(Counter(item["reason"] for item in quarantine))
        write_new(staged / "manifest.json", manifest)
        staged.rename(DATA)


def load(role, revoked=frozenset()):
    if role not in FAMILIES:
        raise ValueError("Only author-public partitions are readable here")
    manifest = json.loads((DATA / "manifest.json").read_bytes())
    data = (DATA / (role+".json")).read_bytes()
    expected = json.loads(FROZEN.read_bytes()) if FROZEN.exists() else manifest
    if sha(data) != expected["partitions"][role]["sha256"] or manifest != expected:
        raise ValueError("Foundry partition or manifest digest mismatch")
    batch = json.loads(data)
    for row in batch["rows"]:
        task = row["task"]
        if task["partition"] != role:
            raise ValueError("Task assigned to the wrong partition")
        for env in row["outcomes"].values():
            validate_record(task, batch["profiles"][task["family"]], env, revoked=revoked)
    return batch


def load_knowledge():
    manifest = json.loads(FROZEN.read_bytes())
    data = (DATA / "knowledge.json").read_bytes()
    if sha(data) != manifest["knowledge_sha256"]:
        raise ValueError("Knowledge index differs from the frozen Foundry receipt")
    return json.loads(data)


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument("command", choices=("acquire", "report", "knowledge"))
    p.add_argument("--project")
    args = p.parse_args()
    if args.command == "acquire":
        acquire()
    elif args.command == "knowledge":
        index = load_knowledge()
        if args.project:
            index["cards"] = {i: row for i, row in index["cards"].items() if row["project_id"] == args.project}
        print(json.dumps(index, indent=2))
    else:
        print((DATA / "manifest.json").read_text(encoding="utf-8"))


if __name__ == "__main__":
    main()
