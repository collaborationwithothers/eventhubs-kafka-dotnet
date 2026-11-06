"""Generates the six factory overview diagrams (the SVG files next to this script).

Run from anywhere: python3 docs/operations/factory-overview/generate-diagrams.py
It uses only the Python standard library and rewrites the SVG files in its own folder.
Each diagram is placed by hand-written coordinates; edit them here, then rerun.
The committed SVG files are the output of this script. Do not edit them directly.
"""
import os, sys
from xml.sax.saxutils import escape

OUT = os.path.dirname(os.path.abspath(__file__))
INK = "#1A202C"
MUTED = "#4A5568"
KINDS = {
    "human": ("#FFF4DB", "#B7791F"),
    "claude": ("#E6F0FB", "#2B6CB0"),
    "codex": ("#E3F5EA", "#2F855A"),
    "github": ("#EEF1F5", "#4A5568"),
    "azure": ("#EDE7F8", "#6B46C1"),
    "warn": ("#FDECEC", "#C53030"),
    "plain": ("#FFFFFF", "#718096"),
    "target": ("#DDF4EF", "#0F766E"),
}
FONT = "font-family=\"Segoe UI, Helvetica, Arial, sans-serif\""
warnings = []


class S:
    def __init__(self, name, w, h, title, sub, aria):
        self.name, self.w, self.h, self.p = name, w, h, []
        self.aria = aria
        self.p.append(f'<rect x="0" y="0" width="{w}" height="{h}" fill="#FFFFFF"/>')
        self.p.append(f'<text x="24" y="30" font-size="19" font-weight="700" fill="{INK}">{escape(title)}</text>')
        self.p.append(f'<text x="24" y="50" font-size="13" fill="{MUTED}">{escape(sub)}</text>')

    def lane(self, x, y, w, h, title):
        self.p.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="10" fill="#F7F8FA" stroke="#D8DEE6"/>')
        self.p.append(f'<text x="{x+40}" y="{y+15}" font-size="11.5" font-weight="700" fill="{MUTED}">{escape(title)}</text>')

    def box(self, x, y, w, h, title, lines=(), kind="plain", dash=False, tsize=13.5, sw=1.6, badge=None):
        fill, stroke = KINDS[kind]
        d = ' stroke-dasharray="6 4"' if dash else ""
        self.p.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="8" fill="{fill}" stroke="{stroke}" stroke-width="{sw}"{d}/>')
        if badge:
            self.p.append(f'<rect x="{x+10}" y="{y-9}" width="{len(badge)*7.4+14:.0f}" height="18" rx="9" fill="{stroke}"/>')
            self.p.append(f'<text x="{x+17}" y="{y+4}" font-size="11" font-weight="700" fill="#FFFFFF">{escape(badge)}</text>')
        for t in [title] + list(lines):
            width = len(t) * (7.3 if t == title else 6.6)
            if width > w - 12:
                warnings.append(f"{self.name}: '{t}' ~{int(width)}px > {w-12}")
        bh = 18 + 16 * len(lines)
        base = y + (h - bh) / 2 + 14
        cx = x + w / 2
        self.p.append(f'<text x="{cx}" y="{base:.1f}" font-size="{tsize}" font-weight="700" text-anchor="middle" fill="{INK}">{escape(title)}</text>')
        for i, t in enumerate(lines):
            self.p.append(f'<text x="{cx}" y="{base+20+16*i:.1f}" font-size="12.5" text-anchor="middle" fill="{INK}">{escape(t)}</text>')

    def arrow(self, pts, dash=False, head=True, color=INK):
        d = "M" + " L".join(f"{x},{y}" for x, y in pts)
        da = ' stroke-dasharray="5 4"' if dash else ""
        m = ' marker-end="url(#a)"' if head else ""
        self.p.append(f'<path d="{d}" fill="none" stroke="{color}" stroke-width="1.6"{da}{m}/>')

    def label(self, x, y, t, anchor="middle", rot=None, size=12, fill=INK, italic=False):
        tr = f' transform="rotate({rot} {x} {y})"' if rot else ""
        it = ' font-style="italic"' if italic else ""
        self.p.append(f'<text x="{x}" y="{y}" font-size="{size}" text-anchor="{anchor}" fill="{fill}" stroke="#FFFFFF" stroke-width="4" paint-order="stroke" stroke-linejoin="round"{tr}{it}>{escape(t)}</text>')

    def legend(self, y, items):
        x = 24
        for kind, label, dash in items:
            fill, stroke = KINDS[kind]
            d = ' stroke-dasharray="4 3"' if dash else ""
            self.p.append(f'<rect x="{x}" y="{y}" width="16" height="12" rx="3" fill="{fill}" stroke="{stroke}" stroke-width="1.5"{d}/>')
            self.p.append(f'<text x="{x+22}" y="{y+11}" font-size="12" fill="{INK}">{escape(label)}</text>')
            x += 22 + len(label) * 6.6 + 26

    def save(self):
        head = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {self.w} {self.h}" width="{self.w}" height="{self.h}" '
                f'role="img" aria-label="{escape(self.aria)}" {FONT}>'
                '<defs><marker id="a" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
                f'<path d="M0,0 L10,5 L0,10 z" fill="{INK}"/></marker></defs>')
        body = "\n".join(self.p)
        with open(os.path.join(OUT, self.name), "w") as f:
            f.write(head + "\n" + body + "\n</svg>\n")


