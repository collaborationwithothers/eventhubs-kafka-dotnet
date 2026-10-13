#!/usr/bin/env python3
"""Deterministic checks for this repository's Markdown.

Each check is an error class that a cold review has already found. Run it
before the cold review (AGENTS.md "Before opening a pull request", rule 8)
and, once Hari adds the workflow, as a status check. It holds no judgement:
a claim that is unsupported by its source, or two documents that disagree,
stay with the reviewer.

Usage:
  python3 scripts/check_docs.py             check every tracked Markdown file
  python3 scripts/check_docs.py FILE...     check only these files
  python3 scripts/check_docs.py --offline   skip the checks that call GitHub
  python3 scripts/check_docs.py --today D   override today's date (UTC, YYYY-MM-DD)

Exit codes: 0 nothing found; 1 something found; 2 bad usage, or GitHub could
not be reached, in which case the GitHub-backed checks are unknown, not passed.
Output: one line per finding, `path:line: class: message`, then a summary.

One waiver exists, for the `date` class only: a date the text states as
planned or scheduled. Mark its line `<!-- check-docs: allow date: <why> -->`.

Sources: a document that defines sources (`| Sn | ... |` rows, or `- Sn: url`
lines) must reference every one of them; a number kept on purpose with no
source is written `- Sn: retired ...`.
"""

from __future__ import annotations

import argparse
import base64
import datetime as dt
import json
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

REPO_SLUG = "collaborationwithothers/eventhubs-kafka-dotnet"
BLUEPRINT = "docs/blueprint.md"
DEFAULT_BRANCH = "main"

# Files the checks cover: the repository's own documents. Third-party skills
# pinned in skills-lock.json are copies and are not ours to fix.
INCLUDE = (
    "AGENTS.md",
    "CLAUDE.md",
    "README.md",
    "CONTEXT.md",
    ".github/pull_request_template.md",
    ".agents/skills/repo-review/SKILL.md",
    ".claude/agents/repo-reviewer.md",
)
INCLUDE_DIRS = ("docs/",)
# Unit symbols the metric-units standard needs; everything else non-ASCII is reported.
ALLOWED_NON_ASCII = {"\u00b0", "\u00b5"}
ALLOW = re.compile(r"<!-- check-docs: allow (date): [^>]*\S[^>]*-->")
WAIVABLE = ("date",)

ADR_HEADING = re.compile(r"^### ADR-(\d{3})\b")
ADR_REF = re.compile(r"\bADR-(\d{3})\b")
ADR_PARTS = ("**Context.**", "**Options considered.**", "**Decision.**", "**Consequences.**")
# An ADR recorded as an option, not taken: `### ADR-NNN (option, deferred): ...`
ADR_OPTION_PARTS = ("**Context.**", "**Option.**", "**Status.**")
RETIRED = "retired"
SOURCE_TABLE_DEF = re.compile(r"^\| (S\d+) \|")
SOURCE_LIST_DEF = re.compile(r"^- (S\d+): ")
SOURCE_REF = re.compile(r"\bS\d+\b")
DATE = re.compile(r"(?<!\d)(20\d\d)-(\d\d)-(\d\d)(?!\d)")  # also inside 2026-10-13T16:17Z
ISSUE_REF = re.compile(r"(?<![\w/#])#(\d+)\b")
# `file.md "Heading"`, `file.md, "Heading"` and `file.md's "Heading"`
FILE_HEADING_REF = re.compile(r"((?:docs/[\w./-]+|AGENTS|CLAUDE|README|CONTEXT)\.md)(?:,|'s)? \"([^\"]+)\"")
GOVERNANCE_REF = re.compile(r'GOVERNANCE "([^"]+)"')
BLUEPRINT_SECTION_REF = re.compile(r"docs/blueprint\.md section (\d+)\b")
HEADING = re.compile(r"^(#{1,6})\s+(.*?)\s*$")


@dataclass(frozen=True)
class Finding:
    path: str
    line: int
    cls: str
    message: str

    def __str__(self) -> str:
        return f"{self.path}:{self.line}: {self.cls}: {self.message}"


