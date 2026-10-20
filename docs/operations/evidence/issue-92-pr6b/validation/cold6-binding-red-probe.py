import copy
import unittest

import accept


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

        self.assertTrue(accept.validation_source_inputs_match(self.measured, validation_sources))

    def test_rejects_a_validation_source_map_with_one_mismatched_hash(self) -> None:
        mismatched_inputs = dict(self.measured["source_input_sha256"])
        mismatched_inputs["tests/ConsumerTests.cs"] = "c" * 64
        validation_sources = [
            {"source_input_sha256": dict(self.measured["source_input_sha256"])},
            {"source_input_sha256": mismatched_inputs},
        ]

        self.assertFalse(accept.validation_source_inputs_match(self.measured, validation_sources))


class ReplayReportBindingTests(unittest.TestCase):
    def setUp(self) -> None:
        self.location = {
            "start": {"line": 12, "column": 9},
            "end": {"line": 12, "column": 28},
        }
        self.source = "class ProbeGateEvaluator { bool Accept() => true; }"
        report_row = (
            "Killed",
            "ProbeGate/ProbeGateEvaluator.cs",
            self.location,
            "Equality mutation",
            'scenarioId != "A01"',
        )
        self.report_rows = {"1839": report_row}
        self.selected = {
            "id": "1839",
            "status": "Killed",
            "file": "ProbeGate/ProbeGateEvaluator.cs",
            "location": copy.deepcopy(self.location),
            "mutatorName": "Equality mutation",
            "replacement": 'scenarioId != "A01"',
            "source": self.source,
        }
        self.attempt = {
            "mutant_id": "1839",
            "status": "Killed",
            "file": "ProbeGate/ProbeGateEvaluator.cs",
            "mutator": "Equality mutation",
            "span": copy.deepcopy(self.location),
            "replacement": 'scenarioId != "A01"',
            "report_status": "Killed",
        }

    def test_accepts_selected_record_with_matching_report_metadata(self) -> None:
        result = accept.selected_report_coverage([self.selected], self.report_rows)

        self.assertTrue(result[0])

    def test_rejects_selected_record_with_changed_start_location(self) -> None:
        selected = copy.deepcopy(self.selected)
        selected["location"]["start"]["column"] += 1

        self.assertFalse(accept.selected_report_coverage([selected], self.report_rows)[0])

    def test_rejects_selected_record_with_changed_end_location(self) -> None:
        selected = copy.deepcopy(self.selected)
        selected["location"]["end"]["column"] += 1

        self.assertFalse(accept.selected_report_coverage([selected], self.report_rows)[0])

    def test_rejects_selected_record_with_changed_replacement(self) -> None:
        selected = {**self.selected, "replacement": "different replacement"}

        self.assertFalse(accept.selected_report_coverage([selected], self.report_rows)[0])

    def test_rejects_selected_record_with_changed_embedded_source(self) -> None:
        selected = {**self.selected, "source": "changed source"}

        self.assertFalse(accept.selected_report_coverage([selected], self.report_rows)[0])

    def test_accepts_attempt_with_matching_report_metadata(self) -> None:
        self.assertTrue(accept.replay_attempts_match_report([self.attempt], self.report_rows))

    def test_rejects_attempt_metadata_changed_after_selection(self) -> None:
        for field, value in (
            ("status", "Survived"),
            ("file", "Other.cs"),
            ("mutator", "Other mutation"),
            ("report_status", "Survived"),
            ("replacement", "different replacement"),
        ):
            attempt = copy.deepcopy(self.attempt)
            attempt[field] = value
            with self.subTest(field=field):
                self.assertFalse(accept.replay_attempts_match_report([attempt], self.report_rows))

    def test_rejects_attempt_with_changed_start_or_end_span(self) -> None:
        for endpoint in ("start", "end"):
            attempt = copy.deepcopy(self.attempt)
            attempt["span"][endpoint]["column"] += 1
            with self.subTest(endpoint=endpoint):
                self.assertFalse(accept.replay_attempts_match_report([attempt], self.report_rows))

    def test_rejects_attempt_with_changed_identity(self) -> None:
        attempt = {**self.attempt, "mutant_id": "other-id"}

        self.assertFalse(accept.replay_attempts_match_report([attempt], self.report_rows))


if __name__ == "__main__":
    unittest.main()