# ---------------------------------------------------------------- 01
def d01():
    s = S("01-system-today.svg", 1240, 830, "How the factory runs today",
          "Sessions are started by hand. GitHub is the shared memory. Azure is reached only through workflows that sit behind GitHub environment gates.",
          "System overview today: Hari starts a control session by hand; it creates fresh Claude Code cloud sessions that share state through GitHub; Codex reviews pull requests automatically; Azure is reached only through workflows that sit behind GitHub environment gates.")
    s.lane(20, 70, 1200, 100, "YOU")
    s.lane(20, 185, 1200, 150, "CLAUDE CODE CLOUD SESSIONS (EACH STARTS WITH A FRESH CONTEXT)")
    s.lane(20, 350, 1200, 120, "GITHUB")
    s.lane(20, 485, 1200, 110, "CODEX")
    s.lane(20, 610, 1200, 130, "AUTOMATION AND AZURE")
    s.box(90, 90, 250, 60, "Hari", ["Approves item plans, verdicts", "and every scenario run"], "human")
    s.box(90, 225, 250, 90, "Control session", ["Started by hand", "Creates the sessions below"], "claude")
    cols = [400, 590, 780, 970]
    s.box(400, 240, 170, 70, "Plan session", ["posts the plan"], "claude")
    s.box(590, 240, 170, 70, "Plan check session", ["posts findings"], "claude")
    s.box(780, 240, 170, 70, "Cold review session", ["diff and receipt only"], "claude")
    s.box(970, 240, 170, 70, "Implement session", ["builds, pushes, merges"], "claude")
    s.arrow([(340, 270), (370, 270), (370, 222), (1055, 222)], head=False)
    for cx in (485, 675, 865, 1055):
        s.arrow([(cx, 222), (cx, 240)])
    s.label(700, 216, "create_session, model pinned per role", size=12)
    s.arrow([(215, 150), (215, 225)])
    s.label(225, 164, "prompt: skill + issue number", anchor="start")
    # github
    s.box(90, 370, 250, 80, "State file", ["items/N/state.md on main", "indexes the issue thread"], "github")
    s.box(400, 370, 360, 80, "Issues", ["Plan, plan check, decisions and", "handoffs: all stored as comments"], "github")
    s.box(780, 370, 170, 80, "main branch", ["no direct pushes;", "protected paths: Hari"], "github")
    s.box(970, 370, 170, 80, "PRs and branches", ["claude/, codex/, -wip"], "github")
    s.arrow([(340, 410), (400, 410)])
    s.label(370, 404, "indexes", size=11)
    s.arrow([(970, 410), (950, 410)])
    s.label(960, 364, "merge", size=11)
    s.arrow([(485, 310), (485, 370)])
    s.arrow([(675, 310), (675, 370)])
    s.label(580, 345, "comments", size=11.5)
    s.arrow([(1055, 310), (1055, 370)])
    s.label(1065, 345, "branch, PR", anchor="start", size=11.5)
    # codex
    s.box(90, 505, 250, 70, "Codex session", ["Started by hand, only for issues", "assigned to Codex"], "codex")
    s.box(780, 505, 360, 70, "Codex connector", ["Reviews every pull request,", "automatically or on @codex review"], "codex")
    s.arrow([(90, 120), (30, 120), (30, 540), (90, 540)])
    s.label(46, 330, "Hari starts by hand", rot=-90, size=11.5)
    s.arrow([(340, 540), (580, 540), (580, 450)])
    s.label(590, 510, "comments, branch, PR", anchor="start", size=11.5)
    s.arrow([(1010, 450), (1010, 505)])
    s.label(1020, 482, "opened", anchor="start", size=11)
    s.arrow([(1100, 505), (1100, 450)])
    s.label(1110, 482, "review", anchor="start", size=11)
    # automation
    s.box(970, 630, 170, 80, "Scenario workflow", ["dispatched by an agent,", "main branch only"], "github")
    s.box(780, 630, 170, 80, "Environment gate", ["Hari approves", "each scenario run"], "human")
    s.box(590, 630, 170, 80, "Azure", ["Event Hubs and AKS;", "OIDC identity only"], "azure")
    s.box(400, 630, 170, 80, "Sweeper workflow", ["destroys leftovers on", "a schedule, main only"], "github")
    s.arrow([(970, 670), (950, 670)])
    s.label(960, 624, "needs approval", size=11)
    s.arrow([(780, 670), (760, 670)])
    s.label(770, 624, "token", size=11)
    s.arrow([(570, 670), (590, 670)])
    s.label(580, 624, "cleans", size=11)
    s.arrow([(1140, 275), (1180, 275), (1180, 670), (1140, 670)])
    s.label(1200, 470, "dispatches a scenario run, main only", rot=-90, size=11.5)
    s.legend(770, [("human", "Hari", False), ("claude", "Claude Code cloud session", False), ("codex", "Codex", False), ("github", "GitHub", False), ("azure", "Azure", False)])
    s.save()


