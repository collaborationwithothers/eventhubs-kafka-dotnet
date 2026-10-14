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
not be reached, in which case the GitHub-backed checks are unknown, not passed,
or the current branch cannot be told (detached HEAD, no GITHUB_HEAD_REF) while
docs/blueprint.md is checked online: run it on a branch, or check other files.
Output: one line per finding, `path:line: class: message`, then a summary.

One waiver exists, for the `date` class only: a date the text states as
planned or scheduled. Mark its line `<!-- check-docs: allow date: <why> -->`.
A value that is not a calendar date (`date-invalid`) cannot be waived.

Files: every tracked Markdown file, except the third-party skills pinned in
skills-lock.json. Paths given on the command line may be spelled relative to
the current directory or absolute; they are read as repository-relative.
Code blocks and HTML comments are excluded from structural and reference
checks. Inline code spans are also excluded from reference checks; heading
titles retain their inline code text. Date waivers are still read from the
original lines.

Sources: a document that defines sources (`| Sn | ... |` rows, or `- Sn: url`
lines) must reference every one of them; a number kept on purpose with no
source is written `- Sn: retired ...`.

Issue and pull request numbers (online only): every `#N` is this repository's and must
exist; `issue #N` must be an issue and `PR #N` or `pull request #N` a pull
request. Another repository's number is written GitHub's way, `owner/repo#N`.
A discussion's (`discussion #N`) is not checked.

