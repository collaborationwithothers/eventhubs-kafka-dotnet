"""Expose the phase suite to the existing lifecycle test discovery."""
from pathlib import Path
import unittest

START = Path(__file__).resolve().parents[1] / "phase" / "tests"


def load_tests(loader, tests, pattern):
    return unittest.TestLoader().discover(str(START), pattern="test_*.py", top_level_dir=str(START))