# ---------------------------------------------------------------- 02 sequence
def d02():
    W, H = 1240, 1060
    s = S("02-lifecycle-today.svg", W, H, "One work item from approval to verdict, today",
          "Read top to bottom. Each numbered arrow is one hand-off. Sessions never talk to each other except through the control session and GitHub.",
          "Sequence of one work item today: Hari approves the plan and prompts the control session, which creates plan, check, implement and review sessions; Codex reviews the pull request; an agent dispatches the Azure run, Hari approves it, and Hari approves the verdict.")
    cols = {"hari": 120, "ctl": 320, "cc": 560, "gh": 800, "cx": 985, "az": 1140}
    heads = [("hari", "Hari", [], "human", 120), ("ctl", "Control session", [], "claude", 150),
             ("cc", "Claude Code sessions", ["fresh context each"], "claude", 190),
             ("gh", "GitHub", [], "github", 120), ("cx", "Codex connector", [], "codex", 140),
             ("az", "Actions and Azure", [], "azure", 142)]
    for k, t, ls, kind, w in heads:
        s.box(cols[k] - w / 2, 66, w, 46, t, ls, kind, tsize=13)
    for k, x in cols.items():
        s.arrow([(x, 112), (x, H - 36)], dash=True, head=False, color="#A0AEC0")
    def step(y, a, b, text, dash=False):
        xa, xb = cols[a], cols[b]
        s.arrow([(xa, y), (xb, y)], dash=dash)
        s.label((xa + xb) / 2, y - 7, text, size=12)
    y = 150
    step(y, "hari", "gh", "1. Names the tool, applies ready-for-agent (item plan approved)")
    y += 54; step(y, "hari", "ctl", "2. Prompt: skill + issue")
    y += 54; step(y, "ctl", "cc", "3. create_session: plan"); step(y, "cc", "gh", "plan comment")
    y += 54; step(y, "ctl", "cc", "4. create_session: check"); step(y, "cc", "gh", "check: findings posted")
    y += 54; step(y, "ctl", "cc", "5. create_session: implement")
    y += 54; step(y, "ctl", "cc", "6. create_session: review")
    s.label(680, y - 7, "gets only diff + receipt", italic=True, size=12)
    y += 54; step(y, "cc", "gh", "7. push branch, open PR")
    y += 54; step(y, "gh", "cx", "8. connector picks it up")
    y += 54; step(y, "cx", "gh", "9. review: P0, P1, security")
    y += 54; step(y, "cc", "gh", "10. fix, then merge or hand off")
    y += 54; step(y, "cc", "az", "11. dispatch scenario run (main only)")
    y += 54; step(y, "hari", "az", "12. Hari approves the environment gate; run, then destroy")
    y += 54; step(y, "az", "cc", "13. results and logs")
    y += 54; step(y, "cc", "gh", "14. verdict PR + evidence pack")
    y += 54; step(y, "gh", "hari", "15. Hari approves or rejects the verdict")
    s.label(24, H - 46, "Steps 3 to 6 are created by the control session. Plan check and cold review run as separate sessions, as Hari runs them.", anchor="start", size=11.5, fill=MUTED)
    s.label(24, H - 30, "docs/agents/factory.md lets a passing plan check approve a plan only when the implement-plan session spawns the planner and checker itself;", anchor="start", size=11.5, fill=MUTED)
    s.label(24, H - 14, "with separate sessions as drawn, its documented approval is Hari's Approval comment. CLAUDE.md prescribes the repo-reviewer subagent for the cold review in interactive sessions.", anchor="start", size=11.5, fill=MUTED)
    s.save()


