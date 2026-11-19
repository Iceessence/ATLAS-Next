import copy
import json
import unittest
from model_lab import inspect_ledger, validate_splits


class TestEligibility(unittest.TestCase):
    def rows(self):
        return {role:[dict(family=role, origin="observed_project_outcome",question=f"read {role}",
                           outcome_receipt={"sha256":role})] for role in ("train","validation","evaluation")}

    def test_distinct_and_pinned_split_shape(self):
        rows=self.rows()
        self.assertEqual(validate_splits(rows)["train"]["outcomes"],1)
        for key in ("family","question","outcome_receipt"):
            leaked=copy.deepcopy(rows)
            leaked["evaluation"][0][key]=leaked["train"][0][key]
            with self.subTest(key=key), self.assertRaisesRegex(ValueError,"leakage"):
                validate_splits(leaked)

    def test_synthetic_rows_refused(self):
        rows=self.rows()
        rows["train"][0]["origin"]="constructed_public_control"
        with self.assertRaisesRegex(ValueError,"observed"):
            validate_splits(rows)

    def test_conversation_labels_never_enrolled(self):
        sample=dict(eligibility={"independent_evaluation":True,"training":False,"private_qualification":False},
                    observations=[{"provenance":{"file":{"kind":"observed_project_source"}}}])
        self.assertEqual(inspect_ledger(json.dumps(sample)),1)
        sample["eligibility"]["training"]=True
        with self.assertRaisesRegex(ValueError,"permissions"):
            inspect_ledger(json.dumps(sample))


if __name__=="__main__":
    unittest.main()