@dataclass
class Doc:
    path: str
    lines: list[str]

    @property
    def text(self) -> str:
        return "\n".join(self.lines)

    def headings(self) -> list[str]:
        out = []
        for line in self.lines:
            m = HEADING.match(line)
            if m:
                out.append(m.group(2))
        return out


# --- discovery --------------------------------------------------------------


def tracked_markdown(root: Path) -> list[str]:
    out = subprocess.run(
        ["git", "ls-files", "*.md"], cwd=root, check=True, capture_output=True, text=True
    ).stdout.split()
    return [p for p in out if p in INCLUDE or p.startswith(INCLUDE_DIRS)]


def load(root: Path, paths: list[str]) -> dict[str, Doc]:
    docs = {}
    for p in paths:
        docs[p] = Doc(p, (root / p).read_text(encoding="utf-8").split("\n"))
    return docs


# --- checks that need no network --------------------------------------------


def check_ascii(doc: Doc) -> list[Finding]:
    out = []
    for n, line in enumerate(doc.lines, 1):
        bad = next((c for c in line if ord(c) > 0x7F and c not in ALLOWED_NON_ASCII), None)
        if bad is not None:
            out.append(Finding(doc.path, n, "ascii", f"non-ASCII character U+{ord(bad):04X}"))
    return out


def check_future_dates(doc: Doc, today: dt.date) -> list[Finding]:
    out = []
    for n, line in enumerate(doc.lines, 1):
        for m in DATE.finditer(line):
            try:
                d = dt.date(int(m.group(1)), int(m.group(2)), int(m.group(3)))
            except ValueError:
                out.append(Finding(doc.path, n, "date", f"{m.group(0)} is not a calendar date"))
                continue
            if d > today:
                out.append(Finding(doc.path, n, "date", f"{m.group(0)} is after today ({today})"))
    return out


def source_definitions(doc: Doc) -> dict[str, int]:
    """Source IDs this document defines, with the line of each definition."""
    defs: dict[str, int] = {}
    for n, line in enumerate(doc.lines, 1):
        m = SOURCE_TABLE_DEF.match(line) or SOURCE_LIST_DEF.match(line)
        if m:
            defs.setdefault(m.group(1), n)
    return defs


def retired_sources(doc: Doc) -> set[str]:
    """Source IDs defined as `- Sn: retired ...`: the number is kept, no reference is expected."""
    return {m.group(1) for line in doc.lines if (m := SOURCE_LIST_DEF.match(line)) and line[m.end():].startswith(RETIRED)}


def check_sources(doc: Doc, docs: dict[str, Doc]) -> list[Finding]:
    """Every source reference resolves; every definition is referenced.

    A document that defines sources is self-contained. A document that
    defines none resolves its references against docs/blueprint.md.
    """
    out = []
    own = source_definitions(doc)
    counts: dict[str, int] = {}
    for n, line in enumerate(doc.lines, 1):
        m = SOURCE_TABLE_DEF.match(line) or SOURCE_LIST_DEF.match(line)
        if m:
            counts[m.group(1)] = counts.get(m.group(1), 0) + 1
    for sid, c in counts.items():
        if c > 1:
            out.append(Finding(doc.path, own[sid], "sources", f"{sid} is defined {c} times"))

    if own:
        defs, where = own, doc.path
    elif BLUEPRINT in docs:
        defs, where = source_definitions(docs[BLUEPRINT]), BLUEPRINT
    else:
        return out

    referenced: set[str] = set()
    for n, line in enumerate(doc.lines, 1):
        if SOURCE_TABLE_DEF.match(line) or SOURCE_LIST_DEF.match(line):
            continue
        for m in SOURCE_REF.finditer(line):
            sid = m.group(0)
            referenced.add(sid)
            if sid not in defs:
                out.append(Finding(doc.path, n, "sources", f"{sid} is not defined in {where}"))
    if own:
        for sid, n in own.items():
            if sid not in referenced and sid not in retired_sources(doc):
                out.append(Finding(doc.path, n, "sources", f"{sid} is defined but never referenced (write `- {sid}: retired ...` to keep the number without a source)"))
    return out


