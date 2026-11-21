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


def comment(comment_id, created, body, issue=7):
    return {
        "id": comment_id,
        "created_at": created,
        "updated_at": created,
        "html_url": f"{URL}/issues/{issue}#issuecomment-{comment_id}",
        "body": body,
    }


class DecisionLogTests(unittest.TestCase):
    def setUp(self):
        fixtures = tempfile.TemporaryDirectory()
        self.addCleanup(fixtures.cleanup)
        self.root = Path(fixtures.name)
        (self.root / "linked").mkdir()
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
            f'### 1. Decision #7 comment 1\n\nCreated {CREATED}; updated {CREATED}; {URL}/issues/7#issuecomment-1\n\n'
            '## Decision\n\nHari: "yes"\n\n---\n\n'
            '### 2. Approval #7 comment 3\n\nCreated 2026-11-01T00:00:02Z; updated 2026-11-01T00:00:02Z; '
            f'{URL}/issues/7#issuecomment-3\n\n  \n  ## Approval\nok\n', out,
        )

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
                f"Created {CREATED}; updated {CREATED}; {URL}/issues/{issue}#issuecomment-{comment_id}\n"
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
        body = f"## Decision\n{URL}/issues/499#issuecomment-99"
        self.write_json("7.json", [comment(1, CREATED, body)])
        out = self.offline("--issue", "7", status=3, stderr="decision-log: 1 linked comment(s) not available\n")
        self.assertIn(body, out)
        self.assertTrue(out.endswith("#### 1.1 Linked from comment 1: #499 comment 99\nNot available.\n"))

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
            f"Created {CREATED}; updated {CREATED}; {link}",
            "Not printed: not a decision or approval comment.",
        ], lines[-3:])
        self.assertNotIn("secret handoff text", out)


if __name__ == "__main__":
    unittest.main()
