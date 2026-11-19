import copy
import unittest
from subprocess import TimeoutExpired

from atlas_planner import plan
from foundry_sources import project


class PlannerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.profile, cls.tasks = project("train", "packaging")

    def test_scoped_replan_recovers_after_inappropriate_source(self):
        task = self.tasks[0]
        tried = []
        def observe(index, remaining):
            tried.append(index)
            source = self.profile["sources"][index]
            return dict(useful=index==0, state="completed", selected=index, digest=source["sha256"],
                        citation={"line": 1}, result="measured"), {"process_ms": 1}
        trace = plan(task["question"], self.profile, [1,0], observe)
        self.assertEqual(tried, [1,0])
        self.assertEqual(trace["state"], "COMPLETED_SHADOW")
        self.assertEqual([step["depends_on"] for step in trace["steps"]], [[],[1],[2],[3],[4]])
        self.assertFalse(trace["steps"][2]["verified"])

    def test_stale_source_and_bad_candidate_do_not_read(self):
        task = self.tasks[0]
        stale = copy.deepcopy(self.profile)
        stale["sources"][0]["text"] += "different"
        def read(*_):
            self.fail("No read is authorized")
        self.assertEqual(plan(task["question"], stale, [0], read)["state"], "QUARANTINED")
        self.assertEqual(plan(task["question"], self.profile, [31], read)["state"], "QUARANTINED")
        self.assertEqual(plan(task["question"], self.profile, [0], read, cancelled=lambda: True)["state"], "CANCELED")
        self.assertEqual(plan(self.tasks[4]["question"], self.profile, [0], read)["state"], "ABSTAINED")

    def test_wall_budget_reaches_host_and_prevents_second_attempt(self):
        task = self.tasks[0]
        called = []
        def timeout(i, seconds):
            called.append((i,seconds))
            raise TimeoutExpired("probe", seconds)
        result = plan(task["question"], self.profile, [0,1], timeout, wall_seconds=.25)
        self.assertEqual(result["state"], "ABSTAINED")
        self.assertEqual(len(called), 1)
        self.assertLessEqual(called[0][1], .25)


if __name__ == "__main__":
    unittest.main()
