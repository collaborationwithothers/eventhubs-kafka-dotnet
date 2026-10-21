"""Seven fixed synthetic regressions; no real report or mutation execution."""

import copy
import importlib.util
import unittest
from pathlib import Path


SPEC = importlib.util.spec_from_file_location("map_closure", Path(__file__).with_name("map-closure.py"))
mapper = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(mapper)


def synthetic_mutant(mutant_id="1", status="Survived", replacement="false"):
    return {"id": mutant_id, "status": status, "replacement": replacement,
            "mutatorName": "Boolean mutation", "location": {
                "start": {"line": 1, "column": 1}, "end": {"line": 1, "column": 5}}}


def synthetic_report(mutants, source="true\n"):
    return {"schemaVersion": "2", "projectRoot": "/synthetic/project",
            "testFiles": {}, "thresholds": {"high": 80, "low": 60},
            "files": {"/synthetic/project/Example.cs": {"source": source, "mutants": mutants}}}


class SyntheticMappingTests(unittest.TestCase):
    def match(self, originals, current, current_source="true\n"):
        old_sources, old_records = mapper.parse_report(synthetic_report(originals))
        new_sources, new_records = mapper.parse_report(synthetic_report(current, current_source))
        return mapper.match_variant(old_records[0], mapper.tuple_index(new_records),
                                    old_sources, new_sources, {"Example.cs": "true\n"})

    def test_changed_report_id_matches_same_tuple(self):
        matched = self.match([synthetic_mutant("1")], [synthetic_mutant("700", "Killed")])
        self.assertEqual("unique", matched["match_status"])
        self.assertEqual(["700"], [record["id"] for record in matched["matches"]])

    def test_same_id_changed_replacement_is_missing(self):
        matched = self.match([synthetic_mutant()], [synthetic_mutant(replacement="null")])
        self.assertEqual({"match_status": "missing", "matches": []}, matched)

    def test_duplicate_tuple_is_ambiguous(self):
        matched = self.match([synthetic_mutant()], [synthetic_mutant("2"), synthetic_mutant("3")])
        self.assertEqual("ambiguous", matched["match_status"])
        self.assertEqual(2, len(matched["matches"]))

    def test_changed_embedded_source_is_source_mismatch(self):
        matched = self.match([synthetic_mutant()], [synthetic_mutant("2")], "true;\n")
        self.assertEqual("source_mismatch", matched["match_status"])
        self.assertEqual(1, len(matched["matches"]))

    def test_mixed_raw_and_scored_populations(self):
        _, records = mapper.parse_report(synthetic_report([
            synthetic_mutant("1", "Killed"), synthetic_mutant("2", "Timeout"),
            synthetic_mutant("3", "Survived"), synthetic_mutant("4", "CompileError")]))
        counts, score = mapper.count_population(records)
        self.assertEqual({"Killed": 1, "Timeout": 1, "Survived": 1, "CompileError": 1}, counts)
        self.assertEqual(4, sum(counts.values()))
        self.assertEqual(2, score["numerator"])
        self.assertEqual(3, score["denominator"])
        self.assertIsNone(mapper.count_population([])[1]["percent"])

    def test_proof_does_not_rewrite_survived_or_denominator(self):
        _, records = mapper.parse_report(synthetic_report([synthetic_mutant("5531")]))
        before = copy.deepcopy(records)
        counts_before, score_before = mapper.count_population(records)
        annotation = mapper.proof_annotation("5531")
        self.assertEqual("runtime premise not verified", annotation["applicability"])
        self.assertEqual(before, records)
        self.assertEqual((counts_before, score_before), mapper.count_population(records))
        self.assertEqual("Survived", records[0]["status"])
        self.assertEqual(1, score_before["denominator"])

    def test_unknown_status_and_oversize_rejected(self):
        with self.assertRaisesRegex(mapper.MappingError, "unsupported status"):
            mapper.parse_report(synthetic_report([synthetic_mutant(status="RuntimeError")]))
        oversized = [synthetic_mutant(str(index)) for index in range(10001)]
        with self.assertRaisesRegex(mapper.MappingError, "exceeds 10000"):
            mapper.parse_report(synthetic_report(oversized))


if __name__ == "__main__":
    unittest.main()