def adr_headings(doc: Doc) -> dict[str, int]:
    out: dict[str, int] = {}
    for n, line in enumerate(doc.lines, 1):
        m = ADR_HEADING.match(line)
        if m:
            out.setdefault(m.group(1), n)
    return out


def adr_titles(doc: Doc) -> dict[str, str]:
    """ADR number to the full heading line, so the same ADR carried on two branches is not a collision."""
    return {m.group(1): line.strip() for line in doc.lines if (m := ADR_HEADING.match(line))}


def check_adr_numbers(doc: Doc, docs: dict[str, Doc]) -> list[Finding]:
    out = []
    if BLUEPRINT not in docs:
        return out
    bp = docs[BLUEPRINT]
    defined = adr_headings(bp)
    if doc.path == BLUEPRINT:
        seen: dict[str, int] = {}
        for n, line in enumerate(doc.lines, 1):
            m = ADR_HEADING.match(line)
            if m:
                if m.group(1) in seen:
                    out.append(Finding(doc.path, n, "adr", f"ADR-{m.group(1)} heading repeats line {seen[m.group(1)]}"))
                seen.setdefault(m.group(1), n)
    for n, line in enumerate(doc.lines, 1):
        for m in ADR_REF.finditer(line):
            if m.group(1) not in defined:
                out.append(Finding(doc.path, n, "adr", f"ADR-{m.group(1)} has no heading in {BLUEPRINT}"))
    return out


def check_adr_format(doc: Doc) -> list[Finding]:
    """Each ADR has Context, Options considered, Decision, Consequences, in order.

    An ADR whose heading says `(option` has Context, Option, Status instead.
    """
    out = []
    if doc.path != BLUEPRINT:
        return out
    starts = [n for n, line in enumerate(doc.lines, 1) if ADR_HEADING.match(line)]
    for i, start in enumerate(starts):
        end = starts[i + 1] - 1 if i + 1 < len(starts) else len(doc.lines)
        body = doc.lines[start:end]
        for stop, line in enumerate(body):
            if line.startswith("## ") or line.strip() == "---":
                body = body[:stop]
                break
        text = "\n".join(body)
        parts = ADR_OPTION_PARTS if "(option" in doc.lines[start - 1] else ADR_PARTS
        first = next((l for l in body if l.strip()), "")
        if not first.startswith(parts[0]):
            out.append(Finding(doc.path, start, "adr-format", f"first paragraph does not start with {parts[0]}"))
        last = -1
        for part in parts:
            pos = text.find(part)
            if pos < 0:
                out.append(Finding(doc.path, start, "adr-format", f"missing {part}"))
            elif pos < last:
                out.append(Finding(doc.path, start, "adr-format", f"{part} is out of order"))
            else:
                last = pos
    return out


def check_cross_references(doc: Doc, docs: dict[str, Doc], root: Path) -> list[Finding]:
    """`file.md "Heading"`, `GOVERNANCE "Heading"` and `docs/blueprint.md section N` resolve."""
    out = []

    def headings_of(path: str) -> list[str] | None:
        if path in docs:
            return docs[path].headings()
        f = root / path
        if f.is_file():
            return Doc(path, f.read_text(encoding="utf-8").split("\n")).headings()
        return None

    def resolves(heads: list[str], wanted: str) -> bool:
        return any(h == wanted or h.startswith(wanted + " ") or h.startswith(wanted + ":") for h in heads)

    for n, line in enumerate(doc.lines, 1):
        for m in FILE_HEADING_REF.finditer(line):
            path, wanted = m.group(1), m.group(2)
            heads = headings_of(path)
            if heads is None:
                out.append(Finding(doc.path, n, "xref", f"{path} does not exist"))
            elif not resolves(heads, wanted):
                out.append(Finding(doc.path, n, "xref", f'{path} has no heading "{wanted}"'))
        for m in GOVERNANCE_REF.finditer(line):
            heads = headings_of("AGENTS.md") or []
            if not resolves(heads, m.group(1)):
                out.append(Finding(doc.path, n, "xref", f'AGENTS.md has no heading "{m.group(1)}"'))
        for m in BLUEPRINT_SECTION_REF.finditer(line):
            heads = headings_of(BLUEPRINT) or []
            if not any(h.startswith(f"{m.group(1)}. ") for h in heads):
                out.append(Finding(doc.path, n, "xref", f"{BLUEPRINT} has no section {m.group(1)}"))
    return out


