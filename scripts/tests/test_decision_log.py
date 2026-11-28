"""Exercise the decision log command with local GitHub comment fixtures."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "decision-log.py"
REPO = "collaborationwithothers/eventhubs-kafka-dotnet"
URL = f"https://github.com/{REPO}"
CREATED = "2026-11-01T00:00:00Z"
NOTICE = "Binding record: the comments below, verbatim, in creation order. This log is not a source; cite the comment URLs."
PAGE = f"repos/{REPO}/issues/7/comments?per_page=100&page="

# The gh stand-in records its argument vector and serves only the prepared routes.
FAKE_GH = """\
import json
from pathlib import Path
import sys

with Path("calls.jsonl").open("a", encoding="utf-8") as calls:
    print(json.dumps(["gh", *sys.argv[1:]]), file=calls)
routes = json.loads(Path("routes.json").read_text(encoding="utf-8"))
if len(sys.argv) != 3 or sys.argv[1] != "api" or sys.argv[2] not in routes:
    sys.exit(1)
print(json.dumps(routes[sys.argv[2]]))
"""


def comment(comment_id, created, body, issue=7, login="haripraghash"):
    return {
        "id": comment_id,
        "created_at": created,
        "updated_at": created,
        "html_url": f"{URL}/issues/{issue}#issuecomment-{comment_id}",
        "body": body,
        "user": {"login": login},
    }


class DecisionLogTests(unittest.TestCase):
    def setUp(self):
        fixtures = tempfile.TemporaryDirectory()
        self.addCleanup(fixtures.cleanup)
        self.root = Path(fixtures.name)
        (self.root / "linked").mkdir()
        (self.root / "reactions").mkdir()
        (self.root / "calls.jsonl").touch()
        self.write_json("routes.json", {})
        gh = self.root / "gh"
        gh.write_text(f"#!{sys.executable}\n{FAKE_GH}", encoding="utf-8")
        gh.chmod(0o755)

    def write_json(self, name, value):
        (self.root / name).write_text(json.dumps(value), encoding="utf-8")

    def calls(self):
        lines = (self.root / "calls.jsonl").read_text(encoding="utf-8").splitlines()
        return [json.loads(line) for line in lines]

    def run_cli(self, *args):
        done = subprocess.run(
            [sys.executable, SCRIPT, *args], cwd=self.root, capture_output=True,
            env={**os.environ, "PATH": str(self.root)},
        )
        # Decoding the captured bytes preserves CRLF from a comment body.
        return done.returncode, done.stdout.decode("utf-8"), done.stderr.decode("utf-8")

    def offline(self, *args, status=0, stderr=""):
        code, out, err = self.run_cli("--offline", self.root, *args)
        self.assertEqual((status, stderr), (code, err))
        self.assertEqual([], self.calls())
        return out

    def test_recognizes_only_decision_and_approval_headings(self):
        self.write_json("7.json", [
            comment(1, CREATED, '## Decision\n\nHari: "yes"'),
            comment(2, "2026-11-01T00:00:01Z", "**Decision needed:** x"),
            comment(3, "2026-11-01T00:00:02Z", "  \n  ## Approval\nok"),
            comment(4, "2026-11-01T00:00:03Z", "### Decision\nno"),
            comment(5, "2026-11-01T00:00:04Z", "Decision: no"),
        ])
        out = self.offline("--issue", "7")
        self.assertEqual(
            f'# Decision log: #7\n\nRead 5 comments. {NOTICE}\n\n---\n\n'
            f'### 1. Decision #7 comment 1\n\nCreated {CREATED}; updated {CREATED}; author haripraghash; {URL}/issues/7#issuecomment-1\n\n'
            '## Decision\n\nHari: "yes"\n\n---\n\n'
            '### 2. Approval #7 comment 3\n\nCreated 2026-11-01T00:00:02Z; updated 2026-11-01T00:00:02Z; author haripraghash; '
            f'{URL}/issues/7#issuecomment-3\n\n  \n  ## Approval\nok\n', out,
        )

    def test_only_decision_account_bodies_are_printed(self):
        outsider = comment(2, CREATED, "## Decision\nthird-party text", login="another-user")
        missing = comment(3, CREATED, "## Approval\nunattributed text")
        del missing["user"]
        links = f"{outsider['html_url']} {missing['html_url']}"
        owner = comment(1, CREATED, f"## Approval\nsee {links}", login="HariPraghash")
        self.write_json("7.json", [owner, outsider, missing])
        self.write_json("linked/2.json", outsider)
        self.write_json("linked/3.json", missing)

        out = self.offline("--issue", "7")
        self.assertEqual(["### 1. Approval #7 comment 1"],
                         [line for line in out.splitlines() if line.startswith("### ")])
        self.assertIn(owner["body"], out)
        for record, login in [(owner, "HariPraghash"), (outsider, "another-user"), (missing, "unknown")]:
            self.assertIn(f"author {login}; {record['html_url']}", out)
        self.assertNotIn(outsider["body"], out)
        self.assertNotIn(missing["body"], out)

    def test_orders_by_created_then_id_across_issues(self):
        later = "2026-11-02T00:00:00Z"
        self.write_json("7.json", [comment(30, later, "## Decision\na")])
        self.write_json("8.json", [comment(20, later, "## Decision\nb", 8), comment(40, CREATED, "## Approval\nc", 8)])
        out = self.offline("--issue", "7", "--issue", "8")
        self.assertTrue(out.startswith(f"# Decision log: #7, #8\n\nRead 3 comments. {NOTICE}\n"))
        self.assertEqual([
            "### 1. Approval #8 comment 40", "### 2. Decision #8 comment 20", "### 3. Decision #7 comment 30",
        ], [line for line in out.splitlines() if line.startswith("### ")])

    def test_prints_body_verbatim(self):
        body = "## Decision\n\nHari said: \u201cfor 531 implement using claude\u201d.  \r\nend"
        self.write_json("7.json", [comment(1, CREATED, body)])
        self.assertIn(body, self.offline("--issue", "7"))

    def test_lists_each_link_once_in_order(self):
        eleven = f"{URL}/issues/499#issuecomment-11"
        twelve = f"{URL}/pull/413#issuecomment-12"
        self.write_json("7.json", [comment(1, CREATED, f"## Decision\nadopts {eleven} and {twelve} and again {eleven}")])
        self.write_json("linked/11.json", comment(11, CREATED, "## Decision\nlinked eleven", 499))
        self.write_json("linked/12.json", comment(12, CREATED, "## Approval\nlinked twelve", 413))
        out = self.offline("--issue", "7")
        self.assertEqual([
            "#### 1.1 Linked from comment 1: #499 comment 11", "#### 1.2 Linked from comment 1: #413 comment 12",
        ], [line for line in out.splitlines() if line.startswith("#### ")])
        for issue, comment_id, kind, phrase in ((499, 11, "Decision", "linked eleven"), (413, 12, "Approval", "linked twelve")):
            self.assertIn(
                f"Created {CREATED}; updated {CREATED}; author haripraghash; {URL}/issues/{issue}#issuecomment-{comment_id}\n"
                'Adoption is judged by the reading session under docs/agents/factory.md "Item lifecycle" step 3.\n\n'
                f"## {kind}\n{phrase}", out,
            )
            self.assertEqual(1, out.count(phrase))

    def test_link_to_listed_entry_has_no_body(self):
        self.write_json("7.json", [
            comment(1, "2026-11-01T00:00:01Z", "## Decision\nfirst"),
            comment(2, "2026-11-01T00:00:02Z", f"## Decision\nsee {URL}/issues/7#issuecomment-1"),
        ])
        out = self.offline("--issue", "7")
        self.assertTrue(out.endswith("#### 2.1 Linked from comment 2: #7 comment 1\nListed as entry 1.\n"))
        self.assertEqual(1, out.count("## Decision\nfirst"))

    def test_missing_link_exits_3(self):
        for linked_id in ("99", "9" * 5000):
            with self.subTest(length=len(linked_id)):
                body = f"## Decision\n{URL}/issues/499#issuecomment-{linked_id}"
                self.write_json("7.json", [comment(1, CREATED, body)])
                out = self.offline("--issue", "7", status=3,
                                   stderr="decision-log: 1 linked comment(s) not available\n")
                self.assertIn(body, out)
                self.assertTrue(out.endswith(
                    f"#### 1.1 Linked from comment 1: #499 comment {linked_id}\nNot available.\n"
                ))

    def test_unreadable_issue_exits_1(self):
        out = self.offline("--issue", "7", status=1, stderr="decision-log: cannot read comments of #7\n")
        self.assertEqual("", out)

    def test_fetch_pages_with_exact_arguments(self):
        self.write_json("routes.json", {
            PAGE + "1": [comment(i, CREATED, "plain") for i in range(1, 101)],
            PAGE + "2": [comment(101, CREATED, "plain")],
        })
        code, out, err = self.run_cli("--issue", "7")
        self.assertEqual([["gh", "api", PAGE + "1"], ["gh", "api", PAGE + "2"]], self.calls())
        self.assertEqual((0, ""), (code, err))
        self.assertIn(f"Read 101 comments. {NOTICE}\n", out)

    def test_fetch_failure_exits_1(self):
        self.assertEqual((1, "", "decision-log: cannot read comments of #7\n"), self.run_cli("--issue", "7"))
        self.assertEqual([["gh", "api", PAGE + "1"]], self.calls())

    def test_no_authority_comments(self):
        self.write_json("7.json", [comment(1, CREATED, "plain")])
        self.assertEqual(
            f"# Decision log: #7\n\nRead 1 comments. {NOTICE}\n\nNo decision or approval comments.\n",
            self.offline("--issue", "7"),
        )

    def test_invalid_issue_exits_2(self):
        for args in (["--issue", "0"], ["--issue", "abc"], []):
            with self.subTest(args=args):
                code, out, err = self.run_cli(*args)
                self.assertEqual((2, ""), (code, out))
                self.assertIn("usage:", err)
                self.assertEqual([], self.calls())

    def test_non_authority_link_body_not_printed(self):
        link = f"{URL}/issues/499#issuecomment-21"
        self.write_json("7.json", [comment(1, CREATED, f"## Decision\nsee {link}")])
        self.write_json("linked/21.json", comment(21, CREATED, "**Decision needed:** secret handoff text", 499))
        out = self.offline("--issue", "7")
        lines = [line for line in out.splitlines() if line]
        self.assertEqual([
            "#### 1.1 Linked from comment 1: #499 comment 21",
            f"Created {CREATED}; updated {CREATED}; author haripraghash; {link}",
            "Not printed: not a decision or approval comment.",
        ], lines[-3:])
        self.assertNotIn("secret handoff text", out)


    def test_machine_account_comments_are_authority_comments(self):
        self.write_json("7.json", [comment(1, CREATED, "## Decision\nrecorded from chat", login="haripraghash-bot")])
        out = self.offline("--issue", "7")
        self.assertIn("### 1. Decision #7 comment 1", out)
        self.assertIn(f"author haripraghash-bot; {URL}/issues/7#issuecomment-1\n\n## Decision\nrecorded from chat", out)
        self.assertNotIn("Confirmation:", out)

    def test_confirmation_needs_haris_thumbs_up(self):
        body = "## Decision\naccepts a risk\n\nConfirmation: thumbs-up required\n"
        meta = f"Created {CREATED}; updated {CREATED}; author haripraghash-bot; {URL}/issues/7#issuecomment-1\n"
        cases = {
            "hari thumbs-up": ([{"content": "+1", "user": {"login": "HariPraghash"}}],
                               "Confirmation: thumbs-up from haripraghash."),
            "no reactions": ([], "Confirmation: NOT confirmed; no thumbs-up from haripraghash. Do not act on this decision."),
            "wrong user or content": ([{"content": "+1", "user": {"login": "haripraghash-bot"}},
                                       {"content": "heart", "user": {"login": "haripraghash"}},
                                       {"content": "+1"}],
                                      "Confirmation: NOT confirmed; no thumbs-up from haripraghash. Do not act on this decision."),
        }
        for name, (reactions, line) in cases.items():
            with self.subTest(name):
                self.write_json("7.json", [comment(1, CREATED, body, login="haripraghash-bot")])
                self.write_json("reactions/1.json", reactions)
                self.assertIn(f"{meta}{line}\n\n{body}", self.offline("--issue", "7"))

    def test_confirmation_posted_by_hari_needs_no_reaction(self):
        body = "## Decision\nloosens a cap\nConfirmation: thumbs-up required"
        self.write_json("7.json", [comment(1, CREATED, body)])
        out = self.offline("--issue", "7")
        self.assertIn(f"author haripraghash; {URL}/issues/7#issuecomment-1\nConfirmation: posted by haripraghash.\n", out)

    def test_marker_must_be_its_own_line(self):
        body = "## Decision\nno Confirmation: thumbs-up required here"
        self.write_json("7.json", [comment(1, CREATED, body, login="haripraghash-bot")])
        self.assertNotIn("\nConfirmation:", self.offline("--issue", "7"))

    def test_unreadable_reactions_exit_3(self):
        body = "## Decision\nConfirmation: thumbs-up required"
        self.write_json("7.json", [comment(1, CREATED, body, login="haripraghash-bot")])
        out = self.offline("--issue", "7", status=3, stderr="decision-log: 1 confirmation(s) not available\n")
        self.assertIn("Confirmation: not available; reactions could not be read.\n", out)
        self.assertIn(body, out)

    def test_linked_draft_shows_confirmation(self):
        link = f"{URL}/issues/499#issuecomment-31"
        self.write_json("7.json", [comment(1, CREATED, f"## Approval\nadopts {link}")])
        self.write_json("linked/31.json", comment(31, CREATED, "## Decision\nConfirmation: thumbs-up required", 499,
                                                  login="haripraghash-bot"))
        self.write_json("reactions/31.json", [])
        out = self.offline("--issue", "7")
        self.assertIn(f"author haripraghash-bot; {link}\nConfirmation: NOT confirmed; no thumbs-up from haripraghash. "
                      "Do not act on this decision.\nAdoption is judged", out)

    def test_fetch_reactions_with_exact_arguments(self):
        reactions = f"repos/{REPO}/issues/comments/1/reactions?per_page=100&page="
        self.write_json("routes.json", {
            PAGE + "1": [comment(1, CREATED, "## Decision\nConfirmation: thumbs-up required", login="haripraghash-bot")],
            reactions + "1": [{"content": "heart", "user": {"login": "x"}}] * 100,
            reactions + "2": [{"content": "+1", "user": {"login": "haripraghash"}}],
        })
        code, out, err = self.run_cli("--issue", "7")
        self.assertEqual([["gh", "api", PAGE + "1"], ["gh", "api", reactions + "1"], ["gh", "api", reactions + "2"]],
                         self.calls())
        self.assertEqual((0, ""), (code, err))
        self.assertIn("Confirmation: thumbs-up from haripraghash.\n", out)

if __name__ == "__main__":
    unittest.main()
