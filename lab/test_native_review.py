import tempfile
import unittest
from pathlib import Path

from native_improvement import compare
from native_review import protected_paths, validate_custody
from outcomes import ROOT


class ReviewChecks(unittest.TestCase):
    def test_evaluator_custody_excludes_checkout(self):
        with tempfile.TemporaryDirectory() as scratch:
            external = Path(scratch).resolve()
            protected_paths(external / "suite.json", external / "report.json")
            with self.assertRaisesRegex(ValueError, "outside"):
                protected_paths(ROOT / "suite.json", external / "report.json")
            with self.assertRaisesRegex(ValueError, "fresh"):
                protected_paths(external / "suite.json", external)

    def test_custody_rejects_family_prompt_and_source_leaks(self):
        public = dict(id="public", family="python-dateutil", question="What is the retry policy?",
                      documents=["Retry policy: backoff"])
        example = dict(id="evaluator-fixture", family="new-test", question="What is the region?",
                       documents=["Region: local"], origin="custodian_fixture", expect={"state": "completed", "document": 0})
        validate_custody([example], [public])
        for key, value in (("family", "python_dateutil"), ("question", "  WHAT IS THE RETRY POLICY? ")):
            with self.assertRaisesRegex(ValueError, "overlaps"):
                validate_custody([dict(example, **{key: value})], [public])
        with self.assertRaisesRegex(ValueError, "source overlaps"):
            validate_custody([dict(example, documents=public["documents"])], [public])

    def test_rejects_lost_completion_and_wrong_delivery(self):
        def policy(useful, state):
            return dict(summary=dict(useful=int(useful), wrong=int(state == "completed" and not useful)),
                        trials=[dict(id="paired", useful=useful, state=state)])
        verified = policy(True, "completed")
        wrong = policy(False, "completed")
        original = dict(native=verified, host=verified)
        candidate = dict(native=wrong, linear=verified)
        result = compare(original, candidate)
        self.assertEqual(result["disposition"], "rejected")
        self.assertEqual(result["regressions"], ["paired"])
        self.assertEqual(result["new_wrong"], ["paired"])


if __name__ == "__main__":
    unittest.main()
