import tempfile
import unittest
from pathlib import Path

from atlas_language_review import profile_for
from atlas_refit import paired
from master_spec import ROOT
from native_review import protected_paths, validate_custody


class RefitReviewChecks(unittest.TestCase):
    def test_paired_outcome_regression_and_bounded_identity(self):
        before = {"rows":[dict(id="a", action="source:1", outcome="CORRECT_DELIVERY")]}
        rejected = {"rows":[dict(id="a", action="source:0", outcome="INCORRECT_DELIVERY")]}
        evidence = paired(before, rejected)
        self.assertEqual(evidence["regressions"], ["a"])
        self.assertEqual(evidence["new_wrong"], ["a"])
        self.assertEqual(evidence["changed_actions"], 1)
        with self.assertRaisesRegex(ValueError, "identities"):
            paired(before, {"rows":[dict(id="b")]})

    def test_separate_path_custody_and_mixed_extensions(self):
        with tempfile.TemporaryDirectory() as temp:
            external = Path(temp).resolve()
            protected_paths(external / "suite.json", external / "report.json")
            with self.assertRaisesRegex(ValueError, "outside"):
                protected_paths(external / "suite.json", ROOT / "result.json")
        case = dict(id="test", family="author-test", question="Locate probe", origin="custodian_fixture",
                    documents=["def probe(): ..."], keys=["probe"], extensions=[".py"],
                    expect={"state":"completed","document":0})
        validate_custody([case], [], allowed_extensions=(".md",".py"))
        with self.assertRaisesRegex(ValueError, "extension"):
            validate_custody([case], [])
        self.assertEqual(profile_for(case)["sources"][0]["key"], "probe")
        with self.assertRaisesRegex(ValueError, "explicit keys"):
            profile_for(dict(case, keys=[]))


if __name__ == "__main__":
    unittest.main()
