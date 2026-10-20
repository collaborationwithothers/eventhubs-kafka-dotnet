import contextlib
import gzip
import importlib.util
import io
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch


EXTRACT_PATH = Path(__file__).with_name("extract.py")
SPEC = importlib.util.spec_from_file_location("issue_92_extract", EXTRACT_PATH)
assert SPEC is not None and SPEC.loader is not None
extract = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(extract)


class FreshReviewOutputTests(unittest.TestCase):
    def test_fresh_review_directory_is_allowed(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            review_dir = Path(temporary_directory) / "review"

            extract.require_fresh_review_outputs(review_dir)

            self.assertFalse(review_dir.exists())

    def test_existing_review_output_is_rejected_unchanged(self) -> None:
        existing_outputs = (
            ("lines.json", b"human-reviewed lines\n"),
            ("lines.json.gz", gzip.compress(b"human-reviewed lines\n")),
            ("dispositions.json", b"human-reviewed dispositions\n"),
            ("dispositions.json.gz", gzip.compress(b"human-reviewed dispositions\n")),
        )
        for name, original_bytes in existing_outputs:
            with self.subTest(name=name), tempfile.TemporaryDirectory() as temporary_directory:
                temporary_path = Path(temporary_directory)
                run_dir = temporary_path / "run"
                review_dir = temporary_path / "review"
                run_dir.mkdir()
                review_dir.mkdir()
                review_file = review_dir / name
                review_file.write_bytes(original_bytes)
                stderr = io.StringIO()

                with patch("sys.argv", [
                    str(EXTRACT_PATH), "--run-dir", str(run_dir), "--review-dir", str(review_dir)
                ]), contextlib.redirect_stderr(stderr):
                    exit_code = extract.main()

                self.assertEqual(1, exit_code)
                self.assertIn("refusing to overwrite existing review output", stderr.getvalue())
                self.assertIn(str(review_file), stderr.getvalue())
                self.assertEqual(original_bytes, review_file.read_bytes())
                self.assertEqual([name], sorted(path.name for path in review_dir.iterdir()))
                self.assertEqual([], list(run_dir.iterdir()))


class RunnerDiagnosticsScopeTests(unittest.TestCase):
    def test_labels_filtered_warning_count_and_distinguishes_complete_wrn_scan(self) -> None:
        lines = [
            "[WRN] Warning term matched",
            "[WRN] DUBIOUS relay result",
            "[WRN] ASSUMING selected FRAMEWORK is present",
            "[WRN] Preview runner is in preview",
            "[WRN] Unidentified mutation resulted in a compile error",
        ]
        with tempfile.TemporaryDirectory() as temporary_directory:
            run_dir = Path(temporary_directory)
            (run_dir / "command.log").write_text("\n".join(lines) + "\n", encoding="utf-8")

            diagnostics, _ = extract.runner_diagnostics(run_dir)

        complete_wrn_count = sum("[WRN]" in line for line in lines)
        self.assertEqual(3, diagnostics["warning_count"])
        self.assertEqual(5, complete_wrn_count)
        self.assertEqual([1, 2, 3], [warning["line"] for warning in diagnostics["warnings"]])
        self.assertEqual({
            "definition": "Filtered count of lines containing 'warning' or 'dubious', or containing both 'assum' and 'framework'.",
            "case_sensitive": False,
            "counts_all_WRN_tagged_lines": False,
        }, diagnostics["warning_count_scope"])


if __name__ == "__main__":
    unittest.main()