# --- checks that call GitHub ------------------------------------------------


class Unavailable(Exception):
    """GitHub could not be asked. The answer is unknown, not negative."""


class GitHub:
    """Thin wrapper over `gh api`, so tests can replace it.

    `get` returns the parsed JSON, `None` when GitHub says the route does not
    exist (HTTP 404), and raises Unavailable for anything else: `gh` missing,
    no credentials, a proxy refusal, a rate limit.
    """

    def __init__(self, slug: str):
        self.slug = slug

    def get(self, route: str):
        try:
            r = subprocess.run(["gh", "api", f"repos/{self.slug}/{route}"], capture_output=True, text=True)
        except FileNotFoundError as e:
            raise Unavailable("gh is not installed") from e
        if r.returncode == 0:
            try:
                return json.loads(r.stdout)
            except json.JSONDecodeError as e:
                raise Unavailable(f"gh api {route} returned something that is not JSON") from e
        if "HTTP 404" in r.stderr:
            return None
        raise Unavailable(f"gh api {route}: {r.stderr.strip() or r.stdout.strip() or 'failed'}")


def check_issue_references(doc: Doc, gh: GitHub, cache: dict[int, str | None]) -> list[Finding]:
    """`#N` exists; `issue #N` is an issue and `PR #N` or `pull request #N` is a pull request."""
    out = []
    for n, line in enumerate(doc.lines, 1):
        for m in ISSUE_REF.finditer(line):
            num = int(m.group(1))
            if num not in cache:
                data = gh.get(f"issues/{num}")
                cache[num] = None if data is None else ("pull" if "pull_request" in data else "issue")
            kind = cache[num]
            if kind is None:
                out.append(Finding(doc.path, n, "refs", f"#{num} does not exist in {gh.slug}"))
                continue
            before = line[max(0, m.start() - 20):m.start()].lower()
            if re.search(r"\b(pr|prs|pull requests?)\s*$", before) and kind != "pull":
                out.append(Finding(doc.path, n, "refs", f"#{num} is called a pull request but is an issue"))
            elif re.search(r"\bissues?\s*$", before) and kind != "issue":
                out.append(Finding(doc.path, n, "refs", f"#{num} is called an issue but is a pull request"))
    return out


def check_adr_branch_collisions(docs: dict[str, Doc], gh: GitHub, current_branch: str) -> list[Finding]:
    """An ADR number new on this branch is not also new, with a different title, on another branch.

    The same heading on both branches is the same ADR carried over (a branch cut
    from another, or a split pull request), not a collision.
    """
    out = []
    if BLUEPRINT not in docs:
        return out
    branches = all_branches(gh)
    main_doc = gh.get(f"contents/{BLUEPRINT}?ref={DEFAULT_BRANCH}")
    if not branches or main_doc is None:
        return out
    on_main = set(adr_headings(Doc(BLUEPRINT, decode(main_doc).split("\n"))))
    here = adr_headings(docs[BLUEPRINT])
    titles_here = adr_titles(docs[BLUEPRINT])
    new_here = {k: v for k, v in here.items() if k not in on_main}
    if not new_here:
        return out
    for b in branches:
        name = b["name"]
        if name in (DEFAULT_BRANCH, current_branch):
            continue
        data = gh.get(f"contents/{BLUEPRINT}?ref={name}")
        if data is None:
            continue
        their_titles = adr_titles(Doc(BLUEPRINT, decode(data).split("\n")))
        for num in sorted(new_here.keys() & (set(their_titles) - on_main)):
            if their_titles[num] != titles_here[num]:
                out.append(Finding(BLUEPRINT, new_here[num], "adr", f"ADR-{num} is also new on branch {name}, with a different title"))
    return out