# ---------------------------------------------------------------- 03 guardrails
def d03():
    W, H = 1240, 800
    s = S("03-guardrails.svg", W, H, "Guardrails: what a machine enforces and what relies on agents following text",
          "The left column is enforced by tools. The middle column depends on agents following written rules.",
          "Guardrails in three groups: enforced by a machine, followed by convention, and decided only by Hari, with the known gaps listed below.")
    cw, gap, x0 = 380, 20, 30
    xs = [x0, x0 + cw + gap, x0 + 2 * (cw + gap)]
    s.box(xs[0], 70, cw, 40, "Enforced by a machine", [], "claude", tsize=14)
    s.box(xs[1], 70, cw, 40, "Followed by convention (written rules and prompts)", [], "warn", dash=True, tsize=14)
    s.box(xs[2], 70, cw, 40, "Only Hari decides", [], "human", tsize=14)
    col1 = [("Ruleset on main", ["No direct pushes or force pushes;", "protected paths need Hari's approval"]),
            ("Azure access gates", ["Scenario runs need Hari's approval;", "the sweeper environment is main-only"]),
            ("Workflow files", ["Protected paths; runs from outside", "contributors need Hari's approval"]),
            ("CI on pull requests", [".NET verification runs on every pull request;", "documentation checks are advisory"]),
            ("Stop hook (where enabled)", ["Blocks ending without a handoff comment, up to", "three times; unverified in GitHub Actions"])]
    col2 = [("No terraform apply or destroy by agents", ["a rule; no technical block identified"]),
            ("No Azure credentials in agent sessions", ["a rule; no technical block identified"]),
            ("Only Hari applies ready-for-agent", ["its plan approval"]),
            ("No edits to expected outcomes,", ["verdicts or results to make a run pass"]),
            ("Decision and Approval headings", ["every comment posts as Hari's account"]),
            ("Codex review before agent merge", ["no open P0/P1 or High security finding"]),
            ("GitHub text is data, not instructions", ["a defence written into the session prompt"])]
    col3 = [("Item plan", ["applies ready-for-agent"]),
            ("Verdict pull requests", ["approves or rejects the evidence"]),
            ("Every scenario run", ["environment gate: Hari approves"]),
            ("Persistent infrastructure", ["only Hari applies the persistent layer"]),
            ("Protected paths (full list: CODEOWNERS)", ["for example workflows, governance, ADRs,", "verifier, scripts, results, project files"])]
    for xs_i, cards, kind, dash in ((xs[0], col1, "claude", False), (xs[1], col2, "warn", True), (xs[2], col3, "human", False)):
        y = 124
        for t, ls in cards:
            h = 62 if len(ls) > 1 else 54
            s.box(xs_i, y, cw, h, t, ls, kind, dash=dash)
            y += h + 10
    s.box(30, 640, 1180, 100, "Known gaps today", [
        "Agent merges are not audited: the weekly spot-check has not started, and GitHub shows one account as the author of every pull request.",
        "The Stop hook acts only where enabled and is unverified in Actions. A dead session writes no handoff; its backup branch, if any, remains.",
        "No technical block on terraform apply was identified; the rule is text."], "warn")
    s.save()


