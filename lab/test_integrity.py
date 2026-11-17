import json
import tempfile
import unittest
from pathlib import Path
from corpus import ROOT, ORIGINS, digest, features, rows

class Integrity(unittest.TestCase):
    def test_unsupported_provenance_is_rejected(self):
        spec=json.loads((ROOT/"lab/train.json").read_text())
        with tempfile.TemporaryDirectory() as work:
            path=Path(work)/"corpus.json"
            for origin in (ORIGINS-{"rule_label"})|{"unclassified"}:
                spec["origin"]=origin
                path.write_text(json.dumps(spec))
                with self.subTest(origin=origin), self.assertRaises(ValueError):
                    rows(path)

    def test_split_identity(self):
        groups=[]
        shapes=[]
        for name in ("train","validation","holdout"):
            dataset=rows(ROOT/"lab"/f"{name}.json")
            self.assertTrue(all(row["origin"]=="rule_label" and row["source_sha256"] for row in dataset))
            groups.append({row["family"] for row in dataset})
            shapes.append({row["prompt"].replace(row["family"],"{topic}") for row in dataset})
        for partition in (groups,shapes):
            for i,group in enumerate(partition):
                for other in partition[i+1:]:
                    self.assertFalse(group & other)

    def test_checkpoint_and_lineage(self):
        manifest=json.loads((ROOT/"assets/native.manifest.json").read_text())
        self.assertEqual(digest(ROOT/"assets/native.json"),manifest["checkpoint_sha256"])
        self.assertEqual(digest(ROOT/"lab/train.json"),manifest["train_sha256"])
        self.assertEqual(digest(ROOT/"lab/validation.json"),manifest["validation_sha256"])
        self.assertEqual("shadow",manifest["qualification"])
        self.assertEqual(len(features("hello")),256)

if __name__=="__main__": unittest.main()