def all_branches(gh: GitHub) -> list[dict]:
    """Every branch, paged by hand: `gh api --paginate` is refused by the cloud-session proxy (AGENTS.md)."""
    out: list[dict] = []
    page = 1
    while True:
        batch = gh.get(f"branches?per_page=100&page={page}") or []
        out += batch
        if len(batch) < 100:
            return out
        page += 1


def decode(contents: dict) -> str:
    return base64.b64decode(contents["content"]).decode("utf-8")


# --- entry point ------------------------------------------------------------


def run(root: Path, paths: list[str], today: dt.date, gh: GitHub | None, branch: str) -> list[Finding]:
    docs = load(root, paths)
    if BLUEPRINT not in docs and (root / BLUEPRINT).is_file():
        docs[BLUEPRINT] = Doc(BLUEPRINT, (root / BLUEPRINT).read_text(encoding="utf-8").split("\n"))
    findings: list[Finding] = []
    cache: dict[int, str | None] = {}
    for p in paths:
        doc = docs[p]
        findings += check_ascii(doc)
        findings += check_future_dates(doc, today)
        findings += check_sources(doc, docs)
        findings += check_adr_numbers(doc, docs)
        findings += check_adr_format(doc)
        findings += check_cross_references(doc, docs, root)
        if gh is not None:
            findings += check_issue_references(doc, gh, cache)
    if gh is not None and BLUEPRINT in paths:
        findings += check_adr_branch_collisions(docs, gh, branch)
    findings = [f for f in findings if not allowed(docs[f.path], f)]
    return sorted(set(findings), key=lambda f: (f.path, f.line, f.cls, f.message))


def allowed(doc: Doc, f: Finding) -> bool:
    """True when the finding's class is waivable and its line carries `<!-- check-docs: allow CLASS: why -->`."""
    if f.cls not in WAIVABLE or f.line < 1 or f.line > len(doc.lines):
        return False
    return any(m.group(1) == f.cls for m in ALLOW.finditer(doc.lines[f.line - 1]))


def current_branch(root: Path) -> str:
    r = subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=root, capture_output=True, text=True)
    return r.stdout.strip() if r.returncode == 0 else ""


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("files", nargs="*", help="Markdown files to check; default: every tracked one in scope")
    ap.add_argument("--offline", action="store_true", help="skip the checks that call GitHub")
    ap.add_argument("--today", help="UTC date to treat as today, YYYY-MM-DD")
    ap.add_argument("--repo", default=REPO_SLUG, help="owner/name for the GitHub checks")
    args = ap.parse_args(argv)

    top = subprocess.run(["git", "rev-parse", "--show-toplevel"], capture_output=True, text=True)
    if top.returncode != 0:
        print("check-docs: run it inside the repository (git rev-parse failed)", file=sys.stderr)
        return 2
    root = Path(top.stdout.strip())
    try:
        today = dt.date.fromisoformat(args.today) if args.today else dt.datetime.now(dt.timezone.utc).date()
    except ValueError:
        print("check-docs: --today must be YYYY-MM-DD", file=sys.stderr)
        return 2
    paths = args.files or tracked_markdown(root)
    paths = [p for p in paths if p.endswith(".md")]
    missing = [p for p in paths if not (root / p).is_file()]
    if missing:
        print(f"check-docs: not found: {', '.join(missing)}", file=sys.stderr)
        return 2

    gh = None if args.offline else GitHub(args.repo)
    try:
        findings = run(root, paths, today, gh, current_branch(root))
    except Unavailable as e:
        print(f"check-docs: could not check with GitHub ({e}); the refs and ADR-collision checks are unknown, not passed. Fix the access or hand off; --offline is for local iteration only", file=sys.stderr)
        return 2
    for f in findings:
        print(f)
    mode = "offline" if gh is None else "with GitHub"
    if findings:
        print(f"check-docs: {len(findings)} finding(s) in {len(paths)} file(s), {mode}")
        return 1
    print(f"check-docs: ok, {len(paths)} file(s), {mode}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
