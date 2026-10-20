import unittest

from accept import validation_source_inputs_match


class ValidationSourceInputsMatchTests(unittest.TestCase):
    def setUp(self) -> None:
        self.measured = {
            "tested_commit": "a2001534d94cd1135b9af53dc0e9d9816b00ce3d",
            "source_input_sha256": {
                "src/Consumer.cs": "a" * 64,
                "tests/ConsumerTests.cs": "b" * 64,
            },
        }

    def test_accepts_matching_validation_source_maps(self) -> None:
        validation_sources = [
            {"tested_commit": "different-commit", "source_input_sha256": dict(self.measured["source_input_sha256"])},
            {"source_input_sha256": dict(self.measured["source_input_sha256"])},
        ]

        self.assertTrue(validation_source_inputs_match(self.measured, validation_sources))

    def test_rejects_a_validation_source_map_with_one_mismatched_hash(self) -> None:
        mismatched_inputs = dict(self.measured["source_input_sha256"])
        mismatched_inputs["tests/ConsumerTests.cs"] = "c" * 64
        validation_sources = [
            {"source_input_sha256": dict(self.measured["source_input_sha256"])},
            {"source_input_sha256": mismatched_inputs},
        ]

        self.assertFalse(validation_source_inputs_match(self.measured, validation_sources))


if __name__ == "__main__":
    unittest.main()