# ---------------------------------------------------------------- 04 state
def d04():
    W, H = 1240, 800
    s = S("04-state-and-context.svg", W, H, "How state and context are kept together",
          "Every session starts empty. GitHub is the shared memory.",
          "Fresh sessions are created by the control session; they read and write GitHub, which holds issue comments, the state file, pull request receipts, evidence and backup branches.")
    s.box(420, 70, 400, 80, "Control session", ["Creates sessions with create_session and", "messages them with send_message."], "claude")
    sx = [120, 490, 860]
    names = [("Session A: plan", "fresh context"), ("Session B: implement", "fresh context"), ("Session C: next session", "fresh context")]
    for x, (t, l) in zip(sx, names):
        s.box(x, 220, 260, 80, t, [l, "reads a defined list, not the thread"], "claude")
    s.arrow([(620, 150), (620, 185), (250, 185), (250, 220)])
    s.arrow([(620, 185), (620, 220)])
    s.arrow([(620, 185), (990, 185), (990, 220)])
    s.label(430, 178, "create_session: prompt names the skill and the issue", anchor="middle", size=12)
    s.lane(60, 390, 1120, 250, "GITHUB: THE ONLY SHARED MEMORY")
    cards = [("Issue comments", ["plans, plan checks,", "decisions, handoffs"]),
             ("State file", ["items/N/state.md:", "what is in force now"]),
             ("PR and receipt", ["description, review", "reports, linked evidence"]),
             ("Evidence folders", ["results, logs, pod JSON,", "verifier counts"]),
             ("Backup branch", ["name-wip: saved work", "when a session dies"])]
    x = 90
    for t, ls in cards:
        s.box(x, 430, 198, 90, t, ls, "github")
        x += 214
    for sxi in sx:
        c = sxi + 130
        s.arrow([(c - 25, 300), (c - 25, 390)])
        s.label(c - 31, 350, "writes", anchor="end", size=11.5)
        s.arrow([(c + 25, 390), (c + 25, 300)])
        s.label(c + 31, 350, "catch-up read", anchor="start", size=11.5)
    s.label(620, 580, "Catch-up: a script lists comment IDs and edit times, then selects the comments the session's role needs.", size=12)
    s.label(620, 600, "A session re-checks for new or edited comments before it publishes a plan, pushes or merges.", size=12)
    s.box(60, 665, 560, 74, "Stop hook (where enabled)", ["Blocks a session from ending until it posts a comment starting", "'**Decision needed:**'; releases after three blocks; unverified in Actions"], "claude")
    s.box(640, 665, 540, 74, "When a session disappears", ["No handoff can be written. Comments and reviewed pushes remain;", "unpublished work survives only in a backup branch, if one was pushed."], "warn", dash=True)
    s.save()


