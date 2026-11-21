#!/usr/bin/env python3
"""Print decision and approval comments on the specified repository issues."""

import argparse
from dataclasses import dataclass
import json
from pathlib import Path
import re
import subprocess
import sys


REPOSITORY = "collaborationwithothers/eventhubs-kafka-dotnet"
DECISION_ACCOUNT = "haripraghash"
API = f"repos/{REPOSITORY}/issues"
COMMENT_URL = re.compile(
    rf"https://github\.com/{REPOSITORY}/(?:issues|pull)/(\d+)#issuecomment-(\d+)"
)
READ_ERRORS = (OSError, ValueError, subprocess.CalledProcessError)


@dataclass
class Comment:
    id: int
    issue: str
    created_at: str
    updated_at: str
    html_url: str
    body: str
    author: str

    @classmethod
    def parse(cls, data):
        if not isinstance(data, dict) or type(data.get("id")) is not int:
            raise ValueError("expected a comment object")
        for field in ("created_at", "updated_at", "html_url"):
            if not isinstance(data.get(field), str):
                raise ValueError(f"expected a comment {field}")
        if "body" not in data or not isinstance(data["body"], (str, type(None))):
            raise ValueError("expected a comment body")
        url = COMMENT_URL.fullmatch(data["html_url"])
        if not url or url[2] != str(data["id"]):
            raise ValueError("expected a repository issue comment URL")
        user = data.get("user")
        login = user.get("login") if isinstance(user, dict) else None
        return cls(data["id"], url[1], data["created_at"], data["updated_at"],
                   data["html_url"], data["body"] or "", login if isinstance(login, str) else "")

    def kind(self):
        if not self.author.isascii() or self.author.lower() != DECISION_ACCOUNT:
            return None
        heading = next((line.lstrip(" ") for line in self.body.splitlines() if line.strip()), "")
        for kind in ("Decision", "Approval"):
            if heading.startswith(f"## {kind}"):
                return kind
        return None

    def metadata(self):
        return f"Created {self.created_at}; updated {self.updated_at}; author {self.author or 'unknown'}; {self.html_url}"


def get_json(route):
    response = subprocess.run(["gh", "api", route], check=True, capture_output=True)
    return json.loads(response.stdout.decode("utf-8"))


def read_comments(issue, offline):
    comments = []
    page = 1
    while True:
        if offline is None:
            data = get_json(f"{API}/{issue}/comments?per_page=100&page={page}")
        else:
            data = json.loads((offline / f"{issue}.json").read_bytes())
        if not isinstance(data, list):
            raise ValueError("expected a comment array")
        comments.extend(Comment.parse(item) for item in data)
        if offline is not None or len(data) < 100:
            return comments
        page += 1


def read_linked(comment_id, offline):
    if offline is None:
        data = get_json(f"{API}/comments/{comment_id}")
    else:
        data = json.loads((offline / "linked" / f"{comment_id}.json").read_bytes())
    comment = Comment.parse(data)
    if str(comment.id) != comment_id:
        raise ValueError("linked comment ID does not match")
    return comment


def render(issues, comments, offline):
    entries = sorted((c for c in comments if c.kind()), key=lambda c: (c.created_at, c.id))
    listed = {str(c.id): i for i, c in enumerate(entries, 1)}
    linked = {}
    unavailable = set()
    issue_list = ", ".join(f"#{issue}" for issue in issues)
    lines = [f"# Decision log: {issue_list}", "",
             f"Read {len(comments)} comments. Binding record: the comments below, verbatim, "
             "in creation order. This log is not a source; cite the comment URLs."]

    if not entries:
        lines.extend(["", "No decision or approval comments."])
    for i, comment in enumerate(entries, 1):
        lines.extend(["", "---", "", f"### {i}. {comment.kind()} #{comment.issue} comment {comment.id}",
                      "", comment.metadata(), "", comment.body])
        links = {}
        for issue, comment_id in COMMENT_URL.findall(comment.body):
            links.setdefault(comment_id.lstrip("0") or "0", issue)
        for k, (comment_id, issue) in enumerate(links.items(), 1):
            if comment_id not in linked and (offline is None or comment_id not in listed):
                try:
                    linked[comment_id] = read_linked(comment_id, offline)
                except READ_ERRORS:
                    linked[comment_id] = None
            entry = listed.get(comment_id)
            target = entries[entry - 1] if entry is not None else linked[comment_id]
            target_issue = target.issue if target is not None else issue
            lines.extend(["", f"#### {i}.{k} Linked from comment {comment.id}: "
                              f"#{target_issue} comment {comment_id}"])
            if entry is not None:
                # The issue page already supplied this comment in full.
                lines.append(f"Listed as entry {entry}.")
            elif target is None:
                lines.append("Not available.")
                unavailable.add(comment_id)
            elif target.kind():
                lines.extend(["", target.metadata(), 'Adoption is judged by the reading session under '
                              'docs/agents/factory.md "Item lifecycle" step 3.', "", target.body])
            else:
                lines.extend([target.metadata(), "Not printed: not a decision or approval comment."])
    return "\n".join(lines) + "\n", len(unavailable)


def positive_issue(value):
    issue = int(value)
    if issue <= 0:
        raise argparse.ArgumentTypeError("expected a positive issue number")
    return issue


def main():
    parser = argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
    parser.add_argument("--issue", type=positive_issue, required=True, action="append", metavar="N")
    parser.add_argument("--offline", type=Path, metavar="DIR")
    args = parser.parse_args()
    issues = list(dict.fromkeys(args.issue))
    comments = []
    for issue in issues:
        try:
            comments.extend(read_comments(issue, args.offline))
        except READ_ERRORS:
            print(f"decision-log: cannot read comments of #{issue}", file=sys.stderr)
            return 1
    log, unavailable = render(issues, comments, args.offline)
    sys.stdout.buffer.write(log.encode("utf-8"))
    if unavailable:
        print(f"decision-log: {unavailable} linked comment(s) not available", file=sys.stderr)
        return 3
    return 0


if __name__ == "__main__":
    sys.exit(main())
