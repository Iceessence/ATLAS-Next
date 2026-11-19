"""Read-only startup and retrieval contract for the pinned ATLAS product specification."""
from dataclasses import dataclass
import csv
import hashlib
import io
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
VERSION = "docs/ai/ATLAS_MASTER_PLAN_VERSION.json"
CHAPTERS = {f"CH-{n:03}" for n in range(1, 193)}
PARTS = {"PART-"+p for p in "I II III IV V VI VII VIII IX X XI XII XIII XIV XV XVI XVII XVIII XIX XX XXI XXII XXIII".split()}
APPENDICES = {"APPENDIX-"+c for c in "ABCDEFGHIJKLMNOPQRSTUVW"}
REQUIREMENTS = {f"ID-{n:02}" for n in range(1, 51)} | {f"V33-{n}" for n in range(167, 193)}
REQUIRED_FILES = {"docs/ai/ATLAS_MASTER_PLAN.md", "indexes/ATLAS_MASTER_INDEX.json",
                  "indexes/ATLAS_REQUIREMENTS_TRACE.csv", "indexes/ATLAS_REQUIREMENTS_TRACE.json",
                  "docs/ai/archive/master-plan/v3.3-transfer-sha256.json"}


def digest(data):
    return hashlib.sha256(data).hexdigest()


@dataclass(frozen=True)
class MasterSpec:
    sha256: str
    edition: str
    entries: dict
    lines: tuple[str, ...]

    def section(self, ident):
        e = self.entries[ident]
        return "".join(self.lines[e["start_line"]-1:e["end_line"]])

    def search(self, query):
        words = query.casefold().split()
        return [e for e in self.entries.values() if words and all(
            word in " ".join([e["title"], *e["tags"]]).casefold() for word in words)]

    def consult(self, *ids):
        # Materialize the requested passages from the validated bytes and preserve
        # their identities. Policy cannot confer capability grants.
        return {ident: self.section(ident) for ident in ids}


def validate(md, index):
    text = md.decode("utf-8")
    lines = tuple(text.splitlines(keepends=True))
    if (digest(md), len(lines), len(text.split())) != (index["sha256"], index["total_lines"], index["total_words"]):
        raise ValueError("Master digest, line count, or word count mismatch")
    entries, covered, previous = {}, set(), 0
    for e in index["entries"]:
        ident, start, end = e["id"], e["start_line"], e["end_line"]
        if ident in entries or not previous < start <= end <= len(lines):
            raise ValueError(f"Repeated, unordered, or invalid section {ident}")
        previous = start
        section = "".join(lines[start-1:end])
        if (lines[start-1].rstrip("\r\n") != "#" * e["level"] + " " + e["title"]
                or digest(section.encode()) != e["sha256"]
                or len(section.split()) != e["word_count"]):
            raise ValueError(f"Section content mismatch for {ident}")
        covered.update(range(start, end+1))
        entries[ident] = e
    if not CHAPTERS | PARTS | APPENDICES <= entries.keys() or len(covered) != len(lines):
        raise ValueError("Missing chapter, part, appendix or index coverage")
    present = set(re.findall(r"\bID-\d{2}\b", text))
    if not {req for req in REQUIREMENTS if req.startswith("ID-")} <= present:
        raise ValueError("Historical requirement missing from canonical plan")
    return MasterSpec(index["sha256"], index["edition"], entries, lines)


def installed(root=ROOT):
    version = json.loads((root / VERSION).read_bytes())
    if (version["schema"], version["canonical"], version["index"]) != (
            "atlas.master_version.v1", "docs/ai/ATLAS_MASTER_PLAN.md", "indexes/ATLAS_MASTER_INDEX.json"):
        raise ValueError("Unrecognized master version authority")
    if set(version["files"]) != REQUIRED_FILES:
        raise ValueError("Unexpected edition manifest files")
    files = {name: (root / name).read_bytes() for name in sorted(REQUIRED_FILES)}
    for name, data in files.items():
        if digest(data) != version["files"][name]:
            raise ValueError(f"Installed version mismatch for {name}")
    spec = validate(files[version["canonical"]], json.loads(files[version["index"]]))
    if spec.edition != version["edition"]:
        raise ValueError("Installed edition mismatch")
    historical = csv.DictReader(io.StringIO(files["indexes/ATLAS_REQUIREMENTS_TRACE.csv"].decode("utf-8-sig")))
    ids = [row["requirement_id"] for row in historical]
    if len(ids) != len(REQUIREMENTS) or set(ids) != REQUIREMENTS:
        raise ValueError("Requirements trace differs from prescribed IDs")
    return spec
