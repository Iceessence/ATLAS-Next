import copy
import json
import unittest
from master_spec import ROOT, VERSION, installed, validate


class MasterContractTests(unittest.TestCase):
    def test_pinned_edition_and_navigation(self):
        spec = installed()
        self.assertEqual(len(spec.entries), 301)
        self.assertIn("Internal agent constitution", spec.section("CH-015"))
        self.assertIn("CH-172", {e["id"] for e in spec.search("provenance learning chain")})
        self.assertIn("Nonfatal defects", spec.section("APPENDIX-V"))
        self.assertIn("CH-161", spec.consult("CH-161", "CH-172"))

    def test_changed_bytes_or_missing_navigation_refused(self):
        version = json.loads((ROOT / VERSION).read_bytes())
        md = (ROOT / version["canonical"]).read_bytes()
        index = json.loads((ROOT / version["index"]).read_bytes())
        with self.assertRaisesRegex(ValueError, "digest"):
            validate(md + b"x", index)
        tampered = copy.deepcopy(index)
        tampered["entries"] = [entry for entry in tampered["entries"] if entry["id"] != "CH-072"]
        with self.assertRaisesRegex(ValueError, "Missing"):
            validate(md, tampered)


if __name__ == "__main__":
    unittest.main()
