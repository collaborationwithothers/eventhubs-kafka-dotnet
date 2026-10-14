#!/usr/bin/env python3
"""Tests for scripts/check_docs.py. Run: python3 -B scripts/tests/check_docs_test.py

Each test builds a small repository in a temporary directory, so the checks
run against known files. GitHub is replaced by a stub; no network is used.
"""

from __future__ import annotations

import datetime as dt
import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

HERE = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location("check_docs", HERE.parent / "check_docs.py")
cd = importlib.util.module_from_spec(SPEC)
sys.modules["check_docs"] = cd  # dataclasses resolve annotations through sys.modules
SPEC.loader.exec_module(cd)

TODAY = dt.date(2026, 10, 13)

BLUEPRINT_OK = """# Blueprint

## 4. Key decisions

### ADR-001: First

**Context.** Why.

**Options considered.**
1. A.

**Decision.** A.

**Consequences.**
- Fine.

### ADR-002 (option, deferred): Lanes

**Context.** Why.

**Option.** Lanes.

**Status.** Not built (S1).

---

## 5. Failure modes

### Sources

| ID | Source | Updated |
|---|---|---|
| S1 | [One](https://example.test/one) | read 2026-10-11 |
"""


class StubGitHub:
    """Answers `gh api` routes from a dictionary; records what was asked."""

    def __init__(self, routes: dict):
        self.routes = routes
        self.slug = "owner/repo"
        self.asked: list[str] = []

    def get(self, route: str):
        self.asked.append(route)
        return self.routes.get(route)


def b64(text: str) -> dict:
    import base64

    return {"content": base64.b64encode(text.encode()).decode()}


class CheckDocsTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        (self.root / "docs").mkdir()
        self.write("docs/blueprint.md", BLUEPRINT_OK)

    def tearDown(self):
        self.tmp.cleanup()

    def write(self, path: str, text: str) -> None:
        f = self.root / path
        f.parent.mkdir(parents=True, exist_ok=True)
        f.write_text(text, encoding="utf-8")

    def line_of(self, path: str, needle: str) -> int:
        """1-based line of the first line containing needle, computed rather than counted by hand."""
        for n, line in enumerate((self.root / path).read_text(encoding="utf-8").split("\n"), 1):
            if needle in line:
                return n
        raise AssertionError(f"{needle!r} not in {path}")

    def path_without_gh(self) -> Path:
        """A PATH directory holding git and sh, but no gh."""
        import shutil

        d = self.root / "path-without-gh"
        d.mkdir(exist_ok=True)
        for tool in ("git", "sh"):
            real = shutil.which(tool)
            self.assertIsNotNone(real, tool)
            link = d / tool
            if not link.exists():
                link.symlink_to(real)
        return d

    def run_checks(self, paths: list[str], gh=None, branch="claude/1-x") -> list[str]:
        findings = cd.run(self.root, paths, TODAY, gh, branch)
        return [f"{f.path}:{f.line}: {f.cls}: {f.message}" for f in findings]

    # --- clean baseline -----------------------------------------------------

    def test_clean_blueprint_has_no_findings(self):
        self.assertEqual(self.run_checks(["docs/blueprint.md"]), [])

    # --- ascii ----------------------------------------------------------------

    def test_non_ascii_character_is_reported_with_its_code_point(self):
        self.write("AGENTS.md", "# A\n\nAn em dash \u2014 here.\n")
        out = self.run_checks(["AGENTS.md"])
        self.assertEqual(out, ["AGENTS.md:3: ascii: non-ASCII character U+2014"])

    def test_unit_symbols_pass_and_other_non_ascii_is_reported(self):
        self.write("AGENTS.md", "# A\n\n20 \u00b0C and 5 \u00b5s are fine.\nA curly \u2019 is not.\n")
        out = self.run_checks(["AGENTS.md"])
        self.assertEqual(out, ["AGENTS.md:4: ascii: non-ASCII character U+2019"])

    # --- dates ----------------------------------------------------------------

    def test_future_date_is_reported(self):
        self.write("AGENTS.md", "# A\n\nDecided 2026-10-14.\n")
        out = self.run_checks(["AGENTS.md"])
        self.assertEqual(out, ["AGENTS.md:3: date: 2026-10-14 is after today (2026-10-13)"])

    def test_today_and_past_dates_pass(self):
        self.write("AGENTS.md", "# A\n\nDecided 2026-10-13 and 2025-01-01.\n")
        self.assertEqual(self.run_checks(["AGENTS.md"]), [])

    def test_future_timestamp_is_reported_and_allowed_line_is_not(self):
        self.write("AGENTS.md", "# A\n\nRe-run at 2026-10-14T16:17Z.\nRetires on 2027-01-01. <!-- check-docs: allow date: announced retirement -->\nNo reason 2027-01-02. <!-- check-docs: allow date -->\n")
        out = self.run_checks(["AGENTS.md"])
        self.assertEqual(out, [
            "AGENTS.md:3: date: 2026-10-14 is after today (2026-10-13)",
            "AGENTS.md:5: date: 2027-01-02 is after today (2026-10-13)",
        ])

    def test_only_the_date_class_can_be_waived(self):
        self.write("AGENTS.md", "# A\n\nSee ADR-099. <!-- check-docs: allow adr: no -->\n")
        out = self.run_checks(["AGENTS.md"])
        self.assertEqual(out, ["AGENTS.md:3: adr: ADR-099 has no heading in docs/blueprint.md"])

    def test_impossible_date_is_reported(self):
        self.write("AGENTS.md", "# A\n\nDecided 2026-13-01.\n")
        out = self.run_checks(["AGENTS.md"])
        self.assertEqual(out, ["AGENTS.md:3: date-invalid: 2026-13-01 is not a calendar date"])

    # --- sources --------------------------------------------------------------

    def test_undefined_source_in_blueprint_is_reported(self):
        self.write("docs/blueprint.md", BLUEPRINT_OK.replace("Not built (S1).", "Not built (S1, S2)."))
        out = self.run_checks(["docs/blueprint.md"])
        self.assertEqual(out, [f"docs/blueprint.md:{self.line_of('docs/blueprint.md', '(S1, S2)')}: sources: S2 is not defined in docs/blueprint.md"])

    def test_unreferenced_source_in_blueprint_is_reported(self):
        self.write("docs/blueprint.md", BLUEPRINT_OK.replace("Not built (S1).", "Not built."))
        out = self.run_checks(["docs/blueprint.md"])
        self.assertEqual(out, [f"docs/blueprint.md:{self.line_of('docs/blueprint.md', '| S1 |')}: sources: S1 is defined but never referenced (write `- S1: retired ...` to keep the number without a source)"])

    def test_duplicate_source_definition_is_reported(self):
        dup = BLUEPRINT_OK + "| S1 | [Again](https://example.test/again) | read 2026-10-11 |\n"
        self.write("docs/blueprint.md", dup)
        out = self.run_checks(["docs/blueprint.md"])
        self.assertEqual(out, [f"docs/blueprint.md:{self.line_of('docs/blueprint.md', '| S1 |')}: sources: S1 is defined 2 times"])

    def test_document_without_definitions_resolves_against_blueprint(self):
        self.write("docs/agents/factory.md", "# F\n\nSee S1 and S9.\n")
        out = self.run_checks(["docs/agents/factory.md"])
        self.assertEqual(out, ["docs/agents/factory.md:3: sources: S9 is not defined in docs/blueprint.md"])

    def test_document_with_its_own_list_is_self_contained(self):
        self.write("docs/research/r.md", "# R\n\nFact (S01). Other (S02).\n\n## Sources\n\n- S01: https://a.test\n- S02: https://b.test\n- S03: retired, no source\n")
        self.assertEqual(self.run_checks(["docs/research/r.md"]), [])
        self.write("docs/research/r.md", "# R\n\nFact (S01).\n\n## Sources\n\n- S01: https://a.test\n- S02: https://b.test\n")
        out = self.run_checks(["docs/research/r.md"])
        self.assertEqual(out, ["docs/research/r.md:8: sources: S02 is defined but never referenced (write `- S02: retired ...` to keep the number without a source)"])

    # --- ADR numbers and format -----------------------------------------------

    def test_adr_reference_without_heading_is_reported(self):
        self.write("AGENTS.md", "# A\n\nSee ADR-001 and ADR-012.\n")
        out = self.run_checks(["AGENTS.md"])
        self.assertEqual(out, ["AGENTS.md:3: adr: ADR-012 has no heading in docs/blueprint.md"])

    def test_repeated_adr_heading_is_reported(self):
        text = BLUEPRINT_OK.replace("### ADR-002 (option, deferred): Lanes", "### ADR-001: Lanes").replace(
            "**Context.** Why.\n\n**Option.** Lanes.\n\n**Status.** Not built (S1).",
            "**Context.** Why.\n\n**Options considered.**\n1. L.\n\n**Decision.** L (S1).\n\n**Consequences.**\n- Ok.")
        self.write("docs/blueprint.md", text)
        out = self.run_checks(["docs/blueprint.md"])
        self.assertEqual(out, [f"docs/blueprint.md:{self.line_of('docs/blueprint.md', '### ADR-001: Lanes')}: adr: ADR-001 heading repeats line {self.line_of('docs/blueprint.md', '### ADR-001: First')}"])

    def test_adr_missing_or_misordered_parts_are_reported(self):
        text = BLUEPRINT_OK.replace(
            "**Context.** Why.\n\n**Options considered.**\n1. A.\n\n**Decision.** A.\n\n**Consequences.**\n- Fine.",
            "Numbering note.\n\n**Context.** Why.\n\n**Decision.** A.\n\n**Options considered.**\n1. A.")
        self.write("docs/blueprint.md", text)
        out = self.run_checks(["docs/blueprint.md"])
        n = self.line_of("docs/blueprint.md", "### ADR-001: First")
        self.assertEqual(out, [
            f"docs/blueprint.md:{n}: adr-format: **Decision.** is out of order",
            f"docs/blueprint.md:{n}: adr-format: first paragraph does not start with **Context.**",
            f"docs/blueprint.md:{n}: adr-format: missing **Consequences.**",
        ])

    def test_option_adr_uses_option_and_status(self):
        text = BLUEPRINT_OK.replace("**Status.** Not built (S1).", "**Decision.** Not built (S1).")
        self.write("docs/blueprint.md", text)
        out = self.run_checks(["docs/blueprint.md"])
        self.assertEqual(out, [f"docs/blueprint.md:{self.line_of('docs/blueprint.md', '### ADR-002 (option')}: adr-format: missing **Status.**"])

    # --- cross-references -----------------------------------------------------

    def test_heading_reference_resolves_by_prefix_in_each_written_form(self):
        self.write("AGENTS.md", "# A\n\n## GOVERNANCE (approved)\n\n### Merge rule\n\nText.\n")
        self.write("docs/agents/factory.md", '# F\n\nSee AGENTS.md "Merge rule", AGENTS.md, "Merge rule", AGENTS.md\'s "Merge rule", GOVERNANCE "Merge rule" and docs/blueprint.md section 4.\n')
        self.assertEqual(self.run_checks(["docs/agents/factory.md"]), [])
        self.write("docs/agents/factory.md", '# F\n\nSee AGENTS.md, "Nope" and AGENTS.md\'s "Nor".\n')
        out = self.run_checks(["docs/agents/factory.md"])
        self.assertEqual(out, [
            'docs/agents/factory.md:3: xref: AGENTS.md has no heading "Nope"',
            'docs/agents/factory.md:3: xref: AGENTS.md has no heading "Nor"',
        ])

    def test_broken_heading_references_are_reported(self):
        self.write("AGENTS.md", "# A\n\n### Merge rule\n")
        self.write("docs/agents/factory.md", '# F\n\nSee AGENTS.md "Escalation", GOVERNANCE "Handoffs", docs/missing.md "X" and docs/blueprint.md section 9.\n')
        out = self.run_checks(["docs/agents/factory.md"])
        self.assertEqual(out, [
            'docs/agents/factory.md:3: xref: AGENTS.md has no heading "Escalation"',
            'docs/agents/factory.md:3: xref: AGENTS.md has no heading "Handoffs"',
            "docs/agents/factory.md:3: xref: docs/blueprint.md has no section 9",
            "docs/agents/factory.md:3: xref: docs/missing.md does not exist",
        ])

    # --- GitHub-backed checks -------------------------------------------------

    def test_issue_references_are_checked_for_existence_and_kind(self):
        gh = StubGitHub({"issues/8": {"number": 8}, "issues/41": {"number": 41, "pull_request": {}}})
        self.write("AGENTS.md", "# A\n\nSee issue #8, PR #41, pull request #8, issue #41 and #99. Not https://x.test/a#5, openai/codex#52549, community discussion #156147 or discussions #1, #2 and #3.\nBut Claude Code/Codex issue #7, scripts/x.py issue #70 and openai/codex issue #700 are ours.\n")
        out = self.run_checks(["AGENTS.md"], gh=gh)
        self.assertEqual(out, [
            "AGENTS.md:3: refs: #41 is called an issue but is a pull request",
            "AGENTS.md:3: refs: #8 is called a pull request but is an issue",
            "AGENTS.md:3: refs: #99 does not exist in owner/repo (another repository's number is written owner/repo#99)",
            "AGENTS.md:4: refs: #7 does not exist in owner/repo (another repository's number is written owner/repo#7)",
            "AGENTS.md:4: refs: #70 does not exist in owner/repo (another repository's number is written owner/repo#70)",
            "AGENTS.md:4: refs: #700 does not exist in owner/repo (another repository's number is written owner/repo#700)",
        ])
        self.assertEqual(gh.asked, ["issues/8", "issues/41", "issues/99", "issues/7", "issues/70", "issues/700"])

    def test_github_unavailable_is_not_treated_as_an_answer(self):
        class Down:
            slug = "owner/repo"

            def get(self, route):
                raise cd.Unavailable("HTTP 403")

        self.write("AGENTS.md", "# A\n\nSee issue #8.\n")
        with self.assertRaises(cd.Unavailable):
            cd.run(self.root, ["AGENTS.md"], TODAY, Down(), "claude/1-x")

    def test_gh_wrapper_distinguishes_not_found_from_failure(self):
        fake = self.root / "bin"
        fake.mkdir()
        gh = fake / "gh"
        gh.write_text("#!/bin/sh\ncase \"$2\" in\n  */issues/404) echo '{\"message\":\"Not Found\"}'; echo 'gh: Not Found (HTTP 404)' >&2; exit 1;;\n  */issues/403) echo 'gh: Forbidden (HTTP 403)' >&2; exit 1;;\n  */issues/410) echo 'gh: Gone (HTTP 410)' >&2; exit 1;;\n  */issues/500) echo 'not json';;\n  *) echo '{\"number\": 1}';;\nesac\n")
        gh.chmod(0o755)
        old = os.environ["PATH"]
        os.environ["PATH"] = f"{fake}{os.pathsep}{old}"
        try:
            api = cd.GitHub("owner/repo")
            self.assertEqual(api.get("issues/1"), {"number": 1})
            self.assertIsNone(api.get("issues/404"))
            self.assertIsNone(api.get("issues/410"))
            with self.assertRaises(cd.Unavailable):
                api.get("issues/403")
            with self.assertRaises(cd.Unavailable):
                api.get("issues/500")
            os.environ["PATH"] = str(self.path_without_gh())
            with self.assertRaises(cd.Unavailable):
                api.get("issues/1")
        finally:
            os.environ["PATH"] = old

    def test_adr_number_new_on_two_branches_is_reported(self):
        main = BLUEPRINT_OK.replace("### ADR-002 (option, deferred): Lanes\n\n**Context.** Why.\n\n**Option.** Lanes.\n\n**Status.** Not built (S1).\n\n", "")
        main = main.replace("**Consequences.**\n- Fine.", "**Consequences.**\n- Fine (S1).")
        other = main.replace("## 5. Failure modes", "### ADR-002: Theirs\n\n**Context.** T.\n\n**Options considered.**\n1. T.\n\n**Decision.** T.\n\n**Consequences.**\n- T.\n\n---\n\n## 5. Failure modes")
        gh = StubGitHub({
            "branches?per_page=100&page=1": [{"name": "main"}, {"name": "claude/1-x"}, {"name": "claude/8-adrs"}],
            "contents/docs/blueprint.md?ref=main": b64(main),
            "contents/docs/blueprint.md?ref=claude/8-adrs": b64(other),
        })
        line = self.line_of("docs/blueprint.md", "### ADR-002 (option")
        prefix = f"docs/blueprint.md:{line}: adr: ADR-002 is also new on branch claude/8-adrs, with a different title; "
        # Neither branch has a pull request: nothing is decided, so nobody is told to do what a push gate forbids.
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [])
        self.assertNotIn("contents/docs/blueprint.md?ref=claude/1-x", gh.asked)
        # Only theirs has a pull request: we renumber.
        gh.routes["pulls?state=open&head=owner:claude/8-adrs"] = [{"created_at": "2026-10-13T10:00:00Z", "number": 8}]
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [prefix + "this branch has no open pull request and that one has, so this branch renumbers"])
        # Theirs opened first: we renumber.
        gh.routes["pulls?state=open&head=owner:claude/1-x"] = [{"created_at": "2026-10-13T11:00:00Z", "number": 1}]
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [prefix + "that branch's pull request opened first, so this branch renumbers"])
        # Ours opened first: we keep the number.
        gh.routes["pulls?state=open&head=owner:claude/1-x"] = [{"created_at": "2026-10-13T09:00:00Z", "number": 1}]
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [])
        # Only ours has a pull request: we keep it.
        gh.routes["pulls?state=open&head=owner:claude/8-adrs"] = []
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [])
        # The same ADR carried on both branches is not a collision.
        gh.routes["contents/docs/blueprint.md?ref=claude/8-adrs"] = b64(BLUEPRINT_OK)
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [])

    def test_branches_are_paged_by_hand(self):
        page1 = [{"name": f"b{i}"} for i in range(100)]
        gh = StubGitHub({"branches?per_page=100&page=1": page1, "branches?per_page=100&page=2": [{"name": "last"}]})
        self.assertEqual(len(cd.all_branches(gh)), 101)
        self.assertEqual(gh.asked, ["branches?per_page=100&page=1", "branches?per_page=100&page=2"])

    # --- review round 2 on PR 43: classes found by Codex ------------------------

    def test_non_ascii_letters_pass_and_non_ascii_punctuation_is_reported(self):
        self.write("AGENTS.md", "# A\n\nAuthor Zo\u00eb and Jos\u00e9 are fine.\nA no-break\u00a0space, an en dash \u2013 and a zero-width\u200bspace are not.\n")
        out = self.run_checks(["AGENTS.md"])
        self.assertEqual(out, ["AGENTS.md:4: ascii: non-ASCII character U+00A0"])

    def test_every_non_ascii_punctuation_class_is_reported(self):
        for ch, code in (("\u2013", "2013"), ("\u200b", "200B"), ("\u2192", "2192"), ("\u00a9", "00A9")):
            with self.subTest(code=code):
                self.write("AGENTS.md", f"# A\n\nx{ch}y\n")
                self.assertEqual(self.run_checks(["AGENTS.md"]), [f"AGENTS.md:3: ascii: non-ASCII character U+{code}"])

    def test_retired_source_cannot_be_cited(self):
        self.write("docs/research/r.md", "# R\n\nFact (S01) and a claim (S02).\n\n## Sources\n\n- S01: https://a.test\n- S02: retired, no source\n")
        out = self.run_checks(["docs/research/r.md"])
        self.assertEqual(out, ["docs/research/r.md:3: sources: S02 is retired in docs/research/r.md, so it has no source to cite"])
        self.write("docs/research/r.md", "# R\n\nFact (S01).\n\n## Sources\n\n- S01: https://a.test\n- S02: retired, no source\n")
        self.assertEqual(self.run_checks(["docs/research/r.md"]), [])

    def test_retired_blueprint_source_cannot_be_cited_from_another_document(self):
        self.write("docs/blueprint.md", BLUEPRINT_OK + "\n- S2: retired, no source\n")
        self.write("docs/agents/factory.md", "# F\n\nSee S1 and S2.\n")
        out = self.run_checks(["docs/agents/factory.md"])
        self.assertEqual(out, ["docs/agents/factory.md:3: sources: S2 is retired in docs/blueprint.md, so it has no source to cite"])

    def test_adr_number_below_another_branchs_highest_is_reported(self):
        main = BLUEPRINT_OK.replace("### ADR-002 (option, deferred): Lanes\n\n**Context.** Why.\n\n**Option.** Lanes.\n\n**Status.** Not built (S1).\n\n", "")
        main = main.replace("**Consequences.**\n- Fine.", "**Consequences.**\n- Fine (S1).")
        other = main.replace("## 5. Failure modes", "### ADR-003: Theirs\n\n**Context.** T.\n\n**Options considered.**\n1. T.\n\n**Decision.** T.\n\n**Consequences.**\n- T.\n\n---\n\n## 5. Failure modes")
        gh = StubGitHub({
            "branches?per_page=100&page=1": [{"name": "main"}, {"name": "claude/1-x"}, {"name": "claude/8-adrs"}],
            "contents/docs/blueprint.md?ref=main": b64(main),
            "contents/docs/blueprint.md?ref=claude/8-adrs": b64(other),
            "pulls?state=open&head=owner:claude/8-adrs": [{"created_at": "2026-10-13T10:00:00Z", "number": 8}],
        })
        line = self.line_of("docs/blueprint.md", "### ADR-002 (option")
        # Theirs has ADR-003, ours is ADR-002 and has no pull request yet: we renumber.
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [
            f"docs/blueprint.md:{line}: adr: ADR-002 is below ADR-003, the highest on branch claude/8-adrs; this branch has no open pull request and that one has, so this branch renumbers"])
        # Ours opened first: we keep it, and theirs is the one above ours.
        gh.routes["pulls?state=open&head=owner:claude/1-x"] = [{"created_at": "2026-10-13T09:00:00Z", "number": 1}]
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [])

    def test_number_reserved_below_mains_highest_is_not_a_finding(self):
        # ADR-018's block: main has ADR-009; this branch holds the reserved ADR-002 below it.
        main = BLUEPRINT_OK.replace("### ADR-002 (option, deferred): Lanes", "### ADR-009: Late").replace(
            "**Context.** Why.\n\n**Option.** Lanes.\n\n**Status.** Not built (S1).",
            "**Context.** Why.\n\n**Options considered.**\n1. L.\n\n**Decision.** L (S1).\n\n**Consequences.**\n- Ok.")
        mine = main.replace("### ADR-009: Late", "### ADR-002: Reserved")
        self.write("docs/blueprint.md", mine)
        gh = StubGitHub({
            "branches?per_page=100&page=1": [{"name": "main"}, {"name": "claude/1-x"}, {"name": "claude/9-merged-main"}],
            "contents/docs/blueprint.md?ref=main": b64(main),
            # a branch that merged main holds ADR-009 too; that one is not its allocation
            "contents/docs/blueprint.md?ref=claude/9-merged-main": b64(main),
        })
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [])
        # Another branch adds a later ADR: the reserved block stays reserved, whoever opened first.
        later = main.replace("## 5. Failure modes", "### ADR-019: Later\n\n**Context.** T.\n\n**Options considered.**\n1. T.\n\n**Decision.** T.\n\n**Consequences.**\n- T.\n\n---\n\n## 5. Failure modes")
        gh.routes["branches?per_page=100&page=1"].append({"name": "claude/19-later"})
        gh.routes["contents/docs/blueprint.md?ref=claude/19-later"] = b64(later)
        gh.routes["pulls?state=open&head=owner:claude/19-later"] = [{"created_at": "2026-10-13T08:00:00Z", "number": 2}]
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [])

    def test_grouped_references_share_the_kind_of_their_label(self):
        gh = StubGitHub({"issues/6": {"number": 6, "pull_request": {}}, "issues/24": {"number": 24, "pull_request": {}}, "issues/34": {"number": 34}, "issues/5": {"number": 5}, "issues/7": {"number": 7, "pull_request": {}}})
        self.write("AGENTS.md", "# A\n\nSee PRs #6, #24 and #34. Issues #5 or #7.\nBut PR #6, openai/codex#52549 and #34 has no group.\nAnd issue #5, PR #6, #24 are three.\n")
        out = self.run_checks(["AGENTS.md"], gh=gh)
        self.assertEqual(out, [
            "AGENTS.md:3: refs: #34 is called a pull request but is an issue",
            "AGENTS.md:3: refs: #7 is called an issue but is a pull request",
        ])

    def test_dates_outside_the_2000s_are_checked_too(self):
        self.write("AGENTS.md", "# A\n\nRetires 2100-01-01 and 2100-13-01 and 1999-12-31.\n")
        out = self.run_checks(["AGENTS.md"])
        self.assertEqual(out, [
            "AGENTS.md:3: date: 2100-01-01 is after today (2026-10-13)",
            "AGENTS.md:3: date-invalid: 2100-13-01 is not a calendar date",
        ])

    def test_in_page_link_anchors_are_not_issue_references(self):
        gh = StubGitHub({"issues/8": {"number": 8}})
        self.write("AGENTS.md", "# A\n\nSee [modes](#5-failure-modes), [x](#12-Notes), [y](#12-3-options), <a href=\"#9-z\">z</a>, <a href='#12-3-options'>q</a>, <a href=#13-x>r</a>, <A HREF = '#14-y'>s</A>, a dash&#8212;written as an entity, and issue #8.\n\n[ref]: #7-reference-style\n")
        self.assertEqual(self.run_checks(["AGENTS.md"], gh=gh), [])
        self.assertEqual(gh.asked, ["issues/8"])

    def test_invalid_date_cannot_be_waived_but_a_future_one_can(self):
        self.write("AGENTS.md", "# A\n\nPlanned for 2026-13-01. <!-- check-docs: allow date: planned -->\nPlanned for 2026-12-01. <!-- check-docs: allow date: planned -->\n")
        out = self.run_checks(["AGENTS.md"])
        self.assertEqual(out, ["AGENTS.md:3: date-invalid: 2026-13-01 is not a calendar date"])

    def test_every_markdown_file_is_discovered_except_pinned_third_party_skills(self):
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        for path in ("SECURITY.md", "docs/new/x.md", ".agents/skills/mine/SKILL.md", ".agents/skills/theirs/SKILL.md", ".agents/skills/theirs/OTHER.md"):
            self.write(path, "# T\n")
        self.write("skills-lock.json", '{"version": 1, "skills": {"theirs": {"source": "x"}}}')
        subprocess.run(["git", "add", "-A"], cwd=self.root, check=True)
        self.assertEqual(sorted(cd.tracked_markdown(self.root)), [".agents/skills/mine/SKILL.md", "SECURITY.md", "docs/blueprint.md", "docs/new/x.md"])

    def test_heading_reference_must_name_the_whole_heading(self):
        self.write("AGENTS.md", "# A\n\n## Before opening a pull request\n\n## Notes: more\n")
        self.write("docs/agents/factory.md", '# F\n\nSee AGENTS.md "Before", AGENTS.md "Before opening a pull request" and AGENTS.md "Notes".\n')
        out = self.run_checks(["docs/agents/factory.md"])
        self.assertEqual(out, ['docs/agents/factory.md:3: xref: AGENTS.md has no heading "Before"'])

    def test_equal_pull_request_timestamps_are_broken_by_number(self):
        main = BLUEPRINT_OK.replace("### ADR-002 (option, deferred): Lanes\n\n**Context.** Why.\n\n**Option.** Lanes.\n\n**Status.** Not built (S1).\n\n", "")
        main = main.replace("**Consequences.**\n- Fine.", "**Consequences.**\n- Fine (S1).")
        other = main.replace("## 5. Failure modes", "### ADR-002: Theirs\n\n**Context.** T.\n\n**Options considered.**\n1. T.\n\n**Decision.** T.\n\n**Consequences.**\n- T.\n\n---\n\n## 5. Failure modes")
        same = "2026-10-13T10:00:00Z"
        gh = StubGitHub({
            "branches?per_page=100&page=1": [{"name": "main"}, {"name": "claude/1-x"}, {"name": "claude/8-adrs"}],
            "contents/docs/blueprint.md?ref=main": b64(main),
            "contents/docs/blueprint.md?ref=claude/8-adrs": b64(other),
            "pulls?state=open&head=owner:claude/1-x": [{"created_at": same, "number": 5}],
            "pulls?state=open&head=owner:claude/8-adrs": [{"created_at": same, "number": 7}],
        })
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [])  # the lower number keeps it
        gh.routes["pulls?state=open&head=owner:claude/1-x"] = [{"created_at": same, "number": 9}]
        out = self.run_checks(["docs/blueprint.md"], gh=gh)
        self.assertEqual(len(out), 1)
        self.assertIn("that branch's pull request opened first, so this branch renumbers", out[0])

    def test_explicit_paths_are_normalised_to_repository_relative(self):
        cwd = self.root / "docs"
        self.assertEqual(cd.repo_relative(self.root, self.root, "./docs/blueprint.md"), "docs/blueprint.md")
        self.assertEqual(cd.repo_relative(self.root, self.root, str(self.root / "docs" / "blueprint.md")), "docs/blueprint.md")
        self.assertEqual(cd.repo_relative(self.root, cwd, "blueprint.md"), "docs/blueprint.md")
        self.assertEqual(cd.repo_relative(self.root, cwd, "../docs/../docs/blueprint.md"), "docs/blueprint.md")
        with self.assertRaises(ValueError):
            cd.repo_relative(self.root, self.root, "../elsewhere.md")

    def test_cli_checks_the_blueprint_whatever_way_its_path_is_spelled(self):
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        self.write("docs/blueprint.md", BLUEPRINT_OK.replace("**Decision.** A.\n\n", ""))
        env = {k: v for k, v in os.environ.items() if k not in ("GITHUB_HEAD_REF", "GITHUB_REF_NAME", "GITHUB_EVENT_PATH")}
        script = str(HERE.parent / "check_docs.py")
        for spelling in ("docs/blueprint.md", "./docs/blueprint.md", str(self.root / "docs" / "blueprint.md")):
            with self.subTest(spelling=spelling):
                r = subprocess.run([sys.executable, script, "--offline", "--today", "2026-10-13", spelling], cwd=self.root, env=env, capture_output=True, text=True)
                self.assertEqual(r.returncode, 1, r.stdout + r.stderr)
                self.assertIn("docs/blueprint.md:", r.stdout)
                self.assertIn("adr-format: missing **Decision.**", r.stdout)

    def test_ordinary_adr_with_optional_in_its_title_keeps_the_ordinary_format(self):
        text = BLUEPRINT_OK.replace("### ADR-001: First", "### ADR-001: First (optional extras)")
        self.write("docs/blueprint.md", text)
        self.assertEqual(self.run_checks(["docs/blueprint.md"]), [])
        self.write("docs/blueprint.md", text.replace("**Decision.** A.\n\n", ""))
        n = self.line_of("docs/blueprint.md", "### ADR-001")
        self.assertEqual(self.run_checks(["docs/blueprint.md"]), [f"docs/blueprint.md:{n}: adr-format: missing **Decision.**"])

    def test_malformed_adr_headings_are_reported(self):
        for heading in ("### ADR-12: Short", "### ADR-0012: Long", "## ADR-003: Wrong level", "### ADR 004: No hyphen", "### adr-005: Lower case", "### ADR- 006: Space after hyphen"):
            with self.subTest(heading=heading):
                text = BLUEPRINT_OK.replace("## 5. Failure modes", heading + "\n\n**Context.** C.\n\n---\n\n## 5. Failure modes")
                self.write("docs/blueprint.md", text)
                out = self.run_checks(["docs/blueprint.md"])
                # A heading that is not canonical is also not a defined ADR, so a reference to it may add its own finding.
                self.assertIn(f"docs/blueprint.md:{self.line_of('docs/blueprint.md', heading)}: adr-format: heading is not in the form `### ADR-NNN` (three digits)", out)

    def test_duplicate_adr_sections_are_reported(self):
        text = BLUEPRINT_OK.replace("**Decision.** A.\n\n", "**Decision.** A.\n\n**Decision.** B, after a bad merge.\n\n")
        self.write("docs/blueprint.md", text)
        n = self.line_of("docs/blueprint.md", "### ADR-001")
        self.assertEqual(self.run_checks(["docs/blueprint.md"]), [f"docs/blueprint.md:{n}: adr-format: **Decision.** starts 2 paragraphs; expected one"])

    def test_headings_inside_fenced_code_are_not_headings(self):
        fenced = BLUEPRINT_OK + "\n```md\n## 9. Example\n\n### ADR-050: Shown only\n```\n\n~~~~\n## 8. Tilde example\n~~~\n## 7. Still inside, the fence is longer\n~~~~\n"
        self.write("docs/blueprint.md", fenced)
        self.write("docs/agents/factory.md", "# F\n\nSee docs/blueprint.md section 9, docs/blueprint.md section 8, docs/blueprint.md section 7 and ADR-050.\n\nReal: docs/blueprint.md section 4.\n")
        out = self.run_checks(["docs/agents/factory.md", "docs/blueprint.md"])
        self.assertEqual(out, [
            "docs/agents/factory.md:3: adr: ADR-050 has no heading in docs/blueprint.md",
            "docs/agents/factory.md:3: xref: docs/blueprint.md has no section 7",
            "docs/agents/factory.md:3: xref: docs/blueprint.md has no section 8",
            "docs/agents/factory.md:3: xref: docs/blueprint.md has no section 9",
        ])

    def test_an_unclosed_fence_runs_to_the_end_of_the_document(self):
        doc = cd.Doc("x.md", ["# A", "```", "## B", "text"])
        self.assertEqual(doc.headings(), ["A"])
        self.assertEqual(doc.fenced(), [False, True, True, True])

    def test_branch_is_needed_only_when_the_blueprint_is_checked(self):
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        self.write("README.md", "# R\n\nSee issue #1.\n")
        subprocess.run(["git", "add", "README.md", "docs/blueprint.md"], cwd=self.root, check=True)
        subprocess.run(["git", "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "x"], cwd=self.root, check=True)
        subprocess.run(["git", "checkout", "-q", "--detach"], cwd=self.root, check=True)
        fake = self.root / "bin"
        fake.mkdir()
        gh = fake / "gh"
        gh.write_text("#!/bin/sh\necho '{\"number\": 1}'\n")
        gh.chmod(0o755)
        env = {k: v for k, v in os.environ.items() if k not in ("GITHUB_HEAD_REF", "GITHUB_REF_NAME", "GITHUB_EVENT_PATH")}
        env.update(PATH=f"{fake}{os.pathsep}{os.environ['PATH']}", GIT_DIR=str(self.root / ".git"), GIT_WORK_TREE=str(self.root))
        script = str(HERE.parent / "check_docs.py")
        readme = subprocess.run([sys.executable, script, "--today", "2026-10-13", "README.md"], cwd=self.root, env=env, capture_output=True, text=True)
        self.assertEqual(readme.returncode, 0, readme.stdout + readme.stderr)
        self.assertIn("with GitHub", readme.stdout)
        both = subprocess.run([sys.executable, script, "--today", "2026-10-13", "README.md", "docs/blueprint.md"], cwd=self.root, env=env, capture_output=True, text=True)
        self.assertEqual(both.returncode, 2, both.stdout + both.stderr)
        self.assertIn("cannot tell the current branch", both.stderr)

    # --- fenced examples and fork pull requests -------------------------------

    def test_fenced_examples_do_not_truncate_or_duplicate_adr_parts(self):
        for fence in ("```md", "~~~~"):
            with self.subTest(fence=fence):
                closing = "```" if fence.startswith("`") else "~~~~"
                example = f"{fence}\n## Example\n---\n**Decision.** Example only.\n{closing}\n\n"
                text = BLUEPRINT_OK.replace("**Options considered.**", example + "**Options considered.**")
                text = text.replace("**Option.**", example + "**Option.**")
                self.write("docs/blueprint.md", text)
                self.assertEqual(self.run_checks(["docs/blueprint.md"]), [])

    def test_fenced_adr_parts_cannot_satisfy_required_sections(self):
        self.write("docs/blueprint.md", BLUEPRINT_OK.replace("**Decision.** A.", "```md\n**Decision.** A.\n```"))
        n = self.line_of("docs/blueprint.md", "### ADR-001")
        self.assertEqual(self.run_checks(["docs/blueprint.md"]), [
            f"docs/blueprint.md:{n}: adr-format: missing **Decision.**",
        ])

    def test_fenced_source_definitions_cannot_resolve_prose_citations(self):
        for definition in ("- S99: https://example.test", "| S99 | https://example.test |", "- S99: retired, no source"):
            with self.subTest(definition=definition):
                self.write("README.md", f"# R\n\n```md\n{definition}\n```\n\nClaim (S99).\n")
                self.assertEqual(self.run_checks(["README.md"]), [
                    "README.md:7: sources: S99 is not defined in docs/blueprint.md",
                ])

    def test_fenced_retirement_and_duplicates_do_not_change_real_sources(self):
        self.write("README.md", "# R\n\nClaim (S1).\n\n- S1: https://example.test\n\n~~~md\n- S1: retired, example only\n| S1 | duplicate example |\n~~~\n")
        self.assertEqual(self.run_checks(["README.md"]), [])

    def test_fenced_citations_neither_count_as_usage_nor_report_undefined_sources(self):
        self.write("README.md", "# R\n\n- S1: https://example.test\n\n```md\nExample (S1, S99).\n```\n")
        self.assertEqual(self.run_checks(["README.md"]), [
            "README.md:3: sources: S1 is defined but never referenced (write `- S1: retired ...` to keep the number without a source)",
        ])

    def test_heading_references_cover_arbitrary_repository_markdown_paths(self):
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        for path in ("SECURITY.md", ".github/CONTRIBUTING.md", "guides/nested/guide-v2.md"):
            self.write(path, "# Guide\n\n## Reporting\n")
            subprocess.run(["git", "add", path], cwd=self.root, check=True)
            for suffix in ("", ",", "'s"):
                with self.subTest(path=path, suffix=suffix):
                    self.write("README.md", f'# R\n\nSee {path}{suffix} "Reporting".\nSee {path}{suffix} "Missing".\n')
                    self.assertEqual(self.run_checks(["README.md"]), [
                        f'README.md:4: xref: {path} has no heading "Missing"',
                    ])
        self.write("README.md", '# R\n\nSee MISSING.md "Reporting".\n')
        self.assertEqual(self.run_checks(["README.md"]), ['README.md:3: xref: MISSING.md does not exist'])

    def test_fenced_cross_references_are_examples_and_urls_are_not_local_paths(self):
        self.write("README.md", '# R\n\n```md\nMISSING.md "Reporting", GOVERNANCE "Missing", docs/blueprint.md section 99 and issue #99.\n```\n\nSee https://example.test/SECURITY.md "Reporting".\n')
        gh = StubGitHub({})
        self.assertEqual(self.run_checks(["README.md"], gh=gh), [])
        self.assertEqual(gh.asked, [])

    def test_fork_pr_owner_is_preserved_from_event_through_collision_ordering(self):
        main = "### ADR-001: First\n"
        other = main + "### ADR-002: Other\n"
        self.write("event.json", json.dumps({"pull_request": {"head": {
            "ref": "topic", "repo": {"owner": {"login": "contributor"}},
        }}}))
        env = {"GITHUB_HEAD_REF": "topic", "GITHUB_EVENT_PATH": str(self.root / "event.json")}
        gh = StubGitHub({
            "branches?per_page=100&page=1": [{"name": "main"}, {"name": "topic"}],
            "contents/docs/blueprint.md?ref=main": b64(main),
            "contents/docs/blueprint.md?ref=topic": b64(other),
            "pulls?state=open&head=contributor:topic": [{"created_at": "2026-10-13T09:00:00Z", "number": 1}],
            "pulls?state=open&head=owner:topic": [{"created_at": "2026-10-13T10:00:00Z", "number": 2}],
        })
        with patch.dict(os.environ, env, clear=True):
            for checkout in ("HEAD", "topic"):
                with self.subTest(checkout=checkout), patch.object(cd.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, checkout + "\n")):
                    branch = cd.current_branch(self.root)
                    self.assertEqual(branch, "contributor:topic")
                    self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh, branch=branch), [])
            self.assertIn("contents/docs/blueprint.md?ref=topic", gh.asked)
            self.assertIn("pulls?state=open&head=contributor:topic", gh.asked)
            gh.routes["pulls?state=open&head=contributor:topic"][0]["created_at"] = "2026-10-13T11:00:00Z"
            out = self.run_checks(["docs/blueprint.md"], gh=gh, branch=branch)
            self.assertEqual(len(out), 1)
            self.assertIn("that branch's pull request opened first, so this branch renumbers", out[0])

    def test_unreadable_fork_event_fails_instead_of_guessing_the_owner(self):
        env = {"GITHUB_HEAD_REF": "topic", "GITHUB_EVENT_PATH": str(self.root / "missing.json")}
        with patch.dict(os.environ, env, clear=True), patch.object(cd.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, "HEAD\n")):
            with self.assertRaises(cd.Unavailable):
                cd.current_branch(self.root)

    def test_unrelated_checked_out_branch_does_not_inherit_event_owner(self):
        env = {"GITHUB_HEAD_REF": "topic", "GITHUB_EVENT_PATH": str(self.root / "missing.json")}
        with patch.dict(os.environ, env, clear=True), patch.object(cd.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, "local-work\n")):
            self.assertEqual(cd.current_branch(self.root), "local-work")

    # --- hidden prose, heading syntax and complete ADR collision discovery -----

    def test_html_comments_cannot_define_sources_or_supply_citation_usage(self):
        self.write("README.md", "# R\n\nClaim (S99).\n<!--\n- S99: https://example.test\n-->\n")
        self.assertEqual(self.run_checks(["README.md"]), [
            "README.md:3: sources: S99 is not defined in docs/blueprint.md",
        ])
        self.write("README.md", "# R\n\n- S1: https://example.test\n<!-- S1 -->\n")
        self.assertEqual(self.run_checks(["README.md"]), [
            "README.md:3: sources: S1 is defined but never referenced (write `- S1: retired ...` to keep the number without a source)",
        ])

    def test_hidden_adr_parts_headings_and_references_are_not_prose(self):
        self.write("docs/blueprint.md", BLUEPRINT_OK.replace("**Decision.** A.", "<!--\n**Decision.** A.\n-->"))
        self.write("README.md", '# R\n<!--\n## Hidden\nADR-999, S999, issue #999 and MISSING.md "Hidden".\n-->\nSee README.md "Hidden".\n')
        gh = StubGitHub({})
        self.assertEqual(self.run_checks(["README.md"], gh=gh), [
            'README.md:6: xref: README.md has no heading "Hidden"',
        ])
        self.assertEqual(gh.asked, [])
        n = self.line_of("docs/blueprint.md", "### ADR-001")
        self.assertEqual(self.run_checks(["docs/blueprint.md"]), [
            f"docs/blueprint.md:{n}: adr-format: missing **Decision.**",
        ])

    def test_inline_and_multiline_comments_preserve_visible_references_and_lines(self):
        self.write("README.md", "# R\n<!-- S999 --> See S1. <!-- S888 -->\n<!-- hidden\nS777 --> Claim S404. <!-- S666 -->\n<!-- unclosed S555\nS444\n")
        self.assertEqual(self.run_checks(["README.md"]), [
            "README.md:4: sources: S404 is not defined in docs/blueprint.md",
        ])

    def test_fences_in_comments_and_comments_in_fences_do_not_hide_later_prose(self):
        self.write("README.md", "# R\n<!--\n```md\n-->\n```html <!--\n<!-- example\n```\nClaim S404.\n")
        self.assertEqual(self.run_checks(["README.md"]), [
            f"README.md:{self.line_of('README.md', 'Claim S404')}: sources: S404 is not defined in docs/blueprint.md",
        ])

    def test_date_waiver_survives_semantic_comment_masking(self):
        self.write("README.md", "# R\nPlanned 2027-01-01. <!-- check-docs: allow date: planned release -->\n")
        self.assertEqual(self.run_checks(["README.md"]), [])

    def test_empty_html_comment_forms_do_not_hide_following_citations(self):
        for comment in ("<!-->", "<!--->", "<!---->"):
            with self.subTest(comment=comment):
                self.write("README.md", f"# R\n{comment} Claim S404.\n")
                self.assertEqual(self.run_checks(["README.md"]), [
                    "README.md:2: sources: S404 is not defined in docs/blueprint.md",
                ])

    def test_literal_comment_openers_do_not_hide_following_citations(self):
        for example in ("Use `<!--` as the opener.", "Use `` ` <!-- `` as the opener.", "Use `a\n<!--` as the opener.", "Use \\<!-- as the opener.", "Use ` unmatched text."):
            with self.subTest(example=example):
                self.write("README.md", "# R\n" + example + "\nClaim S404.\n")
                self.assertEqual(self.run_checks(["README.md"]), [
                    f"README.md:{self.line_of('README.md', 'Claim S404')}: sources: S404 is not defined in docs/blueprint.md",
                ])
        self.write("README.md", "# R\nLiteral `<!--` then <!-- S999 --> claim S404.\n")
        self.assertEqual(self.run_checks(["README.md"]), [
            "README.md:2: sources: S404 is not defined in docs/blueprint.md",
        ])

    def test_atx_heading_indentation_and_closing_hashes_resolve(self):
        for heading in ("## Reporting", " ## Reporting", "  ## Reporting", "   ## Reporting", "## Reporting ##", "   ##\tReporting\t###\t"):
            with self.subTest(heading=heading):
                self.write("SECURITY.md", heading + "\n")
                self.write("README.md", 'See SECURITY.md "Reporting".\n')
                self.assertEqual(self.run_checks(["README.md"]), [])

    def test_atx_normalization_preserves_literal_hashes_and_rejects_non_headings(self):
        doc = cd.Doc("SECURITY.md", ["    ## Indented code", "##No space", "####### Too many", "## Reporting#", "## Reporting \\#", "##", "## ###"])
        self.assertEqual(doc.headings(), ["Reporting#", "Reporting \\#", "", ""])

    def test_source_descriptions_validate_referenced_sources_in_lists_and_tables(self):
        for definition in ("- S1: https://example.test (cross-check S404)", "| S1 | https://example.test (cross-check S404) |"):
            with self.subTest(definition=definition):
                self.write("README.md", "# R\nClaim S1.\n" + definition + "\n")
                self.assertEqual(self.run_checks(["README.md"]), [
                    "README.md:3: sources: S404 is not defined in README.md",
                ])
        self.write("README.md", "# R\nClaim S1.\n- S1: https://example.test (cross-check S2)\n- S2: https://other.test\n")
        self.assertEqual(self.run_checks(["README.md"]), [])
        self.write("README.md", "# R\nClaim S1.\n- S1: https://example.test (cross-check S2)\n- S2: retired, no source\n")
        self.assertEqual(self.run_checks(["README.md"]), [
            "README.md:3: sources: S2 is retired in README.md, so it has no source to cite",
        ])

    def test_stale_branch_conflicting_with_main_must_update_even_without_new_numbers(self):
        main = BLUEPRINT_OK.replace("### ADR-001: First", "### ADR-001: Merged competing decision")
        gh = StubGitHub({"contents/docs/blueprint.md?ref=main": b64(main)})
        n = self.line_of("docs/blueprint.md", "### ADR-001")
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [
            f"docs/blueprint.md:{n}: adr: ADR-001 has a different heading on main; update this branch or renumber the new decision",
        ])
        gh.routes["contents/docs/blueprint.md?ref=main"] = b64(BLUEPRINT_OK)
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [])

    def test_two_fork_pull_requests_are_compared_from_their_source_repositories(self):
        ours = {cd.BLUEPRINT: cd.Doc(cd.BLUEPRINT, ["### ADR-001: First", "### ADR-002: Ours"])}
        pr = {"head": {"ref": "topic", "repo": {"full_name": "other/fork"}}}
        gh = StubGitHub({
            "contents/docs/blueprint.md?ref=main": b64("### ADR-001: First"),
            "branches?per_page=100&page=1": [{"name": "main"}],
            "pulls?state=open&per_page=100&page=1": [pr, pr, {"head": {"repo": None}}],
            "pulls?state=open&head=contributor:topic": [{"created_at": "2026-10-13T11:00:00Z", "number": 2}],
            "pulls?state=open&head=other:topic": [{"created_at": "2026-10-13T10:00:00Z", "number": 1}],
        })
        fork = StubGitHub({"contents/docs/blueprint.md?ref=topic": b64("### ADR-001: First\n### ADR-002: Theirs")})
        with patch.object(cd, "GitHub", return_value=fork) as factory:
            out = cd.check_adr_branch_collisions(ours, gh, "contributor:topic")
            self.assertEqual(len(out), 1)
            self.assertIn("ADR-002 is also new on branch other:topic", out[0].message)
            factory.assert_called_once_with("other/fork")
            self.assertEqual(fork.asked, ["contents/docs/blueprint.md?ref=topic"])
            fork.routes["contents/docs/blueprint.md?ref=topic"] = b64("### ADR-001: First\n### ADR-003: Theirs")
            out = cd.check_adr_branch_collisions(ours, gh, "contributor:topic")
            self.assertEqual(len(out), 1)
            self.assertIn("ADR-002 is below ADR-003", out[0].message)
            gh.routes["pulls?state=open&head=contributor:topic"][0]["created_at"] = "2026-10-13T09:00:00Z"
            self.assertEqual(cd.check_adr_branch_collisions(ours, gh, "contributor:topic"), [])

    def test_open_pull_requests_are_paged_by_hand(self):
        first = [{"number": n} for n in range(100)]
        gh = StubGitHub({"pulls?state=open&per_page=100&page=1": first, "pulls?state=open&per_page=100&page=2": [{"number": 101}]})
        self.assertEqual(cd.all_open_pull_requests(gh), first + [{"number": 101}])
        self.assertEqual(gh.asked, ["pulls?state=open&per_page=100&page=1", "pulls?state=open&per_page=100&page=2"])

    def test_current_fork_head_is_not_compared_with_itself(self):
        ours = {cd.BLUEPRINT: cd.Doc(cd.BLUEPRINT, ["### ADR-002: Ours"])}
        gh = StubGitHub({
            "contents/docs/blueprint.md?ref=main": b64("### ADR-001: First"),
            "pulls?state=open&per_page=100&page=1": [{"head": {"ref": "topic", "repo": {"full_name": "contributor/fork"}}}],
        })
        with patch.object(cd, "GitHub") as factory:
            self.assertEqual(cd.check_adr_branch_collisions(ours, gh, "contributor:topic"), [])
            factory.assert_not_called()

    # --- Markdown block forms and encoded branch names ------------------------

    def test_indented_adr_headings_use_the_same_format_and_identity(self):
        for spaces in range(1, 4):
            with self.subTest(spaces=spaces):
                text = BLUEPRINT_OK.replace("### ADR-", " " * spaces + "### ADR-")
                self.write("docs/blueprint.md", text)
                self.assertEqual(self.run_checks(["docs/blueprint.md"]), [])
                changed = text.replace("ADR-002 (option, deferred): Lanes", "ADR-001 (option, deferred): Lanes")
                self.write("docs/blueprint.md", changed)
                out = self.run_checks(["docs/blueprint.md"])
                self.assertEqual(out, [f"docs/blueprint.md:{self.line_of('docs/blueprint.md', 'Lanes')}: adr: ADR-001 heading repeats line {self.line_of('docs/blueprint.md', 'First')}"])
        self.write("docs/blueprint.md", BLUEPRINT_OK + "\n   ### ADR-12: Typo\n")
        self.assertIn(f"docs/blueprint.md:{self.line_of('docs/blueprint.md', 'Typo')}: adr-format: heading is not in the form `### ADR-NNN` (three digits)", self.run_checks(["docs/blueprint.md"]))

    def test_indented_branch_adr_is_discovered_and_decorations_do_not_change_identity(self):
        ours = {cd.BLUEPRINT: cd.Doc(cd.BLUEPRINT, ["### ADR-001: First", "   ### ADR-002: Ours ##"])}
        gh = StubGitHub({
            "contents/docs/blueprint.md?ref=main": b64("### ADR-001: First"),
            "branches?per_page=100&page=1": [{"name": "other"}],
            "contents/docs/blueprint.md?ref=other": b64("   ### ADR-002: Theirs"),
            "pulls?state=open&head=owner:other": [{"created_at": "2026-10-13T10:00:00Z", "number": 1}],
        })
        out = cd.check_adr_branch_collisions(ours, gh, "ours")
        self.assertEqual(len(out), 1)
        self.assertEqual(out[0].line, 2)
        self.assertIn("ADR-002 is also new on branch other", out[0].message)
        gh.routes["contents/docs/blueprint.md?ref=other"] = b64("### ADR-002: Ours")
        self.assertEqual(cd.check_adr_branch_collisions(ours, gh, "ours"), [])

    def test_adr_cannot_borrow_required_parts_from_a_later_section(self):
        for boundary in ("   ## Later section", "   # Later section ##", " ##\tLater section", "Later section\n---", "Later section\n===", "***"):
            with self.subTest(boundary=boundary):
                text = "### ADR-001: First\n\n**Context.** C.\n\n**Options considered.** A.\n\n" + boundary + "\n\n**Decision.** A.\n\n**Consequences.** Fine.\n"
                doc = cd.Doc(cd.BLUEPRINT, text.split("\n"))
                self.assertEqual([f.message for f in cd.check_adr_format(doc)], ["missing **Decision.**", "missing **Consequences.**"])

    def test_setext_heading_references_resolve_single_and_multiline_titles(self):
        for heading in ("Reporting\n=========", "Reporting\n-", "   Reporting\n   ---   ", "Reporting\nprocedure\n==="):
            with self.subTest(heading=heading):
                self.write("GUIDE.md", heading + "\n")
                wanted = "Reporting procedure" if "procedure" in heading else "Reporting"
                self.write("README.md", f'See GUIDE.md "{wanted}".\n')
                self.assertEqual(self.run_checks(["README.md"]), [])

    def test_setext_headings_do_not_cross_blank_lines_code_comments_or_other_blocks(self):
        for example in ("Reporting\n\n===", "```md\nReporting\n===\n```", "<!--\nReporting\n===\n-->", "    Reporting\n    ===", "- Reporting\n===", "Reporting\n\n```\nexample\n```\n==="):
            with self.subTest(example=example):
                self.write("GUIDE.md", example + "\n")
                self.write("README.md", 'See GUIDE.md "Reporting".\n')
                self.assertEqual(self.run_checks(["README.md"]), ['README.md:1: xref: GUIDE.md has no heading "Reporting"'])

    def test_xrefs_ignore_link_destinations_and_titles_but_keep_visible_labels(self):
        self.write("AGENTS.md", "# Rules\n")
        self.write("README.md", '# R\n[rules](AGENTS.md "Tooltip")\n![image](GUIDE.md "Tooltip")\n[rules](path(with(parentheses)).md \'GUIDE.md "Missing"\')\n[rules](x.md \'text ) GUIDE.md "Missing"\')\n[ref]: GUIDE.md \'AGENTS.md "Missing"\'\n[Visible AGENTS.md "Missing"](https://example.test)\nSee AGENTS.md "Missing".\n')
        out = self.run_checks(["README.md"])
        self.assertEqual(out, [
            f"README.md:{self.line_of('README.md', '[Visible')}: xref: AGENTS.md has no heading \"Missing\"",
            f"README.md:{self.line_of('README.md', 'See AGENTS')}: xref: AGENTS.md has no heading \"Missing\"",
        ])

    def test_branch_refs_are_encoded_for_contents_and_pull_request_queries(self):
        from urllib.parse import parse_qs, urlsplit

        ours = {cd.BLUEPRINT: cd.Doc(cd.BLUEPRINT, ["### ADR-002: Ours"])}
        for branch, encoded in (("topic#draft", "topic%23draft"), ("topic&x=1", "topic%26x%3D1"), ("topic+percent%", "topic%2Bpercent%25")):
            with self.subTest(branch=branch):
                gh = StubGitHub({
                    "contents/docs/blueprint.md?ref=main": b64("### ADR-001: First"),
                    "branches?per_page=100&page=1": [{"name": branch}],
                    f"contents/docs/blueprint.md?ref={encoded}": b64("### ADR-002: Theirs"),
                    f"pulls?state=open&head=owner:{encoded}": [{"created_at": "2026-10-13T10:00:00Z", "number": 1}],
                })
                self.assertEqual(len(cd.check_adr_branch_collisions(ours, gh, "ours")), 1)
                for route in gh.asked:
                    if encoded in route:
                        query = parse_qs(urlsplit(route).query)
                        self.assertEqual(query.get("ref", query.get("head")), [branch] if "ref" in query else ["owner:" + branch])
                fork = StubGitHub({f"contents/docs/blueprint.md?ref={encoded}": b64("### ADR-002: Fork")})
                gh.routes["branches?per_page=100&page=1"] = []
                gh.routes["pulls?state=open&per_page=100&page=1"] = [{"head": {"ref": branch, "repo": {"full_name": "other/fork"}}}]
                gh.routes[f"pulls?state=open&head=other:{encoded}"] = [{"created_at": "2026-10-13T10:00:00Z", "number": 1}]
                with patch.object(cd, "GitHub", return_value=fork):
                    self.assertEqual(len(cd.check_adr_branch_collisions(ours, gh, "contributor:" + branch)), 1)
                self.assertEqual(fork.asked, [f"contents/docs/blueprint.md?ref={encoded}"])
                self.assertIn(f"pulls?state=open&head=contributor:{encoded}", gh.asked)

    def test_indented_code_blocks_are_excluded_from_all_semantic_checks(self):
        for indent in ("    ", "\t", "  \t"):
            with self.subTest(indent=repr(indent)):
                example = ["#999999 example output", "ADR-999 and S999", 'MISSING.md "Missing"', "<!--", "```md"]
                self.write("README.md", "# R\n\n" + "\n".join(indent + line for line in example) + "\n\nVisible S1.\n")
                gh = StubGitHub({})
                self.assertEqual(self.run_checks(["README.md"], gh=gh), [])
                self.assertEqual(gh.asked, [])

    def test_indentation_does_not_hide_paragraph_or_list_continuations(self):
        for text in ("Paragraph\n    Claim S404.", "- Item\n\n    Claim S404.", "- Item\n    - Nested\n\n        Claim S404."):
            with self.subTest(text=text):
                self.write("README.md", text + "\n")
                self.assertEqual(self.run_checks(["README.md"]), [
                    f"README.md:{self.line_of('README.md', 'Claim S404')}: sources: S404 is not defined in docs/blueprint.md",
                ])
        self.write("README.md", "- Item\n\n      S999 is code.\n\nVisible S1.\n")
        self.assertEqual(self.run_checks(["README.md"]), [])

    def test_list_fences_hide_examples_and_stop_at_their_container(self):
        for example in (
            "- Example:\n\n    ```md\n    S999 example.\n    ```",
            "- Example:\n\n\t~~~md\n\tS999 example.\n\t~~~",
            "- Outer:\n    - Inner:\n\n        ```md\n        S999 example.\n        ```",
            "- ```md\n  S999 example.\n  ```",
            "- Example:\n\n    ```md\n    S999 example.",
        ):
            with self.subTest(example=example):
                self.write("README.md", example + "\n\nVisible S404.\n")
                self.assertEqual(self.run_checks(["README.md"]), [
                    f"README.md:{self.line_of('README.md', 'Visible S404')}: sources: S404 is not defined in docs/blueprint.md",
                ])

    def test_blockquote_code_is_excluded_without_hiding_following_prose(self):
        for example in (
            "> ```md\n> S999 example.\n> ```",
            "> > ~~~md\n> > S999 example.\n> > ~~~",
            "> ```md\n> S999 example.",
            ">     S999 example.",
            "> - Item:\n>\n>     ```md\n>     S999 example.\n>     ```",
            "- Item:\n\n  > ```md\n  > S999 example.\n  > ```",
            "- > ```md\n  > S999 example.\n  > ```",
        ):
            with self.subTest(example=example):
                self.write("README.md", example + "\n\n> Visible S404.\nOutside S405.\n")
                self.assertEqual(self.run_checks(["README.md"]), [
                    f"README.md:{self.line_of('README.md', 'Visible S404')}: sources: S404 is not defined in docs/blueprint.md",
                    f"README.md:{self.line_of('README.md', 'Outside S405')}: sources: S405 is not defined in docs/blueprint.md",
                ])

    def test_new_zero_adr_cannot_bypass_main_allocation(self):
        ours = {cd.BLUEPRINT: cd.Doc(cd.BLUEPRINT, ["### ADR-000: New"])}
        gh = StubGitHub({"contents/docs/blueprint.md?ref=main": b64("### ADR-001: First")})
        self.assertEqual([f.message for f in cd.check_adr_branch_collisions(ours, gh, "ours")], [
            "ADR-000 is below ADR-001, the highest on main; renumber the new decision",
        ])
        gh.routes["contents/docs/blueprint.md?ref=main"] = b64("### ADR-000: New")
        self.assertEqual(cd.check_adr_branch_collisions(ours, gh, "ours"), [])

    def test_reference_scanners_ignore_link_targets_but_keep_labels(self):
        self.write("README.md", '# R\n[upstream](https://example.test/S404/ADR-999 "S405 ADR-12")\n[ref]: https://example.test/S406/ADR-998 "S407"\n<https://example.test/S408/ADR-997>\n[Visible S409 and ADR-996](https://example.test)\n')
        self.assertEqual(self.run_checks(["README.md"]), [
            "README.md:5: adr: ADR-996 has no heading in docs/blueprint.md",
            "README.md:5: sources: S409 is not defined in docs/blueprint.md",
        ])
        self.write("README.md", '- S1: [upstream](https://example.test/S1)\n')
        self.assertEqual(self.run_checks(["README.md"]), [
            "README.md:1: sources: S1 is defined but never referenced (write `- S1: retired ...` to keep the number without a source)",
        ])

    def test_invalid_backtick_fence_openers_do_not_hide_references(self):
        for prefix in ("", "   ", "> ", "- "):
            with self.subTest(prefix=prefix):
                self.write("README.md", prefix + "``` bad` info\n#999999 and S404.\n")
                gh = StubGitHub({})
                out = self.run_checks(["README.md"], gh=gh)
                self.assertEqual(len(out), 2)
                self.assertTrue(any(": refs: #999999 does not exist" in f for f in out))
                self.assertIn("README.md:2: sources: S404 is not defined in docs/blueprint.md", out)
                self.assertEqual(gh.asked, ["issues/999999"])
        self.write("README.md", "~~~ bad` info\nS404\n~~~\n")
        self.assertEqual(self.run_checks(["README.md"]), [])

    def test_inline_code_references_are_examples_and_preserve_visible_prose(self):
        for example in (
            '`#999999 S999 ADR-999 ADR-12 MISSING.md "Missing"`',
            '``#999999 ` S999 ADR-999``',
            '`#999999\nS999 ADR-999`',
            '> `#999999 S999 ADR-999`',
            '- `#999999 S999 ADR-999`',
        ):
            with self.subTest(example=example):
                self.write("README.md", example + " S404.\n")
                gh = StubGitHub({})
                self.assertEqual(self.run_checks(["README.md"], gh=gh), [
                    f"README.md:{self.line_of('README.md', ' S404.')}: sources: S404 is not defined in docs/blueprint.md",
                ])
                self.assertEqual(gh.asked, [])
        self.write("README.md", "- S1: https://example.test\n\n`S1`\n")
        self.assertEqual(self.run_checks(["README.md"]), [
            "README.md:1: sources: S1 is defined but never referenced (write `- S1: retired ...` to keep the number without a source)",
        ])

    def test_inline_code_does_not_erase_heading_titles_or_unmatched_prose(self):
        doc = cd.Doc("GUIDE.md", ['# Use `client`', '### ADR-001: Use `client`'])
        self.assertEqual(doc.headings(), ['Use `client`', 'ADR-001: Use `client`'])
        self.assertEqual(cd.adr_titles(doc), {'001': '### ADR-001: Use `client`'})
        for text in ('Unmatched ` S404.', 'Escaped \u005c\u005c` S404.', '`code`\n    S404.'):
            with self.subTest(text=text):
                self.write("README.md", text + "\n")
                self.assertEqual(self.run_checks(["README.md"]), [
                    f"README.md:{self.line_of('README.md', 'S404')}: sources: S404 is not defined in docs/blueprint.md",
                ])

    def test_malformed_adr_references_are_reported_without_heading_or_link_noise(self):
        for reference in ('ADR-12', 'ADR-0012', 'ADR 012', 'adr-012', 'ADR_012', 'ADR- 012'):
            with self.subTest(reference=reference):
                self.write("README.md", f"See {reference}.\n")
                self.assertEqual(self.run_checks(["README.md"]), [
                    f"README.md:1: adr: {reference} is not in the form ADR-NNN (three digits)",
                ])
        self.write("README.md", "See ADR-001 and [upstream](https://example.test/ADR-12).\n")
        self.assertEqual(self.run_checks(["README.md"]), [])

    def test_inline_code_cannot_cross_heading_or_container_boundaries(self):
        for text in ('# `S999\nS404`', '` S999\n- S404\n`', '` S999\n> S404\n`',
                     '` S999\n---\nS404`', '- `S999\n- S404`'):
            with self.subTest(text=text):
                self.write("README.md", text + "\n")
                self.assertEqual(self.run_checks(["README.md"]), sorted([
                    f"README.md:{self.line_of('README.md', 'S999')}: sources: S999 is not defined in docs/blueprint.md",
                    f"README.md:{self.line_of('README.md', 'S404')}: sources: S404 is not defined in docs/blueprint.md",
                ]))

    def test_adr_parts_must_start_separate_paragraphs(self):
        parts = ["**Context.** C.", "**Options considered.** A.", "**Decision.** A.", "**Consequences.** C."]
        for separator in ("\n", "  \n"):
            with self.subTest(separator=separator):
                doc = cd.Doc(cd.BLUEPRINT, ("### ADR-001: First\n\n" + separator.join(parts)).split("\n"))
                self.assertEqual([f.message for f in cd.check_adr_format(doc)], [
                    "missing **Options considered.**", "missing **Decision.**", "missing **Consequences.**",
                ])
        for boundary in ("\n\n", "\n\n```\nexample\n```\n", "\n#### Detail\n"):
            with self.subTest(boundary=boundary):
                doc = cd.Doc(cd.BLUEPRINT, ("### ADR-001: First\n" + boundary.join(parts)).split("\n"))
                self.assertEqual(cd.check_adr_format(doc), [])
        doc = cd.Doc(cd.BLUEPRINT, ("### ADR-001: First\n\n`example`\n" + "\n\n".join(parts)).split("\n"))
        self.assertIn("missing **Context.**", [f.message for f in cd.check_adr_format(doc)])

    def test_multiline_link_targets_are_masked_by_every_reference_check(self):
        for link in (
            '[upstream](\nhttps://example.test/S404/ADR-999/#999999\n)',
            '[upstream](https://example.test\n\'S404 ADR-999 #999999 MISSING.md "Missing"\'\n)',
            '[upstream](https://example.test "S404\nADR-999 #999999\nMISSING.md \'Missing\'")',
            '[ref]:\n  https://example.test/S404/ADR-999/#999999',
            '[ref]: https://example.test\n  "S404 ADR-999 #999999"',
            '[ref]:\n  https://example.test\n  "S404\nADR-999 #999999"',
            '[ref]: https://example.test "S404\nADR-999 #999999"',
            '[ref]: <https://example.test/S404> \'ADR-999\n#999999\'',
        ):
            with self.subTest(link=link):
                self.write("README.md", link + "\n\nVisible S405.\n")
                gh = StubGitHub({})
                self.assertEqual(self.run_checks(["README.md"], gh=gh), [
                    f"README.md:{self.line_of('README.md', 'Visible S405')}: sources: S405 is not defined in docs/blueprint.md",
                ])
                self.assertEqual(gh.asked, [])

    def test_multiline_link_masking_preserves_labels_and_paragraph_boundaries(self):
        for text in ('[Visible S404](\nhttps://example.test\n)', '[upstream](\n\nS404)',
                     '[upstream](\n```\nexample\n```\nS404)', '[ref]: https://example.test\n"title\n\nS404"'):
            with self.subTest(text=text):
                self.write("README.md", text + "\n")
                self.assertEqual(self.run_checks(["README.md"]), [
                    f"README.md:{self.line_of('README.md', 'S404')}: sources: S404 is not defined in docs/blueprint.md",
                ])

    def test_date_waiver_cannot_hide_multiple_future_occurrences(self):
        for text, dates in (
            ('Released 2027-01-01; next review planned 2027-02-01.', ['2027-01-01', '2027-02-01']),
            ('Released 2027-01-01; next review planned 2027-01-01.', ['2027-01-01']),
        ):
            with self.subTest(text=text):
                self.write("README.md", text + ' <!-- check-docs: allow date: planned review -->\n')
                expected = [f"README.md:1: date: {date} is after today (2026-10-13)" for date in dates]
                self.assertEqual(self.run_checks(["README.md"]), expected)
        self.write("README.md", 'Released 2026-10-01; next review planned 2027-02-01. <!-- check-docs: allow date: planned review -->\n')
        self.assertEqual(self.run_checks(["README.md"]), [])
        self.write("README.md", 'Released 2027-01-01.\nNext review planned 2027-02-01. <!-- check-docs: allow date: planned review -->\n')
        self.assertEqual(self.run_checks(["README.md"]), ["README.md:1: date: 2027-01-01 is after today (2026-10-13)"])
        self.write("README.md", 'Invalid 2027-13-01; next review planned 2027-02-01. <!-- check-docs: allow date: planned review -->\n')
        self.assertEqual(self.run_checks(["README.md"]), ["README.md:1: date-invalid: 2027-13-01 is not a calendar date"])

    def test_bare_urls_do_not_create_or_satisfy_source_references(self):
        for definition in ('- S1: https://example.test/S404/ADR-999', '| S1 | https://example.test/S404/ADR-999 |'):
            with self.subTest(definition=definition):
                self.write("README.md", "Claim S1.\n" + definition + " (cross-check S405).\n")
                self.assertEqual(self.run_checks(["README.md"]), ["README.md:2: sources: S405 is not defined in README.md"])
        self.write("README.md", "Claim S1.\n- S1: https://example.test/S404\n- S404: https://other.test\n")
        self.assertEqual(self.run_checks(["README.md"]), [
            "README.md:3: sources: S404 is defined but never referenced (write `- S404: retired ...` to keep the number without a source)",
        ])

    def test_malformed_link_labels_do_not_hide_references(self):
        for text in ('upstream](S404 ADR-999 #999999)', r'\[upstream](S404 ADR-999 #999999)',
                     '[earlier\n\nupstream](S404 ADR-999 #999999)', '[earlier] text](S404 ADR-999 #999999)'):
            with self.subTest(text=text):
                self.write("README.md", text + "\n")
                gh = StubGitHub({})
                out = self.run_checks(["README.md"], gh=gh)
                self.assertEqual(len(out), 3)
                self.assertTrue(any(": adr: ADR-999 has no heading" in f for f in out))
                self.assertTrue(any(": sources: S404 is not defined" in f for f in out))
                self.assertEqual(gh.asked, ["issues/999999"])
        for text in ('[label [nested]](S404)', r'[label \] bracket](S404)', '[![image](S404)](S405)'):
            with self.subTest(text=text):
                self.write("README.md", text + " Visible S406.\n")
                self.assertEqual(self.run_checks(["README.md"]), ["README.md:1: sources: S406 is not defined in docs/blueprint.md"])

    def test_cross_references_cannot_resolve_external_files_or_symlinks(self):
        with tempfile.TemporaryDirectory() as outside:
            external = Path(outside) / "outside.md"
            external.write_text("# Heading\n")
            traversal = "../" + Path(outside).name + "/outside.md"
            (self.root / "escape.md").symlink_to(external)
            for target in (traversal, 'escape.md'):
                with self.subTest(target=target):
                    self.write("README.md", f'See {target} "Heading".\n')
                    self.assertEqual(self.run_checks(["README.md"]), [f"README.md:1: xref: {target} does not exist"])
            self.write("GUIDE.MD", "# Heading\n")
            self.write("README.md", 'See docs/../GUIDE.MD "Heading".\n')
            self.assertEqual(self.run_checks(["README.md"]), [])

    def test_invalid_link_destination_or_title_keeps_references_visible(self):
        for text in ('[details](S404 ADR-999 #999999)', '[details](S404\nADR-999 #999999)',
                     '[details](<S404\nADR-999 #999999>)', '[details](url "S404 ADR-999 #999999)',
                     r'[details](S404\ ADR-999 #999999)'):
            with self.subTest(text=text):
                self.write("README.md", text + "\n")
                gh = StubGitHub({})
                out = self.run_checks(["README.md"], gh=gh)
                self.assertEqual(len(out), 3)
                self.assertTrue(any(": adr: ADR-999 has no heading" in f for f in out))
                self.assertTrue(any(": sources: S404 is not defined" in f for f in out))
                self.assertEqual(gh.asked, ["issues/999999"])

    def test_valid_link_destination_and_title_forms_are_excluded(self):
        for text in ('[details](<S404 ADR-999 #999999>)', '[details](<S404)ADR-999> "#999999")',
                     '[details](S404(and(ADR-999)) "#999999")', '[details](S404 (ADR-999 #999999))',
                     '[details]( "S404 ADR-999 #999999")', r'[details](S404\(ADR-999\) "#999999")'):
            with self.subTest(text=text):
                self.write("README.md", text + " Visible S405.\n")
                gh = StubGitHub({})
                self.assertEqual(self.run_checks(["README.md"], gh=gh), ["README.md:1: sources: S405 is not defined in docs/blueprint.md"])
                self.assertEqual(gh.asked, [])

    def test_date_waivers_require_visible_qualification_for_the_date(self):
        for text in ('Released 2027-01-01.', 'Released 2027-01-01. <!-- planned -->',
                     'Released 2027-01-01; other work is scheduled.', '`Planned` release 2027-01-01.',
                     'Not planned for 2027-01-01.', 'Scheduled 2026-01-01, released 2027-01-01.'):
            with self.subTest(text=text):
                self.write("README.md", text + ' <!-- check-docs: allow date: planned release -->\n')
                self.assertEqual(self.run_checks(["README.md"]), ["README.md:1: date: 2027-01-01 is after today (2026-10-13)"])
        for text in ('Release planned for 2027-01-01.', 'Scheduled release: 2027-01-01.',
                     'Release 2027-01-01 (planned).', 'Retires on 2027-01-01.'):
            with self.subTest(text=text):
                self.write("README.md", text + ' <!-- check-docs: allow date: announced schedule -->\n')
                self.assertEqual(self.run_checks(["README.md"]), [])

    def test_uppercase_markdown_is_discovered_checked_and_cross_referenced(self):
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        self.write("README.MD", "# Heading\nClaim S404.\n")
        self.write("guide.Md", 'See README.MD "Missing".\n')
        subprocess.run(["git", "add", "README.MD", "guide.Md", "docs/blueprint.md"], cwd=self.root, check=True)
        self.assertEqual(sorted(cd.tracked_markdown(self.root)), ["README.MD", "docs/blueprint.md", "guide.Md"])
        script = str(HERE.parent / "check_docs.py")
        env = dict(os.environ, GIT_DIR=str(self.root / ".git"), GIT_WORK_TREE=str(self.root))
        for paths in ([], ["README.MD", "guide.Md"]):
            with self.subTest(paths=paths):
                result = subprocess.run([sys.executable, script, "--offline", "--today", "2026-10-13", *paths], cwd=self.root, env=env, text=True, capture_output=True)
                self.assertEqual(result.returncode, 1, result.stdout + result.stderr)
                self.assertIn("README.MD:2: sources: S404 is not defined", result.stdout)
                self.assertIn('guide.Md:1: xref: README.MD has no heading "Missing"', result.stdout)

    def test_full_reference_links_hide_only_the_defined_destination_label(self):
        for label, definition in (('S404', 'S404'), ('ADR-999', 'adr-999'), ('#999999', '#999999'),
                                  ('S404\n ADR-999', 's404 ADR-999'), (r'S404\]', r's404\]')):
            with self.subTest(label=label):
                self.write("README.md", f'[Visible S405][{label}]\n\n[{definition}]: https://example.test\n')
                gh = StubGitHub({})
                self.assertEqual(self.run_checks(["README.md"], gh=gh), [
                    "README.md:1: sources: S405 is not defined in docs/blueprint.md",
                ])
                self.assertEqual(gh.asked, [])
        for text in ('[S404]', '[S404][]', '[upstream][S404]', '[upstream] [S404]'):
            with self.subTest(text=text):
                # An undefined full-reference label stays literal; shortcut and
                # collapsed labels are themselves the visible link text.
                definitions = '' if text == '[upstream][S404]' else '\n\n[S404]: https://example.test\n'
                self.write("README.md", text + definitions)
                self.assertEqual(self.run_checks(["README.md"]), [
                    "README.md:1: sources: S404 is not defined in docs/blueprint.md",
                ])

    def test_issue_kind_claims_survive_inline_markdown_formatting(self):
        gh = StubGitHub({"issues/12": {"number": 12, "pull_request": {}}, "issues/13": {"number": 13}})
        for text in ('issue [#12](https://example.test)', 'issue **#12**', '**issue** _#12_',
                     'issue [#12][ref]\n\n[ref]: https://example.test', 'issue ~~#12~~',
                     'issues [#13](https://example.test), **#12**'):
            with self.subTest(text=text):
                self.write("README.md", text + "\n")
                self.assertEqual(self.run_checks(["README.md"], gh=gh), [
                    "README.md:1: refs: #12 is called an issue but is a pull request",
                ])
        self.write("README.md", 'PRs **#12** and [#13](https://example.test).\n')
        self.assertEqual(self.run_checks(["README.md"], gh=gh), [
            "README.md:1: refs: #13 is called a pull request but is an issue",
        ])
        self.write("README.md", 'PRs **#12**, openai/codex#1 and **#13**.\n')
        self.assertEqual(self.run_checks(["README.md"], gh=gh), [])

    def test_date_waiver_does_not_apply_to_a_different_predicate(self):
        for text in ('The feature was planned but released on 2027-01-01.',
                     'The release was scheduled and completed on 2027-01-01.',
                     'Planned work was delivered on 2027-01-01.',
                     'Never scheduled for 2027-01-01.', 'No longer planned for 2027-01-01.'):
            with self.subTest(text=text):
                self.write('README.md', text + ' <!-- check-docs: allow date: future schedule -->\n')
                self.assertEqual(self.run_checks(['README.md']), [
                    'README.md:1: date: 2027-01-01 is after today (2026-10-13)',
                ])

    def test_setext_adr_heading_is_rejected_even_if_number_exists(self):
        for underline in ('===', '---'):
            with self.subTest(underline=underline):
                text = BLUEPRINT_OK + '\nADR-001: Duplicate\n' + underline + '\n'
                doc = cd.Doc(cd.BLUEPRINT, text.splitlines())
                findings = cd.check_adr_format(doc)
                self.assertEqual(len(findings), 1)
                self.assertEqual(findings[0].message, 'heading is not in the form `### ADR-NNN` (three digits)')
                self.assertEqual(doc.lines[findings[0].line - 1], 'ADR-001: Duplicate')

    def test_angle_brackets_hide_only_autolinks_and_valid_html(self):
        self.write('README.md', '<#999999> <S404!> <ADR-999!>\n')
        gh = StubGitHub({})
        out = self.run_checks(['README.md'], gh=gh)
        self.assertEqual(len(out), 3)
        self.assertTrue(any('S404 is not defined' in f for f in out))
        self.assertTrue(any('ADR-999 has no heading' in f for f in out))
        self.assertEqual(gh.asked, ['issues/999999'])
        self.write('README.md', 'Inline <S404> <ADR-999> <https://example.test/S405/#12> <S406@example.test> text.\n')
        gh = StubGitHub({})
        self.assertEqual(self.run_checks(['README.md'], gh=gh), [])
        self.assertEqual(gh.asked, [])

    def test_raw_html_blocks_cannot_define_markdown_headings(self):
        for start, end in (('<div>', '</div>'), ('<script>', '</script>'), ('<pre>', '</pre>'),
                           ('<style>', '</style>'), ('<textarea>', '</textarea>'),
                           ('<!--', '-->'), ('<?xml', '?>'), ('<!DECL', '>'),
                           ('<![CDATA[', ']]>'), ('<custom-tag>', '</custom-tag>')):
            with self.subTest(start=start):
                text = start + '\n# Reporting\n### ADR-999: Hidden\n' + end + '\n\n# Visible\n'
                doc = cd.Doc('SECURITY.md', text.splitlines())
                self.assertEqual(doc.headings(), ['Visible'])
                self.assertEqual(cd.adr_headings(doc), {})
                self.write('SECURITY.md', text)
                self.write('README.md', 'SECURITY.md "Reporting"\n')
                self.assertEqual(self.run_checks(['README.md']), [
                    'README.md:1: xref: SECURITY.md has no heading "Reporting"',
                ])
        # Type 7 cannot interrupt prose; type 6 can. Blank lines terminate
        # types 6/7 even before a closing tag, as in Markdown inside details.
        self.assertEqual(cd.Doc('x.md', ['Paragraph', '<span>', '# Visible']).headings(), ['Visible'])
        self.assertEqual(cd.Doc('x.md', ['Paragraph', '<div>', '# Hidden']).headings(), [])
        self.assertEqual(cd.Doc('x.md', ['<details>', '', '# Visible', '', '</details>']).headings(), ['Visible'])
        self.assertEqual(cd.Doc('x.md', ['> <div>', '> # Hidden', '# Visible']).headings(), ['Visible'])
        self.assertEqual(cd.Doc('x.md', ['- <div>', '  # Hidden', '# Visible']).headings(), ['Visible'])

    def test_word_adjacent_issue_references_are_local_except_repository_form(self):
        self.write('README.md', 'issue#12 PR#13 typo#14 owner/repo#15 org/repo.name#16.\n')
        gh = StubGitHub({'issues/12': {'pull_request': {}}, 'issues/13': {}})
        self.assertEqual(self.run_checks(['README.md'], gh=gh), [
            'README.md:1: refs: #12 is called an issue but is a pull request',
            'README.md:1: refs: #13 is called a pull request but is an issue',
            'README.md:1: refs: #14 does not exist in owner/repo (another repository\'s number is written owner/repo#14)',
        ])
        self.assertEqual(gh.asked, ['issues/12', 'issues/13', 'issues/14'])

    def test_formatted_source_and_adr_identifiers_are_checked(self):
        for text in ('S**404** ADR-**999**', 'S[404](https://example.test) ADR-[999](https://example.test)',
                     'S[404][ref] ADR-[999][ref]\n\n[ref]: https://example.test',
                     'S***404*** ADR-***999***'):
            with self.subTest(text=text):
                self.write('README.md', text + '\n')
                self.assertEqual(self.run_checks(['README.md']), [
                    'README.md:1: adr: ADR-999 has no heading in docs/blueprint.md',
                    'README.md:1: sources: S404 is not defined in docs/blueprint.md',
                ])

    def test_source_list_definitions_allow_markdown_indentation(self):
        for indent in (' ', '  ', '   '):
            with self.subTest(indent=indent):
                self.write('README.md', f'Cite S99.\n\n{indent}- S99: https://example.test\n')
                self.assertEqual(self.run_checks(['README.md']), [])
                self.write('README.md', f'Cite S99.\n\n{indent}- S99: retired old source\n')
                self.assertEqual(self.run_checks(['README.md']), [
                    'README.md:1: sources: S99 is retired in README.md, so it has no source to cite',
                ])

    def test_link_destinations_do_not_break_visible_identifier_adjacency(self):
        for text in ('[S](https://example.test)404 [ADR-](https://example.test)999 [#](https://example.test)999999',
                     '[S](<https://example.test>)404 [ADR-](<https://example.test>)999 [#](<https://example.test>)999999',
                     '[S][ref]404 [ADR-][ref]999 [#][ref]999999\n\n[ref]: https://example.test'):
            with self.subTest(text=text):
                self.write('README.md', text + '\n')
                gh = StubGitHub({})
                out = self.run_checks(['README.md'], gh=gh)
                self.assertEqual(len(out), 3)
                self.assertTrue(any('S404 is not defined' in f for f in out))
                self.assertTrue(any('ADR-999 has no heading' in f for f in out))
                self.assertEqual(gh.asked, ['issues/999999'])

    # --- command line ---------------------------------------------------------

    def test_cli_exit_codes(self):
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        self.write("AGENTS.md", "# A\n\nFine.\n")
        subprocess.run(["git", "add", "AGENTS.md", "docs/blueprint.md"], cwd=self.root, check=True)
        subprocess.run(["git", "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "x"], cwd=self.root, check=True)
        env = {k: v for k, v in os.environ.items() if k not in ("GITHUB_HEAD_REF", "GITHUB_REF_NAME", "GITHUB_EVENT_PATH")}
        env.update(GIT_DIR=str(self.root / ".git"), GIT_WORK_TREE=str(self.root))
        script = str(HERE.parent / "check_docs.py")
        ok = subprocess.run([sys.executable, script, "--offline", "--today", "2026-10-13"], cwd=self.root, env=env, capture_output=True, text=True)
        self.assertEqual(ok.returncode, 0, ok.stdout + ok.stderr)
        self.assertIn("check-docs: ok, 2 file(s), offline", ok.stdout)
        self.write("AGENTS.md", "# A\n\nDecided 2026-10-14.\n")
        bad = subprocess.run([sys.executable, script, "--offline", "--today", "2026-10-13", "AGENTS.md"], cwd=self.root, env=env, capture_output=True, text=True)
        self.assertEqual(bad.returncode, 1)
        self.assertIn("AGENTS.md:3: date:", bad.stdout)
        usage = subprocess.run([sys.executable, script, "--offline", "--today", "yesterday"], cwd=self.root, env=env, capture_output=True, text=True)
        self.assertEqual(usage.returncode, 2)
        env_no_gh = dict(env, PATH=str(self.path_without_gh()))
        down = subprocess.run([sys.executable, script, "--today", "2026-10-13"], cwd=self.root, env=env_no_gh, capture_output=True, text=True)
        self.assertEqual(down.returncode, 2, down.stdout + down.stderr)
        self.assertIn("could not check with GitHub", down.stderr)
        outside = subprocess.run([sys.executable, script, "--offline"], cwd=self.root.parent, env=dict(os.environ, GIT_DIR=str(self.root / "nowhere")), capture_output=True, text=True)
        self.assertEqual(outside.returncode, 2, outside.stdout + outside.stderr)

    def test_current_branch_is_git_branch_first_then_actions_refs_on_detached_head(self):
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        subprocess.run(["git", "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "--allow-empty", "-m", "x"], cwd=self.root, check=True)
        subprocess.run(["git", "checkout", "-q", "-b", "claude/1-x"], cwd=self.root, check=True)
        old = {k: os.environ.pop(k, None) for k in ("GITHUB_HEAD_REF", "GITHUB_REF_NAME", "GITHUB_EVENT_PATH")}
        try:
            named = cd.current_branch(self.root)
            self.assertEqual(named, "claude/1-x")
            os.environ["GITHUB_REF_NAME"] = "main"  # an issues-event run: git's branch wins
            self.assertEqual(cd.current_branch(self.root), named)
            subprocess.run(["git", "checkout", "-q", "--detach"], cwd=self.root, check=True)
            self.assertEqual(cd.current_branch(self.root), "")  # main is not a working branch
            os.environ["GITHUB_REF_NAME"] = "12/merge"
            self.assertEqual(cd.current_branch(self.root), "")
            os.environ["GITHUB_HEAD_REF"] = "claude/9-x"
            self.assertEqual(cd.current_branch(self.root), "claude/9-x")
        finally:
            for k, v in old.items():
                if v is not None:
                    os.environ[k] = v
                else:
                    os.environ.pop(k, None)


if __name__ == "__main__":
    unittest.main(verbosity=1)
