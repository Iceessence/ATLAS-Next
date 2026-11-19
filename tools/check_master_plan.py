#!/usr/bin/env python3
"""Verify the installed master and retrieve exact indexed policy sections."""
import argparse
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "lab"))
from master_spec import installed, validate


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("canonical", type=Path, nargs="?")
    parser.add_argument("index", type=Path, nargs="?")
    modes = parser.add_mutually_exclusive_group()
    modes.add_argument("--section")
    modes.add_argument("--search")
    args = parser.parse_args()
    if bool(args.canonical) != bool(args.index):
        parser.error("Supply both canonical and index, or neither for the installed edition")
    sys.stdout.reconfigure(encoding="utf-8")
    try:
        if args.canonical:
            spec = validate(args.canonical.read_bytes(), json.loads(args.index.read_bytes()))
        else:
            spec = installed()
        if args.section:
            print(spec.section(args.section), end="")
        elif args.search:
            for e in spec.search(args.search):
                print(f"{e['id']}: {e['title']} [{e['start_line']}-{e['end_line']}]")
        else:
            print(f"PASS {spec.edition} SHA-256 {spec.sha256}; {len(spec.entries)} indexed sections; "
                  "192 chapters, 23 parts, 23 appendices and 50 historical IDs.")
    except (KeyError, OSError, ValueError) as error:
        print(f"Master verification failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
