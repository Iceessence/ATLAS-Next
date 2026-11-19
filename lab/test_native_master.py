import unittest
from unittest.mock import patch

import numpy as np
import native_master as master


class MasterChecks(unittest.TestCase):
    def test_family_never_in_own_training_fold(self):
        rows = [{"family": family} for family in ("alpha", "beta", "gamma")]
        with patch.object(master, "idf_table"), patch.object(master, "specialist_inputs", return_value=(None, None)), \
             patch.object(master, "fit", return_value=({}, {})), \
             patch.object(master, "master_examples", return_value=(np.ones((1, 4)), np.array([1]))):
            x, y, folds = master.crossfit(rows, {}, {"hidden": 8, "seed": 37}, "cpu")
        self.assertEqual(x.shape, (3, 4))
        self.assertEqual(y.tolist(), [1, 1, 1])
        for fold in folds:
            self.assertNotIn(fold["held_family"], fold["fit_families"])
            self.assertEqual(len(fold["fit_families"]), 2)

    def test_low_master_score_routes_to_host(self):
        specialist = {"idf": {}, "policies": {"native": {}}}
        gate = {"weights": [[[0], [0], [0], [0]], [0]], "threshold": .65}
        with patch.object(master, "specialist_signal", return_value=(2, [.99, .4, .3, .1])):
            self.assertEqual(master.gate_action({}, specialist, gate), (None, .5))
            self.assertEqual(master.gate_action({}, specialist, confidence=.5), (2, .99))


if __name__ == "__main__":
    unittest.main()
