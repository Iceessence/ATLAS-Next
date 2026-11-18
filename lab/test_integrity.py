import json
import tempfile
import unittest
from pathlib import Path
from corpus import ROOT, ORIGINS, digest, features, rows

from outcomes import PUBLIC, load_suite, score, sha


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

    def test_outcome_suite_provenance_and_digest(self):
        cases, _ = load_suite(PUBLIC)
        self.assertTrue(all(c["origin"] == "author_fixture" for c in cases))
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            suite = root / "cases.json"
            manifest = root / "manifest.json"
            for origin in ("model_label", "observed_project_outcome"):
                case = dict(cases[0], origin=origin, outcome_receipt={"file":"result.txt","sha256":"0"*64})
                (root / "result.txt").write_text("fixture")
                suite.write_text(json.dumps([case]))
                manifest.write_text(json.dumps({"schema":1,"file":suite.name,"sha256":sha(suite.read_bytes())}))
                with self.assertRaises(ValueError):
                    load_suite(manifest)
            suite.write_text("[]")
            with self.assertRaises(ValueError):
                load_suite(manifest)

    def test_correct_text_with_wrong_span_or_verdict_fails_outcome(self):
        case = load_suite(PUBLIC)[0][0]
        expected = case["expect"]
        work = {"state":"completed", "result":expected["answer"], "sourceIntegrity":"passed",
                "answerRelevance":"supported", "proposal":{"qualification":"shadow"},
                "researchReview":{"verdict":"unsupported"},
                "evidence":{"id":"doc-test","version":1,"digest":sha(case["documents"][0].encode()),
                            "span":{"path":"source-0.md","line":expected["line"],"length":expected["length"],"start":0}}}
        result = score(case, {"answer":work, "ids":["doc-test"]})
        self.assertTrue(result["answer"])
        self.assertFalse(result["span"])
        self.assertFalse(result["verdict"])

    def test_checkpoint_and_lineage(self):
        manifest=json.loads((ROOT/"assets/native.manifest.json").read_text())
        self.assertEqual(digest(ROOT/"assets/native.json"),manifest["checkpoint_sha256"])
        self.assertEqual(digest(ROOT/"lab/train.json"),manifest["train_sha256"])
        self.assertEqual(digest(ROOT/"lab/validation.json"),manifest["validation_sha256"])
        self.assertEqual("shadow",manifest["qualification"])
        self.assertEqual(len(features("hello")),256)

if __name__=="__main__": unittest.main()