# ---------------------------------------------------------------- 05 azure
def d05():
    W, H = 1200, 560
    s = S("05-azure-run.svg", W, H, "One Azure scenario run, and how cleanup is guaranteed",
          "An agent starts a run; Azure is reached through the workflow's OIDC identity. Cleanup does not depend on the run succeeding.",
          "Azure run flow: an agent dispatches the workflow, Hari approves the environment gate, the job applies and destroys the ephemeral layer, AKS stops when the dispatch ends, and the sweeper cleans up if a job dies.")
    xs = [40, 230, 420, 610, 800, 990]
    bw = 160
    items = [("1. Dispatch", ["agent starts the", "workflow, main only"], "claude"),
             ("2. Gate", ["Hari approves", "this run"], "human"),
             ("3. Apply", ["workflow applies the", "ephemeral layer"], "azure"),
             ("4. Run", ["load, faults, repeats;", "verifier judges"], "azure"),
             ("5. Destroy", ["ephemeral layer,", "pass or fail"], "azure"),
             ("6. Dispatch ends", ["AKS stops once,", "pass or fail"], "azure")]
    for x, (t, ls, k) in zip(xs, items):
        s.box(x, 100, bw, 90, t, ls, k)
    notes = ["waits", "OIDC token", "", "success or failure", "last job"]
    for i in range(5):
        a, b = xs[i] + bw, xs[i + 1]
        s.arrow([(a, 145), (b, 145)])
        if notes[i]:
            s.label((a + b) / 2, 92, notes[i], size=11)
    s.box(230, 270, 400, 100, "Sweeper workflow (scheduled)", [
        "When no dispatch is active: destroys a leftover",
        "ephemeral environment on its first sweep;",
        "stops a running AKS cluster 60 minutes after",
        "the last dispatch ended."], "github")
    s.arrow([(500, 270), (500, 190)], dash=True)
    s.label(510, 232, "safety net if a job dies", anchor="start", size=11.5)
    s.box(40, 430, 520, 74, "Persistent layer", ["Spoke network, registry, log workspace and identities.", "Only Hari applies it; agents never apply or destroy it."], "human")
    s.box(620, 430, 540, 74, "Evidence", ["Checked results go into a verdict pull request;", "Hari approves or rejects it."], "github")
    s.arrow([(690, 190), (690, 430)])
    s.label(700, 300, "checked results only", anchor="start", size=11.5)
    s.label(24, H - 14, "Rules: at most one ephemeral environment exists at a time; no Azure workflow triggers on pull_request.", anchor="start", size=11.5, fill=MUTED)
    s.save()


