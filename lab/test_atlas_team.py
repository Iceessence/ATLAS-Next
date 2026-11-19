import unittest
from unittest.mock import patch

import numpy as np

from atlas_team import ROLES, combined, route
from foundry_sources import project


class MasterRoleChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.profile, cls.tasks = project("train", "packaging")

    def test_disagreement_requests_independent_review(self):
        team = {"roles": {"Research": {"weights": {}}, "Engineering": {"weights": {}}}}
        with patch("atlas_team.infer", return_value=("read_fact",[.9,.05,.05])), \
             patch("atlas_team.predict", side_effect=[np.array([.9,.1,.1]),np.array([.8])]):
            order, _, messages = route(self.tasks[0]["question"], self.profile, ROLES, {}, team, "p", {"procedures":{}})
        self.assertEqual({msg["from_role"] for msg in messages[:-1]}, set(ROLES))
        self.assertEqual(messages[-1]["proposal"], "REQUEST_REVIEW")
        self.assertEqual(order[0], 0)

    def test_cannot_compose_two_factual_goals(self):
        with self.assertRaisesRegex(ValueError, "one explicit audited fact"):
            combined(self.profile, self.tasks, self.tasks[0]["question"], self.tasks[1]["question"], {}, {}, "p")

    def test_composition_budgets_and_connects_independent_goals(self):
        def stub(_, group, question, language, team, digest, *, max_steps, deadline):
            self.assertLessEqual(max_steps, 5)
            return dict(request_sha256=question, shadow={"state":"COMPLETED_SHADOW","steps":[1,2,3]})
        fact, definition = self.tasks[0]["question"], self.tasks[3]["question"]
        with patch("atlas_team.infer", side_effect=[("read_fact",[.7,.2,.1]),("locate_definition",[.05,.9,.05])]), \
             patch("atlas_team.live_team", side_effect=stub):
            result = combined(self.profile, self.tasks, fact, definition, {}, {}, "p")
        self.assertEqual(result["steps_used"], 6)
        self.assertEqual(result["goals"][1]["prior"], definition)


if __name__ == "__main__":
    unittest.main()
