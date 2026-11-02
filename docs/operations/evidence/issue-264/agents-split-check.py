"""Check the AGENTS.md split of issue #264 (third governance change).

Usage, from the repository root:
    python3 -I docs/operations/evidence/issue-264/agents-split-check.py <base-ref>

<base-ref> is the commit whose AGENTS.md is the "before" text, for example origin/main.

Check 1, map. Every block (paragraph or list item) of the base AGENTS.md is looked up,
verbatim, in the working-tree AGENTS.md and docs/agents/pull-requests.md. Relative links
in the moved file are resolved to repository paths before comparison. The script prints
where each block now lives. A block found in neither file is printed as CHANGED, with its
removed (-) and added (+) words, or as GONE.

Check 2, citations. Every citation of an AGENTS.md section or rule in tracked Markdown,
skill and agent files is resolved against the working tree:
- a quoted section name after "AGENTS.md" must be a heading of AGENTS.md;
- "Before opening a pull request" rule N must be a numbered rule of that section, in
  AGENTS.md (11 to 13) or docs/agents/pull-requests.md (1 to 10);
- a bare "AGENTS.md rule N" is read as "Before opening a pull request" rule N;
- hard safety rule N must be a numbered rule under "Hard safety rules";
- a link to AGENTS.md#anchor must match a heading anchor of AGENTS.md.
A rule 1 to 10 resolves through the pointer in AGENTS.md "Before opening a pull request",
which names docs/agents/pull-requests.md; the script counts those separately. A citation
that fails on the base AGENTS.md too is reported as PRE-EXISTING and is not a failure.
Exit status is 1 if any block is GONE or any citation newly fails to resolve. A CHANGED
block does not change the exit status: read its printed word diff by hand.
The moved file and the receipt for this change are not scanned for citations.
"""

import difflib
import os
import re
import subprocess
import sys

CORE = "AGENTS.md"
ROLE = "docs/agents/pull-requests.md"
# The receipt quotes citations and this script's output; its quotes are not citations.
RECEIPT = "docs/operations/evidence/issue-264/agents-split-receipt.md"


def blocks(text):
    """Split Markdown into paragraphs and top-level list items, stripped."""
    out, cur = [], []
    for line in text.split("\n"):
        starts_item = re.match(r"^(\d+\.|-) ", line) is not None
        if line.strip() == "" or starts_item or line.startswith("#"):
            if cur:
                out.append("\n".join(cur).strip())
            cur = []
            if line.strip() == "":
                continue
        cur.append(line)
    if cur:
        out.append("\n".join(cur).strip())
    return [b for b in out if b]


def resolve_links(text, file_dir):
    def fix(m):
        target = m.group(2)
        if re.match(r"^[a-z]+:|^#", target):
            return m.group(0)
        path, _, anchor = target.partition("#")
        resolved = os.path.normpath(os.path.join(file_dir, path))
        return "[%s](%s%s)" % (m.group(1), resolved, "#" + anchor if anchor else "")
    return re.sub(r"\[([^\]]*)\]\(([^)]+)\)", fix, text)


def slug(heading):
    s = heading.strip().lower()
    s = re.sub(r"[^\w\- ]", "", s)
    return s.replace(" ", "-")


def numbered_rules(text, heading):
    """Numbers of the top-level list items in the section under the given heading."""
    m = re.search(r"(?m)^(#+) " + re.escape(heading) + r"\s*$", text)
    if not m:
        return set()
    level = len(m.group(1))
    rest = text[m.end():]
    end = re.search(r"(?m)^#{1,%d} " % level, rest)
    sec = rest[: end.start()] if end else rest
    return {int(n) for n in re.findall(r"(?m)^(\d+)\. ", sec)}