# ---------------------------------------------------------------- 06 target
def d06():
    W, H = 1240, 850
    s = S("06-target-state.svg", W, H, "Target state: sessions start themselves; a risk classifier lets low-risk pull requests merge automatically",
          "Dashed boxes are not built yet. The teal box is Hari's target. Hari still approves plans, verdicts and every escalation.",
          "Target state: Hari applies a label, a GitHub Actions workflow starts the sessions, and a merge workflow applies deterministic rules and a risk classifier. Hari's target is that a low classifier score lets a pull request merge automatically; anything flagged waits for Hari. The documented design differs, as the note states.")
    s.box(40, 80, 200, 70, "Hari", ["applies ready-for-agent"], "human")
    s.box(300, 80, 240, 70, "Orchestration workflow", ["GitHub Actions replaces the", "control session"], "claude", dash=True)
    s.box(600, 80, 300, 70, "Sessions started by the workflow", ["plan, check, implement, review;", "Codex implements in Actions too"], "claude", dash=True)
    s.box(960, 80, 200, 70, "Pull request", ["opened after the cold review"], "github")
    s.arrow([(240, 115), (300, 115)]); s.label(270, 74, "label", size=11)
    s.arrow([(540, 115), (600, 115)]); s.label(570, 74, "starts", size=11)
    s.arrow([(900, 115), (960, 115)]); s.label(930, 74, "opens", size=11)
    s.arrow([(1060, 150), (1060, 220)])
    s.box(900, 220, 260, 70, "Codex review", ["code review and", "security review"], "codex")
    s.arrow([(900, 255), (840, 255)])
    s.box(360, 220, 480, 70, "Merge workflow", ["reads the review status and the checks below,", "then decides: merge or escalate"], "github", dash=True)
    s.arrow([(600, 290), (600, 330)], head=False)
    s.arrow([(230, 330), (970, 330)], head=False)
    cx = [230, 600, 970]
    checks = [("Protected path touched?", ["ruleset requires Hari", "(built)"], "human", False),
              ("Codex silent for 60 minutes?", ["posts Review unavailable;", "waits for Hari"], "github", True),
              ("Risk classifier (on trial)", ["scores each pull request; shadow", "mode first: logs, no gating"], "github", True)]
    for x, (t, ls, k, d) in zip(cx, checks):
        s.arrow([(x, 330), (x, 360)])
        s.box(x - 150, 360, 300, 90, t, ls, k, dash=d)
    s.arrow([(190, 480), (970, 480)], head=False)
    for x in (230, 600, 970):
        s.arrow([(x, 450), (x, 480)], head=False)
    s.arrow([(190, 480), (190, 530)])
    s.arrow([(530, 480), (530, 530)])
    s.arrow([(880, 480), (880, 530)])
    s.label(200, 510, "today", anchor="start", size=11.5)
    s.label(540, 510, "target: low score", anchor="start", size=11.5)
    s.label(890, 510, "anything flagged", anchor="start", size=11.5)
    s.box(40, 530, 300, 70, "Agent merge today (built)", ["unprotected paths only,", "under the merge rule"], "claude")
    s.box(380, 530, 300, 70, "Automatic merge", ["a low classifier score lets the", "pull request merge itself"], "target", dash=True, sw=3, badge="TARGET")
    s.box(720, 530, 320, 70, "Waits for Hari", ["any escalation; never silent"], "human")
    s.box(40, 620, 1000, 62, "Not yet reconciled with the factory document", [
        "docs/agents/factory.md today: an adopted classifier only adds escalations, so it can narrow automatic merge but not widen it.",
        "Hari's target (6 November 2026): a low score lets a pull request merge automatically. See the open questions in issue 394."], "warn", dash=True)
    s.box(40, 700, 230, 100, "Same-vendor label", ["labels PRs with a Codex or no", "tool trailer; merge commits and", "Hari's own untrailed commits", "are ignored"], "github", dash=True)
    s.box(300, 700, 240, 100, "Weekly spot-check", ["Hari samples about 5 agent", "merges a week"], "human", dash=True)
    s.box(570, 700, 240, 100, "Escape-count script", ["counts serious defects the", "cold review should have caught"], "github", dash=True)
    s.box(840, 700, 330, 100, "Decision to trial the classifier", ["about 50 PRs labelled by spot-checks;", "a measured problem with the gate;", "general availability, or committed access", "with published pricing and data handling"], "human", dash=True)
    for x in (270, 540, 810):
        s.arrow([(x, 750), (x + 30, 750)])
    s.legend(822, [("human", "Hari", False), ("claude", "Claude Code", False), ("codex", "Codex", False), ("github", "GitHub", False), ("target", "Hari's target", True), ("plain", "dashed outline = not built yet", True)])
    s.save()


for fn in (d01, d02, d03, d04, d05, d06):
    fn()
for w in warnings:
    print("WARN", w)
print("ok")
