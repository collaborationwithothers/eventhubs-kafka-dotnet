#!/usr/bin/env python3
"""Tests for scripts/check_docs.py. Run: python3 scripts/tests/check_docs_test.py

Each test builds a small repository in a temporary directory, so the checks
run against known files. GitHub is replaced by a stub; no network is used.
"""

from __future__ import annotations

import datetime as dt
import importlib.util
import os
import subprocess
import sys
import tempfile
import unittest
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
        self.assertEqual(out, ["AGENTS.md:3: date: 2026-13-01 is not a calendar date"])

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
        gh.write_text("#!/bin/sh\ncase \"$2\" in\n  */issues/404) echo '{\"message\":\"Not Found\"}'; echo 'gh: Not Found (HTTP 404)' >&2; exit 1;;\n  */issues/403) echo 'gh: Forbidden (HTTP 403)' >&2; exit 1;;\n  */issues/500) echo 'not json';;\n  *) echo '{\"number\": 1}';;\nesac\n")
        gh.chmod(0o755)
        old = os.environ["PATH"]
        os.environ["PATH"] = f"{fake}{os.pathsep}{old}"
        try:
            api = cd.GitHub("owner/repo")
            self.assertEqual(api.get("issues/1"), {"number": 1})
            self.assertIsNone(api.get("issues/404"))
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
        # Neither branch has a pull request: both report.
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [prefix + "neither branch has an open pull request yet; the first to open one keeps the number"])
        self.assertNotIn("contents/docs/blueprint.md?ref=claude/1-x", gh.asked)
        # Only theirs has a pull request: we renumber.
        gh.routes["pulls?state=open&head=owner:claude/8-adrs"] = [{"created_at": "2026-10-13T10:00:00Z"}]
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [prefix + "this branch has no open pull request and that one has, so this branch renumbers"])
        # Theirs opened first: we renumber.
        gh.routes["pulls?state=open&head=owner:claude/1-x"] = [{"created_at": "2026-10-13T11:00:00Z"}]
        self.assertEqual(self.run_checks(["docs/blueprint.md"], gh=gh), [prefix + "that branch's pull request opened first, so this branch renumbers"])
        # Ours opened first: we keep the number.
        gh.routes["pulls?state=open&head=owner:claude/1-x"] = [{"created_at": "2026-10-13T09:00:00Z"}]
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

    # --- command line ---------------------------------------------------------

    def test_cli_exit_codes(self):
        subprocess.run(["git", "init", "-q"], cwd=self.root, check=True)
        self.write("AGENTS.md", "# A\n\nFine.\n")
        subprocess.run(["git", "add", "AGENTS.md", "docs/blueprint.md"], cwd=self.root, check=True)
        subprocess.run(["git", "-c", "user.name=t", "-c", "user.email=t@t", "commit", "-q", "-m", "x"], cwd=self.root, check=True)
        env = {k: v for k, v in os.environ.items() if k not in ("GITHUB_HEAD_REF", "GITHUB_REF_NAME")}
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
        old = {k: os.environ.pop(k, None) for k in ("GITHUB_HEAD_REF", "GITHUB_REF_NAME")}
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
