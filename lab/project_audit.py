"""Replayable audit of public installed distributions. Reads only named distribution files."""
import argparse
import ast
import base64
import csv
import hashlib
import io
import json
from email.parser import Parser
from importlib.metadata import distribution
from pathlib import Path

DATA = Path(__file__).resolve().parent / "project_data"
# Upstream projects are the partition unit. The same family never crosses a boundary.
PROJECTS = {
    "train": {
        "numpy": ("numpy/lib/_npyio_impl.py", "savez_compressed", "Compressed archive writer"),
        "pillow": ("PIL/Image.py", "Image.resize", "Image resize"),
        "pypdf": ("pypdf/_reader.py", "PdfReader.__init__", "PDF reader constructor"),
        "packaging": ("packaging/version.py", "Version.__init__", "Version parser constructor"),
    },
    "validation": {
        "python-docx": ("docx/api.py", "Document", "Document opener"),
        "lxml": ("lxml/cssselect.py", "CSSSelector.__init__", "CSS selector constructor"),
    },
    "evaluation": {
        "openpyxl": ("openpyxl/reader/excel.py", "load_workbook", "Workbook loader"),
        "pydantic": ("pydantic/main.py", "BaseModel.model_dump", "Model export"),
        "python-pptx": ("pptx/api.py", "Presentation", "Presentation opener"),
        "xlsxwriter": ("xlsxwriter/workbook.py", "Workbook.__init__", "Workbook constructor"),
    },
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def inspect(family, spec):
    dist = distribution(family)
    root = dist.locate_file("").resolve()
    entries = list(dist.files)
    record = next(p for p in entries if p.name == "RECORD" and p.parent.name.endswith(".dist-info"))
    def read(relative):
        path = (root / relative).resolve()
        if not path.is_relative_to(root):
            raise ValueError("Source outside installed distribution root")
        return path.read_bytes()

    record_bytes = read(record)
    metadata_bytes = read(record.parent / "METADATA")
    wheel_bytes = read(record.parent / "WHEEL")
    wheel = Parser().parsestr(wheel_bytes.decode())
    metadata = Parser().parsestr(metadata_bytes.decode())
    counts = {"checked": 0, "mismatched": 0, "missing": 0, "excluded": 0}
    disagreements = []
    for name, declared, _ in csv.reader(io.StringIO(record_bytes.decode("utf-8"))):
        path = (root / name).resolve()
        if not path.is_relative_to(root) or not declared.startswith("sha256="):
            counts["excluded"] += 1
            continue
        if not path.is_file():
            counts["missing"] += 1
            disagreements.append([name, "missing"])
            continue
        actual = hashlib.sha256(path.read_bytes()).digest()
        counts["checked"] += 1
        if base64.urlsafe_b64encode(actual).decode().rstrip("=") != declared[7:]:
            counts["mismatched"] += 1
            disagreements.append([name, actual.hex()])

    file, symbol, label = spec
    source = read(file)
    node = ast.parse(source)
    for part in symbol.split("."):
        node = next(n for n in node.body if getattr(n, "name", None) == part)
    if not isinstance(node, (ast.AsyncFunctionDef, ast.FunctionDef)):
        raise ValueError(symbol)
    args = node.args
    parameters = [a.arg for a in args.posonlyargs + args.args]
    if args.vararg:
        parameters.append("*" + args.vararg.arg)
    parameters.extend(a.arg for a in args.kwonlyargs)
    if args.kwarg:
        parameters.append("**" + args.kwarg.arg)
    groups = {
        "Release metadata": {"Published name": metadata["Name"], "Release version": dist.version,
                             "Declared dependency count": str(len(metadata.get_all("Requires-Dist", []))),
                             "Python requirement": metadata["Requires-Python"] or "not declared"},
        "Wheel configuration": {"Wheel generator": wheel["Generator"], "Wheel compatibility tags": ", ".join(wheel.get_all("Tag")),
                                "Pure library flag": wheel["Root-Is-Purelib"]},
        "Installed payload audit": {"Restore manifest digest": digest(record_bytes), "Checked payload files": str(counts["checked"]),
                                    "Payload hash mismatches": str(counts["mismatched"]), "Missing payload files": str(counts["missing"])},
        "Interface inspection": {label+" parameters": ", ".join(parameters), label+" declaration line": str(node.lineno),
                                 label+" source digest": digest(source)},
    }
    if any(not v for g in groups.values() for v in g.values()):
        raise ValueError(f"Missing observed value in {family}")
    return dict(origin="observed_project_outcome", custody="author-public", family=family, version=dist.version,
                record_path=str(record).replace("\\","/"), record_sha256=digest(record_bytes),
                metadata_sha256=digest(metadata_bytes), wheel_sha256=digest(wheel_bytes),
                implementation=file, symbol=symbol, implementation_sha256=digest(source),
                counts=counts, disagreements=disagreements, groups=groups)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--capture", action="store_true", help="Create absent receipts. Existing receipts must match.")
    args = parser.parse_args()
    for split, families in PROJECTS.items():
        for family, spec in families.items():
            actual = inspect(family, spec)
            path = DATA / split / (family+".json")
            if args.capture and not path.exists():
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes((json.dumps(actual, indent=2)+"\n").encode())
            if json.loads(path.read_bytes()) != actual:
                raise ValueError(f"{family} differs from the archived observation")
            print(f"{split} {family} {actual['version']} checked={actual['counts']['checked']} mismatches={actual['counts']['mismatched']}")


if __name__ == "__main__":
    main()
