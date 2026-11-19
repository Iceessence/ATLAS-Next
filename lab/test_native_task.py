import tempfile
import unittest
from pathlib import Path

from native_task import live_task, read_sources


class NativeTaskChecks(unittest.TestCase):
    def test_verified_delivery_and_conflict(self):
        with tempfile.TemporaryDirectory() as scratch:
            opened = Path(scratch) / "release.md"
            distinct = Path(scratch) / "limits.md"
            opened.write_text("Deploy region: eu-west\n", encoding="utf-8")
            distinct.write_text("Retry budget: 5 attempts\n", encoding="utf-8")
            question = "What is the deploy region?"
            result = live_task(question, [opened, distinct])
            self.assertEqual(result["external_inference_calls"], 0)
            self.assertEqual(result["deterministic_host"]["answer"], "eu-west")
            self.assertEqual(result["deterministic_host"]["evidence"]["path"], str(opened.resolve()))
            self.assertEqual({model["parameters"] for model in result["models"]}, {37, 65})
            self.assertTrue(all(model["qualification"] == "shadow" for model in result["models"]))
            distinct.write_text("Deploy region: ap-south\n", encoding="utf-8")
            result = live_task(question, [opened, distinct])
            self.assertEqual(result["deterministic_host"]["research_verdict"], "contradiction")
            self.assertEqual(result["shadow_observation"]["state"], "needs clarification")

    def test_only_explicit_bounded_sources(self):
        with tempfile.TemporaryDirectory() as scratch:
            path = Path(scratch) / "source.md"
            path.write_bytes(b"Project field: recorded\n")
            with self.assertRaisesRegex(ValueError, "Duplicate"):
                read_sources([path, path])
            with self.assertRaisesRegex(ValueError, "Markdown"):
                read_sources([path.with_suffix(".ps1")])
            path.write_bytes(b"x" * (256 * 1024 + 1))
            with self.assertRaisesRegex(ValueError, "size"):
                read_sources([path])


if __name__ == "__main__":
    unittest.main()
