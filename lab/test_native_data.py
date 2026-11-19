import copy
import json
from pathlib import Path
from tempfile import TemporaryDirectory
import unittest
from types import SimpleNamespace
from unittest.mock import patch

import native_data as data


class EligibilityChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.splits = {role: data.public_cases(role) for role in data.ROLES}

    def test_author_inventory(self):
        data.validate_cases(self.splits)
        self.assertEqual({role: len(rows) for role, rows in self.splits.items()},
                         {"train": 24, "validation": 8, "evaluation": 20, "acquisition": 8})

    def test_family_overlap(self):
        rows = copy.deepcopy(self.splits)
        rows["evaluation"][0]["family"] = rows["train"][0]["family"].upper()
        with self.assertRaisesRegex(ValueError, "overlaps"):
            data.validate_cases(rows)

    def test_family_separator_alias_does_not_cross_partitions(self):
        rows = copy.deepcopy(self.splits)
        rows["evaluation"][0]["family"] = "python_dateutil"
        with self.assertRaisesRegex(ValueError, "overlaps"):
            data.validate_cases(rows)

    def test_prompt_duplicate_and_fixture_rejected(self):
        rows = copy.deepcopy(self.splits)
        rows["evaluation"][0]["question"] = " " + rows["train"][0]["question"].upper()
        with self.assertRaisesRegex(ValueError, "Duplicate request"):
            data.validate_cases(rows)
        rows["evaluation"][0]["origin"] = "author_fixture"
        with self.assertRaisesRegex(ValueError, "Non-observed"):
            data.validate_cases(rows)

    def test_cross_project_source_duplicate_rejected(self):
        rows = copy.deepcopy(self.splits)
        rows["evaluation"][0]["documents"][0] = rows["train"][0]["documents"][0]
        with self.assertRaisesRegex(ValueError, "Duplicate source"):
            data.validate_cases(rows)

    def test_failed_capture_never_publishes_partial_data(self):
        def fail(splits, staged):
            data.write_new(staged / "train.json", {"partial": True})
            raise RuntimeError("measurement interrupted")

        with TemporaryDirectory() as directory:
            dataset = Path(directory) / "dataset"
            with patch.object(data, "DATASET", dataset), patch.object(data, "capture", side_effect=fail):
                with self.assertRaisesRegex(RuntimeError, "interrupted"):
                    data.measure()
            self.assertEqual(list(Path(directory).iterdir()), [])

    def test_request_and_restriction_binding(self):
        case = self.splits["train"][0]
        answer = {"request": "different prompt", "primaryCandidateId": "one"}
        result = {"answer": answer, "ids": ["one", "two", "three", "four"]}
        probe_result = lambda: SimpleNamespace(returncode=0, stdout=json.dumps(result))
        with patch.object(data, "probe", side_effect=lambda *a, **k: probe_result()):
            with self.assertRaisesRegex(ValueError, "submitted request"):
                data.host_run(case, None)
            answer["request"] = case["question"]
            with self.assertRaisesRegex(ValueError, "source restriction"):
                data.host_run(case, 1)
            with self.assertRaisesRegex(ValueError, "source restriction"):
                data.host_run(case, -1)
            self.assertEqual(data.host_run(case, 0)[0]["answer"], answer)


if __name__ == "__main__":
    unittest.main()
