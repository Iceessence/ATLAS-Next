import unittest
import numpy as np

from native_models import fit, predict, fit_specialist, rank


class LearningChecks(unittest.TestCase):
    def test_seeded_weights_optimizer_and_replay(self):
        # Synthetic arithmetic only. This unit fixture never enters task training.
        x = np.array([[0., 0.], [0., 1.], [1., 0.], [1., 1.]])
        y = np.array([0, 1, 1, 1])
        first, metrics = fit(x, y, 4, 11)
        repeat, _ = fit(x, y, 4, 11)
        different, _ = fit(x, y, 4, 37)
        self.assertEqual(first, repeat)
        self.assertNotEqual(first["initialization"], different["initialization"])
        self.assertEqual(first["parameters"], 17)
        self.assertEqual(first["optimizer"]["steps"], 600)
        self.assertLess(metrics["final_log_loss"], metrics["initial_log_loss"])
        self.assertEqual((predict(x, first) > .5).astype(int).tolist(), y.tolist())

    def test_reject_unobserved_class_and_roles(self):
        with self.assertRaisesRegex(ValueError, "positive and negative"):
            fit(np.ones((2, 6)), np.ones(2), 8, 11)
        with self.assertRaisesRegex(ValueError, "preregistered"):
            fit_specialist(("train", "evaluation"), "cpu")

    def test_rank_preserves_abstention(self):
        self.assertEqual(rank([.2, .3], .5), (-1, .3))
        self.assertEqual(rank([.2, .5], .5), (1, .5))


if __name__ == "__main__":
    unittest.main()