def main():
    base = sys.argv[1]
    old = subprocess.run(["git", "show", base + ":" + CORE], capture_output=True, text=True, check=True).stdout
    core = open(CORE, encoding="utf-8").read()
    role = open(ROLE, encoding="utf-8").read()
    role_resolved = resolve_links(role, os.path.dirname(ROLE))

    failures = 0
    print("== Check 1: map of base AGENTS.md blocks ==")
    counts = {}
    for b in blocks(old):
        where = []
        for name, text in ((CORE, core), (ROLE, role_resolved)):
            if b in text:
                line = text[: text.index(b)].count("\n") + 1
                where.append("%s:%d" % (name, line))
        if where:
            status = "KEPT" if where[0].startswith(CORE) and len(where) == 1 else "MOVED" if len(where) == 1 else "BOTH"
        else:
            # Changed in place: the block's first 60 characters still open a block somewhere.
            head = b[:60]
            near = [n for n, t in ((CORE, core), (ROLE, role_resolved)) if head in t]
            status = "CHANGED" if near else "GONE"
            where = near
            if status == "GONE":
                failures += 1
        counts[status] = counts.get(status, 0) + 1
        print("%-7s %-40s %s" % (status, ", ".join(where) or "-", b[:70].replace("\n", " ")))
        if status == "CHANGED":
            text = core if where[0] == CORE else role_resolved
            start = text.index(head)
            new_block = next(x for x in blocks(text[start:]) if x.startswith(head))
            for op in difflib.ndiff(b.split(" "), new_block.split(" ")):
                if op[0] in "+-":
                    print("        " + op)
    print("Totals:", ", ".join("%s %d" % kv for kv in sorted(counts.items())))

    print("\n== Check 2: citations of AGENTS.md ==")

    def resolver(core_text, role_text):
        headings = {h.strip() for h in re.findall(r"(?m)^#{1,6} (.+)$", core_text)}
        return {
            "sections": headings | {re.sub(r" \(.*\)$", "", h) for h in headings},
            "anchors": {slug(h) for h in headings},
            "core_rules": numbered_rules(core_text, "Before opening a pull request"),
            "role_rules": numbered_rules(role_text, "Rules") if role_text else set(),
            "safety": numbered_rules(core_text, "Hard safety rules"),
        }

    new_r = resolver(core, role)
    base_r = resolver(old, "")
    pointer = ROLE in core[core.index("## Before opening a pull request"):]

    def check(r, kind, value):
        if kind == "section":
            return value in r["sections"]
        if kind == "anchor":
            return value in r["anchors"]
        if kind == "safety":
            return value in r["safety"]
        return value in r["core_rules"] or (value in r["role_rules"] and (r is base_r or pointer))

    files = subprocess.run(["git", "ls-files", "*.md", "*.yml", "*.yaml", ".agents/**", ".claude/**", ".github/**"],
                           capture_output=True, text=True, check=True).stdout.split()
    patterns = [
        ("section", r"AGENTS\.md(?:'s)?(?:,)?(?: GOVERNANCE)?(?:,)? \"([^\"]+)\"", False),
        ("rule", r"\"Before opening a pull request\",? rules? ((?:\d+(?:, | and | to )?)+)", True),
        ("rule", r"AGENTS\.md,? rules? ((?:\d+(?:, | and )?)+)", True),
        ("safety", r"[Hh]ard safety rules? ((?:\d+(?:, | and )?)+)", True),
        ("anchor", r"AGENTS\.md#([\w\-]+)", False),
    ]
    checked = via_pointer = pre_existing = 0
    for f in sorted(set(files)):
        if not os.path.isfile(f) or f in (ROLE, RECEIPT):
            continue
        text = open(f, encoding="utf-8", errors="replace").read()
        for kind, pattern, numeric in patterns:
            for m in re.finditer(pattern, text):
                values = [int(n) for n in re.findall(r"\d+", m.group(1))] if numeric else [m.group(1)]
                for v in values:
                    checked += 1
                    if check(new_r, kind, v):
                        if kind == "rule" and v not in new_r["core_rules"]:
                            via_pointer += 1
                        continue
                    if not check(base_r, kind, v):
                        pre_existing += 1
                        print("PRE-EXISTING %-8s %s: %s" % (kind, f, m.group(0)))
                        continue
                    failures += 1
                    print("UNRESOLVED   %-8s %s: %s" % (kind, f, m.group(0)))
    print("Rules 1 to 10 resolved through the pointer to %s: %d." % (ROLE, via_pointer))
    print("Pre-existing unresolved citations: %d." % pre_existing)
    print("Citations checked: %d. Failures: %d." % (checked, failures))
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
