import unittest
import numpy as np

from atlas_language import fit_weights, grounded_action, language_training, matrix, probabilities, typed_baseline, vocabulary


class LanguageChecks(unittest.TestCase):
    def test_reproducible_original_weights_and_optimizer(self):
        phrases = ["identify a declaration", "give a named value", "do not answer"] * 2
        vocab = vocabulary(phrases)
        x = matrix(phrases, vocab)
        y = [0, 1, 2, 0, 1, 2]
        first, stats = fit_weights(x, y, 3, hidden=4)
        second, _ = fit_weights(x, y, 3, hidden=4)
        self.assertEqual(first, second)
        self.assertGreater(stats["loss_before"], stats["loss_after"])
        self.assertEqual(probabilities(x, first).argmax(axis=1).tolist(), y)

    def test_literal_reference_limits_and_negation(self):
        p = {"sources": [{"suffix": ".md", "key": "Release version"},
                         {"suffix": ".py", "key": "Library.parse"}]}
        self.assertEqual(typed_baseline("I need the release version.", p), "source:0")
        self.assertEqual(grounded_action("Show Library.parse.", p, "locate_definition"), "source:1")
        for question in ("Keep the release version, but do not return it.", "If release version changed, rebuild it.",
                         "Show release version and Library.parse.", "Show release version.foo."):
            self.assertEqual(typed_baseline(question, p), "withhold")

    def test_fit_cannot_take_evaluation_roles(self):
        for partition in ("evaluation", "protected_eval"):
            with self.assertRaisesRegex(ValueError, "preregistered"):
                language_training(("train", partition))


if __name__ == "__main__":
    unittest.main()