ADR numbers across branches (online only): an ADR number new on this branch
is a finding when another branch uses it for a different ADR (a collision),
and when it is below the highest ADR another branch added. The branch whose
open pull request was created first keeps the number and gets no finding;
equal creation times are broken by the lower pull request number. A branch
with no open pull request renumbers when the other has one. When neither has
one, nobody gets a finding: the author could not follow one, because a pull
request needs the push. The first to open one keeps the number. The same heading
line on both branches is one ADR carried over, not a collision. ADRs already
on main are nobody's allocation, but a local heading that differs from main's
for the same number must be updated or renumbered. Candidates include target
repository branches and the source branches of open pull requests from forks.
A gap below main's highest ADR is a block
that an ADR on main reserved; a number in it is never "below another branch's
highest". The script does not know which branch a block was reserved for, so
a branch that takes a number from someone else's block is reported only when
that branch uses the same number; a gap left by a removed ADR counts as
reserved too.
"""

from __future__ import annotations

import argparse
import base64
import datetime as dt
import json
import os
import re
import subprocess
import sys
import unicodedata
from dataclasses import dataclass
from pathlib import Path
from urllib.parse import quote

REPO_SLUG = "collaborationwithothers/eventhubs-kafka-dotnet"
BLUEPRINT = "docs/blueprint.md"
DEFAULT_BRANCH = "main"

# Every tracked Markdown file is ours except the third-party skills pinned in
# skills-lock.json: they are copies and are not ours to fix. Discovery excludes
# those, so a new document anywhere in the repository is covered without
# editing this script.
SKILLS_LOCK = "skills-lock.json"
SKILL_ROOTS = (".agents/skills/", ".claude/skills/")
# Unit symbols the metric-units standard needs. Other non-ASCII punctuation and symbols are reported.
ALLOWED_NON_ASCII = {"\u00b0", "\u00b5"}
ALLOW = re.compile(r"<!-- check-docs: allow (date): [^>]*\S[^>]*-->")
WAIVABLE = ("date",)

ADR_HEADING = re.compile(r"^### ADR-(\d{3})\b")
# A heading that looks like an ADR's, in any form (`## ADR-1`, `### ADR 012`, `### ADR- 012`, `### adr-001`): the
# canonical form is `### ADR-NNN` with three digits, so anything else this matches is a typo.
ADR_LIKE_HEADING = re.compile(r"^#{1,6}\s+ADR\s*[-_]?\s*\d", re.I)
# The heading annotation that selects ADR_OPTION_PARTS
ADR_OPTION_HEADING = re.compile(r"^### ADR-\d{3} \(option, deferred\)")
ADR_REF = re.compile(r"\bADR-(\d{3})\b")
ADR_LIKE_REF = re.compile(r"\bADR[ \t]*[-_]?[ \t]*\d+\b", re.I)
ADR_PARTS = ("**Context.**", "**Options considered.**", "**Decision.**", "**Consequences.**")
# An ADR recorded as an option, not taken: `### ADR-NNN (option, deferred): ...`
ADR_OPTION_PARTS = ("**Context.**", "**Option.**", "**Status.**")
RETIRED = "retired"
SOURCE_TABLE_DEF = re.compile(r"^\| (S\d+) \|")
SOURCE_LIST_DEF = re.compile(r"^- (S\d+): ")
SOURCE_REF = re.compile(r"\bS\d+\b")
DATE = re.compile(r"(?<!\d)(\d{4})-(\d\d)-(\d\d)(?!\d)")  # also inside 2026-10-13T16:17Z
# `&` is excluded: `&#8212;` is a numeric character reference, not issue 8212
ISSUE_REF = re.compile(r"(?<![\w/#&])#(\d+)\b")
# Link destinations are not text: `[x](#12-3-options)`, `[x]: #5-modes`, `href="#5"`, `href='#5'`, `href=#5`, `<#5>`.
# They are blanked, same length, before references are looked for.
LINK_TARGET = re.compile(r"^ {0,3}\[[^\]]+\]:[ \t]*\S+.*$|href\s*=\s*(?:\"[^\"]*\"|'[^']*'|[^\s\"'>]+)|<[^<>\s]*>", re.I)
# Another repository's number is written GitHub's way, `owner/repo#N`, which ISSUE_REF never matches
# (the `#` follows a word character). A community discussion has no repository: `discussion #N`,
# or a list `discussions #1, #2 and #3`, is the one spaced form that is not ours.
# A label before a number, and the pieces of a group such as `PRs #6, #24 and #34`
KIND_LABEL = re.compile(r"\b(prs?|pull requests?|issues?)\s*$", re.I)
GROUP_SEPARATOR = re.compile(r"(?:,?\s+(?:and|or)\s+|\s*[,&]\s*)$", re.I)
PREVIOUS_REF = re.compile(r"(?<![\w/#])#\d+$")
DISCUSSION_REF = re.compile(r"(?:^|[\s(\[])discussions?(?:\s+#\d+(?:,|\s+and|\s+or)?)*(?:\s+(?:and|or))?\s+$", re.I)
# `file.md "Heading"`, `file.md, "Heading"` and `file.md's "Heading"`
FILE_HEADING_REF = re.compile(r"(?<![\w./:-])([\w.-]+(?:/[\w.-]+)*\.md)(?:,|'s)? \"([^\"]+)\"")
GOVERNANCE_REF = re.compile(r'GOVERNANCE "([^"]+)"')
BLUEPRINT_SECTION_REF = re.compile(r"docs/blueprint\.md section (\d+)\b")
HEADING = re.compile(r"^ {0,3}(#{1,6})(?:[ \t]+(.*?))?[ \t]*$")
SETEXT = re.compile(r"^ {0,3}(=+|-+)[ \t]*$")
THEMATIC_BREAK = re.compile(r"^ {0,3}(?:(?:\*[ \t]*){3,}|(?:_[ \t]*){3,}|(?:-[ \t]*){3,})$")
LIST_ITEM = re.compile(r"^( *)([-+*]|\d{1,9}[.)])([ \t]+)(.*)$")
BLOCKQUOTE = re.compile(r"^ {0,3}> ?")
FENCE = re.compile(r"^ {0,3}(`{3,}|~{3,})(.*)$")
INLINE_TOKEN = re.compile(r"\\.|`+|<!--")


def atx_heading(line: str) -> tuple[int, str] | None:
    """Normalize ATX indentation and closing hashes for every heading consumer."""
    m = HEADING.match(line)
    if m is None:
        return None
    title = re.sub(r"(?:^|[ \t]+)#+[ \t]*$", "", m.group(2) or "").strip(" \t")
    return len(m.group(1)), title


def opening_fence(line: str) -> re.Match | None:
    """Backtick fence info strings cannot contain backticks (CommonMark 0.31.2)."""
    match = FENCE.match(line)
    if match and (match.group(1)[0] != "`" or "`" not in match.group(2)):
        return match
    return None


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

    def semantic_lines(self, mask_inline: bool = False) -> list[tuple[str, bool]]:
        """Mask comments and exclude code blocks while preserving original line numbers."""
        out, fence, in_comment = [], None, False
        inline_end = None
        paragraph, code_indent = False, None
        list_indents: list[int] = []
        quote_end = -1
        for index, line in enumerate(self.lines):
            if index <= quote_end:
                continue
            visible, pos = "", 0
            inline_present = inline_end is not None
            expanded = line.expandtabs(4)
            indent = len(expanded) - len(expanded.lstrip(" "))
            base_indent = 0
            if inline_end is not None:
                if index < inline_end[0]:
                    out.append((" " * len(line) if mask_inline else line, False))
                    continue
                pos = inline_end[1]
                visible, inline_end = (" " * pos if mask_inline else line[:pos]), None
            if fence is not None:
                # A list's fence is relative to its content indentation. Leaving
                # that container also ends an otherwise unclosed fenced block.
                if line.strip() and indent < fence[2]:
                    fence = None
                    paragraph = False
                else:
                    m = FENCE.match(expanded[fence[2]:])
                    out.append((line, True))
                    if m and m.group(1)[0] == fence[0] and len(m.group(1)) >= fence[1] and not m.group(2).strip():
                        fence = None
                        paragraph = False
                    continue
            if not in_comment and pos == 0:
                if code_indent is not None:
                    if not line.strip() or indent >= code_indent:
                        out.append((line, True))
                        continue
                    code_indent = None
                    paragraph = False
                item = LIST_ITEM.match(expanded)
                if line.strip() and (not paragraph or item or atx_heading(line)):
                    while list_indents and indent < list_indents[-1]:
                        list_indents.pop()
                base_indent = list_indents[-1] if list_indents else 0
                if line.strip() and indent >= base_indent + 4 and not paragraph:
                    code_indent = base_indent + 4
                    out.append((line, True))
                    continue
                if item:
                    base_indent = item.start(4)
                    list_indents.append(base_indent)
                if BLOCKQUOTE.match(expanded[base_indent:]):
                    # Parse explicitly quoted lines in their own container, so
                    # a fence cannot consume prose after the blockquote ends.
                    quoted, prefixes = [], []
                    for n in range(index, len(self.lines)):
                        candidate = self.lines[n].expandtabs(4)
                        if n > index and len(candidate) - len(candidate.lstrip(" ")) < base_indent:
                            break
                        marker = BLOCKQUOTE.match(candidate[base_indent:])
                        if marker is None:
                            break
                        start = base_indent + marker.end()
                        prefixes.append(candidate[:start])
                        quoted.append(candidate[start:])
                    nested = Doc(self.path, quoted).semantic_lines(mask_inline)
                    out.extend((prefix + text, excluded) for prefix, (text, excluded) in zip(prefixes, nested))
                    quote_end = index + len(quoted) - 1
                    paragraph = False
                    continue
            m = opening_fence(expanded[base_indent:]) if not in_comment and pos == 0 else None
            if m:
                fence = (m.group(1)[0], len(m.group(1)), base_indent)
                paragraph = False
                out.append((line, True))
                continue
            while pos < len(line):
                if in_comment:
                    end = line.find("-->", pos)
                    stop = len(line) if end < 0 else end + len("-->")
                    visible += " " * (stop - pos)
                    pos, in_comment = stop, end < 0
                else:
                    token = INLINE_TOKEN.search(line, pos)
                    if token is None:
                        visible += line[pos:]
                        break
                    visible += line[pos:token.start()]
                    if token.group() == "<!--":
                        if line.startswith("<!-->", token.start()):
                            visible += " " * len("<!-->")
                            pos = token.start() + len("<!-->")
                        else:
                            pos, in_comment = token.start(), True
                    elif token.group().startswith("`"):
                        end = self.code_span_end(index, token.end(), token.group())
                        inline_present = inline_present or end is not None
                        if end is not None and end[0] > index:
                            visible += " " * (len(line) - token.start()) if mask_inline else line[token.start():]
                            inline_end = end
                            break
                        pos = end[1] if end is not None else token.end()
                        visible += " " * (pos - token.start()) if mask_inline and end is not None else line[token.start():pos]
                    else:
                        visible += token.group()
                        pos = token.end()
            m = opening_fence(visible)
            if m:
                fence = (m.group(1)[0], len(m.group(1)), 0)
            out.append((visible, fence is not None))
            paragraph = (bool(visible.strip()) or inline_present) and not (fence or atx_heading(visible) or SETEXT.match(visible) or THEMATIC_BREAK.match(visible))
        return out

    def code_span_end(self, index: int, start: int, delimiter: str) -> tuple[int, int] | None:
        """Find a matching backtick run before a paragraph or explicit block boundary."""
        closing = re.compile(r"(?<!`)" + re.escape(delimiter) + r"(?!`)")
        first_item = LIST_ITEM.match(self.lines[index])
        for n in range(index, len(self.lines)):
            line = self.lines[n]
            if n > index:
                item = LIST_ITEM.match(line)
                list_boundary = item and (
                    first_item is not None and len(item.group(1)) <= len(first_item.group(1))
                    or len(item.group(1)) <= 3 and item.group(4) and item.group(2) in ("-", "+", "*", "1.", "1)")
                )
                if (HEADING.match(self.lines[index]) or not line.strip() or HEADING.match(line)
                        or opening_fence(line) or THEMATIC_BREAK.match(line) or SETEXT.match(line)
                        or BLOCKQUOTE.match(line) or list_boundary):
                    break
            match = closing.search(line, start if n == index else 0)
            if match:
                return n, match.end()
        return None

    def fenced(self) -> list[bool]:
        """Per line: excluded as fenced or indented code, fence lines included."""
        return [f for _, f in self.semantic_lines()]

    def prose(self, mask_inline: bool = True) -> list[tuple[int, str]]:
        """`(line number, visible text)` outside code blocks and HTML comments."""
        return [(n, line) for n, (line, f) in enumerate(self.semantic_lines(mask_inline), 1) if not f]

    def heading_records(self) -> list[tuple[int, int, str]]:
        """Line number, level and normalized title for ATX and Setext headings.

        CommonMark 0.31.2, ATX/Setext headings and indented code blocks:
        https://github.com/commonmark/commonmark-spec/blob/9103e341a973013013bb1a80e13567007c5cef6f/spec.txt
        """
        out = []
        paragraph: list[tuple[int, str]] = []
        previous = 0
        for n, line in self.prose(mask_inline=False):
            if n != previous + 1:
                paragraph = []
            previous = n
            heading = atx_heading(line)
            underline = SETEXT.match(line)
            if heading:
                out.append((n, *heading))
                paragraph = []
            elif underline and paragraph:
                title = " ".join(text.strip() for _, text in paragraph)
                out.append((paragraph[0][0], 1 if underline.group(1)[0] == "=" else 2, title))
                paragraph = []
            elif not line.strip() or underline or THEMATIC_BREAK.match(line) or LIST_ITEM.match(line) or line.lstrip().startswith((">", "<")):
                paragraph = []
            else:
                paragraph.append((n, line))
        return out

    def headings(self) -> list[str]:
        return [title for _, _, title in self.heading_records()]

    def atx_lines(self) -> list[tuple[int, str]]:
        """ATX headings in canonical form, with source line numbers retained."""
        return [(n, f"{'#' * heading[0]} {heading[1]}") for n, line in self.prose(mask_inline=False) if (heading := atx_heading(line))]


# --- discovery --------------------------------------------------------------


def pinned_skill_prefixes(root: Path) -> tuple[str, ...]:
    """Path prefixes of the third-party skills pinned in skills-lock.json."""
    lock = root / SKILLS_LOCK
    if not lock.is_file():
        return ()
    names = json.loads(lock.read_text(encoding="utf-8")).get("skills", {})
    return tuple(f"{base}{name}/" for name in names for base in SKILL_ROOTS)


def tracked_markdown(root: Path) -> list[str]:
    out = subprocess.run(
        ["git", "ls-files", "-z", "*.md"], cwd=root, check=True, capture_output=True, text=True
    ).stdout.split("\0")
    pinned = pinned_skill_prefixes(root)
    return [p for p in out if p and not p.startswith(pinned)]


def repo_relative(root: Path, cwd: Path, given: str) -> str:
    """The path as `git ls-files` spells it: relative to the repository root, forward slashes.

    `./docs/blueprint.md`, an absolute path and a path given from a subdirectory
    all become `docs/blueprint.md`. Raises ValueError for a path outside the repository.
    """
    full = (cwd / given).resolve()
    return full.relative_to(root.resolve()).as_posix()


def load(root: Path, paths: list[str]) -> dict[str, Doc]:
    docs = {}
    for p in paths:
        docs[p] = Doc(p, (root / p).read_text(encoding="utf-8").split("\n"))
    return docs


# --- checks that need no network --------------------------------------------


def is_non_ascii_punctuation(c: str) -> bool:
    """True for a non-ASCII punctuation mark, symbol, separator or invisible format character.

    Letters, digits and combining marks (a name such as Zoë) pass: the rule is ASCII
    punctuation, not ASCII-only text.
    """
    return ord(c) > 0x7F and c not in ALLOWED_NON_ASCII and unicodedata.category(c)[0] in "PSZC"


def check_ascii(doc: Doc) -> list[Finding]:
    out = []
    for n, line in enumerate(doc.lines, 1):
        bad = next((c for c in line if is_non_ascii_punctuation(c)), None)
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
                out.append(Finding(doc.path, n, "date-invalid", f"{m.group(0)} is not a calendar date"))
                continue
            if d > today:
                out.append(Finding(doc.path, n, "date", f"{m.group(0)} is after today ({today})"))
    return out


def source_definitions(doc: Doc) -> dict[str, int]:
    """Source IDs this document defines, with the line of each definition."""
    defs: dict[str, int] = {}
    for n, line in doc.prose():
        m = SOURCE_TABLE_DEF.match(line) or SOURCE_LIST_DEF.match(line)
        if m:
            defs.setdefault(m.group(1), n)
    return defs


def retired_sources(doc: Doc) -> set[str]:
    """Source IDs defined as `- Sn: retired ...`: the number is kept, no reference is expected."""
    return {m.group(1) for _, line in doc.prose() if (m := SOURCE_LIST_DEF.match(line)) and line[m.end():].startswith(RETIRED)}


def check_sources(doc: Doc, docs: dict[str, Doc]) -> list[Finding]:
    """Every source reference resolves; every definition is referenced.

    A document that defines sources is self-contained. A document that
    defines none resolves its references against docs/blueprint.md.
    """
    out = []
    own = source_definitions(doc)
    counts: dict[str, int] = {}
    for n, line in doc.prose():
        m = SOURCE_TABLE_DEF.match(line) or SOURCE_LIST_DEF.match(line)
        if m:
            counts[m.group(1)] = counts.get(m.group(1), 0) + 1
    for sid, c in counts.items():
        if c > 1:
            out.append(Finding(doc.path, own[sid], "sources", f"{sid} is defined {c} times"))

    if own:
        defs, where, retired = own, doc.path, retired_sources(doc)
    elif BLUEPRINT in docs:
        defs, where, retired = source_definitions(docs[BLUEPRINT]), BLUEPRINT, retired_sources(docs[BLUEPRINT])
    else:
        return out

    referenced: set[str] = set()
    for n, line in doc.prose():
        line = mask_link_targets(line)
        definition = SOURCE_TABLE_DEF.match(line) or SOURCE_LIST_DEF.match(line)
        if definition:
            line = line[definition.end():]
        for m in SOURCE_REF.finditer(line):
            sid = m.group(0)
            referenced.add(sid)
            if sid not in defs:
                out.append(Finding(doc.path, n, "sources", f"{sid} is not defined in {where}"))
            elif sid in retired:
                out.append(Finding(doc.path, n, "sources", f"{sid} is retired in {where}, so it has no source to cite"))
    if own:
        for sid, n in own.items():
            if sid not in referenced and sid not in retired:
                out.append(Finding(doc.path, n, "sources", f"{sid} is defined but never referenced (write `- {sid}: retired ...` to keep the number without a source)"))
    return out


def adr_headings(doc: Doc) -> dict[str, int]:
    out: dict[str, int] = {}
    for n, line in doc.atx_lines():
        m = ADR_HEADING.match(line)
        if m:
            out.setdefault(m.group(1), n)
    return out


def adr_titles(doc: Doc) -> dict[str, str]:
    """ADR number to the full heading line, so the same ADR carried on two branches is not a collision."""
    return {m.group(1): line for _, line in doc.atx_lines() if (m := ADR_HEADING.match(line))}


def check_adr_numbers(doc: Doc, docs: dict[str, Doc]) -> list[Finding]:
    out = []
    if BLUEPRINT not in docs:
        return out
    bp = docs[BLUEPRINT]
    defined = adr_headings(bp)
    if doc.path == BLUEPRINT:
        seen: dict[str, int] = {}
        for n, line in doc.atx_lines():
            m = ADR_HEADING.match(line)
            if m:
                if m.group(1) in seen:
                    out.append(Finding(doc.path, n, "adr", f"ADR-{m.group(1)} heading repeats line {seen[m.group(1)]}"))
                seen.setdefault(m.group(1), n)
    for n, line in doc.prose():
        line = mask_link_targets(line)
        for m in ADR_LIKE_REF.finditer(line):
            if ADR_REF.fullmatch(m.group()) is None:
                out.append(Finding(doc.path, n, "adr", f"{m.group()} is not in the form ADR-NNN (three digits)"))
        for m in ADR_REF.finditer(line):
            if m.group(1) not in defined:
                out.append(Finding(doc.path, n, "adr", f"ADR-{m.group(1)} has no heading in {BLUEPRINT}"))
    return out


def check_adr_format(doc: Doc) -> list[Finding]:
    """Each ADR has Context, Options considered, Decision, Consequences, once each, in order.

    An ADR whose heading carries `(option, deferred)` has Context, Option, Status instead.
    A heading that looks like an ADR's but is not `### ADR-NNN` is reported, because
    the other checks would otherwise never see it.
    """
    out = []
    if doc.path != BLUEPRINT:
        return out
    for n, line in doc.atx_lines():
        if ADR_LIKE_HEADING.match(line) and not ADR_HEADING.match(line):
            out.append(Finding(doc.path, n, "adr-format", "heading is not in the form `### ADR-NNN` (three digits)"))
    headings = dict(doc.atx_lines())
    boundaries = [n for n, level, _ in doc.heading_records() if level <= 2]
    starts = [n for n, line in headings.items() if ADR_HEADING.match(line)]
    for i, start in enumerate(starts):
        end = starts[i + 1] - 1 if i + 1 < len(starts) else len(doc.lines)
        end = min([end] + [n - 1 for n in boundaries if start < n <= end])
        body_records = [(n, line) for n, line in doc.prose() if start < n <= end]
        body = [line for _, line in body_records]
        for stop, line in enumerate(body):
            if THEMATIC_BREAK.match(line):
                body = body[:stop]
                break
        parts = ADR_OPTION_PARTS if ADR_OPTION_HEADING.match(headings[start]) else ADR_PARTS
        first = next((l for l in body if l.strip()), "")
        if not first.startswith(parts[0]):
            out.append(Finding(doc.path, start, "adr-format", f"first paragraph does not start with {parts[0]}"))
        last = -1
        structural = dict(doc.prose(mask_inline=False))
        for part in parts:
            at = [k for k, l in enumerate(body) if l.startswith(part) and (
                k == 0 or not structural.get(body_records[k][0] - 1, "").strip()
                or atx_heading(structural.get(body_records[k][0] - 1, ""))
            )]
            if not at:
                out.append(Finding(doc.path, start, "adr-format", f"missing {part}"))
                continue
            if len(at) > 1:
                out.append(Finding(doc.path, start, "adr-format", f"{part} starts {len(at)} paragraphs; expected one"))
            if at[0] < last:
                out.append(Finding(doc.path, start, "adr-format", f"{part} is out of order"))
            else:
                last = at[0]
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
        # The whole heading, or the heading followed by a deliberate decoration: ` (...)` or `:`.
        return any(h == wanted or h.startswith(wanted + " (") or h.startswith(wanted + ":") for h in heads)

    for n, raw in doc.prose():
        line = mask_link_targets(raw)
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
    exist (HTTP 404, or 410 for a deleted issue), and raises Unavailable for anything else: `gh` missing,
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
        # 404: no such route. 410: a deleted issue, when the caller can read the repository (404 when not).
        # Source: https://docs.github.com/en/rest/issues/issues, "Get an issue", read 2026-10-14.
        if "HTTP 404" in r.stderr or "HTTP 410" in r.stderr:
            return None
        raise Unavailable(f"gh api {route}: {r.stderr.strip() or r.stdout.strip() or 'failed'}")


def mask_link_targets(line: str) -> str:
    masked = list(line)
    pos = 0
    while (start := line.find("](", pos)) >= 0:
        depth, title_quote, end = 1, None, start + 2
        while end < len(line) and depth:
            ch = line[end]
            if ch == "\\":
                end += 2
                continue
            if title_quote:
                if ch == title_quote:
                    title_quote = None
            elif ch in "\"'" and depth == 1 and line[end - 1].isspace():
                title_quote = ch
            elif ch == "(":
                depth += 1
            elif ch == ")":
                depth -= 1
            end += 1
        if depth == 0:
            masked[start:end] = " " * (end - start)
        pos = max(end, start + 2)
    return LINK_TARGET.sub(lambda m: " " * len(m.group(0)), "".join(masked))


def expected_kind(before: str) -> str | None:
    """`pull` or `issue` when the text before a number labels it, directly or as a later member of a group.

    In `PRs #6, #24 and #34` all three are pull requests. A group is broken by
    anything that is not a number of ours and a separator, so `PR #6, openai/codex#5 and #7`
    gives #7 no expected kind.
    """
    s = before
    while True:
        m = KIND_LABEL.search(s)
        if m:
            return "pull" if m.group(1).lower().startswith("p") else "issue"
        sep = GROUP_SEPARATOR.search(s)
        if not sep:
            return None
        s = s[:sep.start()]
        prev = PREVIOUS_REF.search(s)
        if not prev:
            return None
        s = s[:prev.start()]


def check_issue_references(doc: Doc, gh: GitHub, cache: dict[int, str | None]) -> list[Finding]:
    """`#N` exists; `issue #N` is an issue and `PR #N` or `pull request #N` is a pull request.

    A label covers the whole group it opens: `PRs #6, #24 and #34`. Another repository's number is written `owner/repo#N` and is never matched.
    A discussion's number (`discussion #N`) is skipped. Every other `#N` is ours.
    """
    out = []
    for n, raw in doc.prose():
        line = mask_link_targets(raw)
        for m in ISSUE_REF.finditer(line):
            num = int(m.group(1))
            if DISCUSSION_REF.search(line[:m.start()]):
                continue
            if num not in cache:
                data = gh.get(f"issues/{num}")
                # "You can identify pull requests by the `pull_request` key" (same page as above, "Get an issue").
                cache[num] = None if data is None else ("pull" if "pull_request" in data else "issue")
            kind = cache[num]
            if kind is None:
                out.append(Finding(doc.path, n, "refs", f"#{num} does not exist in {gh.slug} (another repository's number is written owner/repo#{num})"))
                continue
            wanted = expected_kind(line[:m.start()])
            if wanted == "pull" and kind != "pull":
                out.append(Finding(doc.path, n, "refs", f"#{num} is called a pull request but is an issue"))
            elif wanted == "issue" and kind != "issue":
                out.append(Finding(doc.path, n, "refs", f"#{num} is called an issue but is a pull request"))
    return out


def check_adr_branch_collisions(docs: dict[str, Doc], gh: GitHub, current_branch: str) -> list[Finding]:
    """An ADR number new on this branch is above every other branch's highest, and not new there too.

    Two cases are reported for each other branch: the same number used for a
    different ADR (a collision), and a number below the highest ADR that branch
    added, unless it is in a block `main` reserves (a gap below main's highest
    ADR). The cases and who renumbers are in the module docstring, under
    "ADR numbers across branches".
    """
    out = []
    if BLUEPRINT not in docs:
        return out
    main_doc = gh.get(f"contents/{BLUEPRINT}?ref={DEFAULT_BRANCH}")
    if main_doc is None:
        return out
    main_titles = adr_titles(Doc(BLUEPRINT, decode(main_doc).split("\n")))
    on_main = set(main_titles)
    # A gap below main's highest ADR is a block an ADR on main reserved (ADR-018 reserves the six
    # numbers after ADR-011 for issue #8). Numbers in it may sit below another branch's highest.
    reserved = {f"{i:03d}" for i in range(1, int(max(on_main, default="000")) + 1)} - on_main
    here = adr_headings(docs[BLUEPRINT])
    titles_here = adr_titles(docs[BLUEPRINT])
    for num in sorted(here.keys() & main_titles.keys()):
        if titles_here[num] != main_titles[num]:
            out.append(Finding(BLUEPRINT, here[num], "adr", f"ADR-{num} has a different heading on {DEFAULT_BRANCH}; update this branch or renumber the new decision"))
    new_here = {k: v for k, v in here.items() if k not in on_main}
    for num in sorted(new_here):
        if num < max(on_main, default="000") and num not in reserved:
            out.append(Finding(BLUEPRINT, new_here[num], "adr", f"ADR-{num} is below ADR-{max(on_main)}, the highest on {DEFAULT_BRANCH}; renumber the new decision"))
    if not new_here:
        return out
    candidates = {(gh.slug, b["name"]) for b in all_branches(gh)}
    for pr in all_open_pull_requests(gh):
        head = pr["head"]
        if head["repo"] is not None:
            candidates.add((head["repo"]["full_name"], head["ref"]))
    for repo, ref in sorted(candidates):
        name = ref if repo == gh.slug else f"{repo.split('/')[0]}:{ref}"
        if (repo == gh.slug and ref == DEFAULT_BRANCH) or branch_head(gh, name) == branch_head(gh, current_branch):
            continue
        source = gh if repo == gh.slug else GitHub(repo)
        data = source.get(f"contents/{BLUEPRINT}?ref={quote(ref, safe='/')}")
        if data is None:
            continue
        their_titles = adr_titles(Doc(BLUEPRINT, decode(data).split("\n")))
        # Their allocation is what they added; ADRs already on main are not theirs. A number
        # reserved below main's highest (ADR-018's block for issue #8) is not a finding.
        their_max = max((k for k in their_titles if k not in on_main), default=None)
        for num in sorted(new_here):
            if num in their_titles:
                if num in on_main or their_titles[num] == titles_here[num]:
                    continue
                reason = f"ADR-{num} is also new on branch {name}, with a different title"
            elif their_max is not None and num < their_max and num not in reserved:
                reason = f"ADR-{num} is below ADR-{their_max}, the highest on branch {name}"
            else:
                continue
            verdict = collision_verdict(gh, current_branch, name)
            if verdict is None:
                continue
            out.append(Finding(BLUEPRINT, new_here[num], "adr", f"{reason}; {verdict}"))
    return out


def branch_head(gh: GitHub, branch: str) -> str:
    """Keep an explicit source owner; bare names belong to the target repository."""
    return branch if ":" in branch else f"{gh.slug.split('/')[0]}:{branch}"


def pull_request_opened(gh: GitHub, branch: str) -> tuple[str, int] | None:
    """`(created_at, number)` of the branch's earliest open pull request, or None when it has none.

    The number breaks a tie between identical timestamps, so exactly one of two
    branches opened first.
    """
    prs = gh.get(f"pulls?state=open&head={quote(branch_head(gh, branch), safe='/:')}") or []
    return min(((p["created_at"], p["number"]) for p in prs), default=None)


def collision_verdict(gh: GitHub, ours: str, theirs: str) -> str | None:
    """None when our branch keeps a colliding ADR number, or nobody has decided it yet; otherwise why this branch renumbers."""
    mine, other = pull_request_opened(gh, ours), pull_request_opened(gh, theirs)
    if mine is not None and (other is None or mine < other):
        return None
    if mine is None and other is None:
        return None  # nothing to decide until one opens a pull request; the first to open one keeps the number
    if mine is None:
        return "this branch has no open pull request and that one has, so this branch renumbers"
    return "that branch's pull request opened first, so this branch renumbers"


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


def all_open_pull_requests(gh: GitHub) -> list[dict]:
    """Open PR heads include fork branches absent from the target repository's branch list."""
    out: list[dict] = []
    page = 1
    while True:
        batch = gh.get(f"pulls?state=open&per_page=100&page={page}") or []
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
    """The checked-out branch. On a detached HEAD, as in a pull_request workflow, the
    event's head ref; the default branch and `<n>/merge` refs are not working branches."""
    r = subprocess.run(["git", "rev-parse", "--abbrev-ref", "HEAD"], cwd=root, capture_output=True, text=True)
    name = r.stdout.strip() if r.returncode == 0 else ""
    head_ref = os.environ.get("GITHUB_HEAD_REF", "")
    event_path = os.environ.get("GITHUB_EVENT_PATH", "")
    if head_ref and event_path and (name in ("", "HEAD") or name == head_ref):
        # The event's source repository supplies the owner, including on fork PRs.
        try:
            head = json.loads(Path(event_path).read_text(encoding="utf-8"))["pull_request"]["head"]
            owner = head["repo"]["owner"]["login"]
            if head["ref"] != head_ref or not isinstance(owner, str) or not owner:
                raise ValueError("missing owner or mismatched head ref")
        except (OSError, ValueError, KeyError, TypeError) as e:
            raise Unavailable("cannot read the pull request's source owner from GITHUB_EVENT_PATH") from e
        return f"{owner}:{head_ref}"
    if name and name != "HEAD":
        return name
    # GITHUB_HEAD_REF is the pull request's source branch and is set only for pull_request and
    # pull_request_target events; GITHUB_REF_NAME is `<n>/merge` for an unmerged pull request.
    # Source: https://docs.github.com/en/actions/reference/workflows-and-actions/variables, read 2026-10-14.
    for var in ("GITHUB_HEAD_REF", "GITHUB_REF_NAME"):
        ref = os.environ.get(var, "")
        if ref and ref != DEFAULT_BRANCH and not ref.endswith("/merge"):
            return ref
    return ""


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
    try:
        given = [repo_relative(root, Path.cwd(), f) for f in args.files]
    except ValueError as e:
        print(f"check-docs: a file is outside the repository ({e})", file=sys.stderr)
        return 2
    paths = list(dict.fromkeys(given)) if args.files else tracked_markdown(root)
    paths = [p for p in paths if p.endswith(".md")]
    missing = [p for p in paths if not (root / p).is_file()]
    if missing:
        print(f"check-docs: not found: {', '.join(missing)}", file=sys.stderr)
        return 2

    gh = None if args.offline else GitHub(args.repo)
    try:
        branch = current_branch(root) if gh is not None and BLUEPRINT in paths else ""
        if gh is not None and BLUEPRINT in paths and not branch:
            print("check-docs: cannot tell the current branch (detached HEAD and no GITHUB_HEAD_REF); the ADR-collision check needs it", file=sys.stderr)
            return 2
        findings = run(root, paths, today, gh, branch)
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
