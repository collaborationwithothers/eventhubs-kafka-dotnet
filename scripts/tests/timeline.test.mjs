// Tests for scripts/timeline/event.sh and scripts/timeline/render.sh.
// Run: node --test scripts/tests/timeline.test.mjs
import test from "node:test";
import assert from "node:assert/strict";
import { spawnSync, execFileSync } from "node:child_process";
import { mkdtempSync, writeFileSync, existsSync, readFileSync, mkdirSync, copyFileSync, chmodSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const eventSh = join(repoRoot, "scripts/timeline/event.sh");
const renderSh = join(repoRoot, "scripts/timeline/render.sh");
const UTC = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/;
const BRANCH = "claude/443-session-timeline";
const isRoot = typeof process.getuid === "function" && process.getuid() === 0;

function tmp() {
  return mkdtempSync(join(tmpdir(), "timeline-"));
}

function run(script, args, { env = {}, cwd = repoRoot } = {}) {
  const result = spawnSync(script, args, { cwd, encoding: "utf8", env: { ...process.env, ...env } });
  return { status: result.status, stdout: result.stdout, stderr: result.stderr };
}

function event(args, logPath, extra = {}) {
  return run(eventSh, args, { env: { PARCHWELL_TIMELINE_LOG: logPath, ...extra } });
}

function lines(path) {
  return readFileSync(path, "utf8").split("\n").filter((l) => l !== "");
}

const USAGE_EVENT = "Usage: scripts/timeline/event.sh <event> [<detail>]\n";
const USAGE_RENDER =
  "Usage: scripts/timeline/render.sh --branch <name> --log <file> [--local-gate required|not-applicable|earlier-session] [--summary <file>]... [--git-range <base>..<head>] [--comments <file>]... [--reviews <file>]... [--jobs <file>]... [--since <UTC>]\n";
const GATE_ERR = "The pre-PR local gate has no recorded start and end.\n";

// ---------- event.sh ----------

test("event_line_format", () => {
  const log = join(tmp(), "log.jsonl");
  const r = event(["gate.start", "sandbox"], log);
  assert.equal(r.status, 0);
  const l = lines(log);
  assert.equal(l.length, 1);
  const o = JSON.parse(l[0]);
  assert.deepEqual(Object.keys(o), ["v", "ts", "event", "detail", "branch", "head"]);
  assert.equal(o.v, 1);
  assert.match(o.ts, UTC);
  assert.equal(o.event, "gate.start");
  assert.equal(o.detail, "sandbox");
  assert.equal(o.head, execFileSync("git", ["rev-parse", "HEAD"], { cwd: repoRoot, encoding: "utf8" }).trim());
  const b = execFileSync("git", ["rev-parse", "--abbrev-ref", "HEAD"], { cwd: repoRoot, encoding: "utf8" }).trim();
  assert.equal(o.branch, b === "HEAD" ? null : b);
});

test("event_vocabulary_accepts", () => {
  const log = join(tmp(), "log.jsonl");
  const calls = [
    ["gate.start", "full"], ["gate.pass", "docs"], ["gate.fail", "sandbox"], ["gate.stop", "full"],
    ["focused.run", "fast"], ["focused.run", "project"], ["review.start", "p2-correction"],
    ["review.end", "comprehensive P0=0 P1=0 P2=0"], ["review.end", "focused P0=999999 P1=1 P2=12"],
    ["env.interrupted"], ["agent.resumed"], ["handoff", "hari"], ["handoff", "next-session"],
    ["handoff", "completion"], ["decision.received", "6848185690"], ["decision.received", "12345678901234567890"],
  ];
  for (const call of calls) assert.equal(event(call, log).status, 0, call.join(" "));
  const l = lines(log).map((x) => JSON.parse(x));
  assert.equal(l.length, 16);
  calls.forEach((call, i) => {
    assert.equal(l[i].event, call[0]);
    assert.equal(l[i].detail, call[1] ?? "");
  });
});

test("event_arg_count", () => {
  const log = join(tmp(), "log.jsonl");
  for (const args of [[], ["handoff", "hari", "extra"]]) {
    const r = event(args, log);
    assert.equal(r.status, 2);
    assert.equal(r.stderr, USAGE_EVENT);
  }
  assert.equal(existsSync(log), false);
});

test("event_unknown", () => {
  const log = join(tmp(), "log.jsonl");
  let r = event(["gate.begin", "sandbox"], log);
  assert.equal(r.status, 2);
  assert.equal(r.stderr, "Unknown event: gate.begin\n");
  r = event(["GATE.START", "sandbox"], log);
  assert.equal(r.status, 2);
  assert.equal(r.stderr, "Unknown event.\n");
  assert.equal(existsSync(log), false);
});

test("event_bad_detail", () => {
  const log = join(tmp(), "log.jsonl");
  const calls = [
    ["gate.start", "/home/user"], ["gate.start"], ["review.end", "comprehensive P0=1 P1=2"],
    ["review.end", "comprehensive P0=-1 P1=0 P2=0"], ["review.end", "comprehensive P0=0 P1=0 P2=1234567"],
    ["review.end", "fast P0=0 P1=0 P2=0"], ["env.interrupted", "x"], ["decision.received", ""],
    ["decision.received", "123a"], ["decision.received", "123456789012345678901"], ["focused.run", "sandbox"],
  ];
  for (const call of calls) {
    const r = event(call, log);
    assert.equal(r.status, 2, call.join(" "));
    assert.equal(r.stderr, `Invalid detail for ${call[0]}.\n`);
    if (call[1]) assert.equal(r.stderr.includes(call[1]), false);
  }
  assert.equal(existsSync(log), false);
});

test("event_relative_path", () => {
  const dir = tmp();
  const r = run(eventSh, ["handoff", "hari"], { env: { PARCHWELL_TIMELINE_LOG: "timeline.jsonl" }, cwd: dir });
  assert.equal(r.status, 2);
  assert.equal(r.stderr, "The timeline log path must be absolute.\n");
  assert.equal(existsSync(join(dir, "timeline.jsonl")), false);
});

test("event_inside_repo", () => {
  const inside = join(repoRoot, "timeline-test.jsonl");
  for (const path of [inside, repoRoot]) {
    const r = event(["handoff", "hari"], path);
    assert.equal(r.status, 2);
    assert.equal(r.stderr, "The timeline log must be outside the repository.\n");
  }
  assert.equal(existsSync(inside), false);
});

function makeRepoCopy(base) {
  const repo = join(base, "repo");
  mkdirSync(join(repo, "scripts/timeline"), { recursive: true });
  execFileSync("git", ["init", "-q", repo]);
  const copy = join(repo, "scripts/timeline/event.sh");
  copyFileSync(eventSh, copy);
  chmodSync(copy, 0o755);
  return { repo, copy };
}

test("event_root_prefix_sibling", () => {
  const base = tmp();
  const { repo, copy } = makeRepoCopy(base);
  const sibling = join(base, "repo-sibling.jsonl");
  let r = run(copy, ["handoff", "hari"], { env: { PARCHWELL_TIMELINE_LOG: sibling } });
  assert.equal(r.status, 0);
  assert.equal(lines(sibling).length, 1);
  r = run(copy, ["handoff", "hari"], { env: { PARCHWELL_TIMELINE_LOG: join(repo, "x.jsonl") } });
  assert.equal(r.status, 2);
  assert.equal(r.stderr, "The timeline log must be outside the repository.\n");
  assert.equal(existsSync(join(repo, "x.jsonl")), false);
});

test("event_default_path", () => {
  const dir = tmp();
  const r = run(eventSh, ["handoff", "hari"], { env: { PARCHWELL_TIMELINE_LOG: "", TMPDIR: dir } });
  assert.equal(r.status, 0);
  assert.equal(lines(join(dir, "parchwell-timeline.jsonl")).length, 1);
});

test("event_no_git", () => {
  const log = join(tmp(), "log.jsonl");
  const r = event(["handoff", "hari"], log, { GIT_DIR: "/nonexistent" });
  assert.equal(r.status, 0);
  const o = JSON.parse(lines(log)[0]);
  assert.equal(o.branch, null);
  assert.equal(o.head, null);
});

function gitIn(repo, args, env = {}) {
  return execFileSync("git", ["-C", repo, ...args], {
    encoding: "utf8",
    env: { ...process.env, GIT_AUTHOR_NAME: "t", GIT_AUTHOR_EMAIL: "t@t", GIT_COMMITTER_NAME: "t", GIT_COMMITTER_EMAIL: "t@t", ...env },
  }).trim();
}

test("event_detached", () => {
  const base = tmp();
  const repo = join(base, "r");
  execFileSync("git", ["init", "-q", repo]);
  gitIn(repo, ["commit", "-q", "--allow-empty", "-m", "c"]);
  gitIn(repo, ["checkout", "-q", "--detach"]);
  const head = gitIn(repo, ["rev-parse", "HEAD"]);
  const log = join(base, "log.jsonl");
  const r = event(["handoff", "hari"], log, { GIT_DIR: join(repo, ".git"), GIT_WORK_TREE: repo });
  assert.equal(r.status, 0);
  const o = JSON.parse(lines(log)[0]);
  assert.equal(o.branch, null);
  assert.equal(o.head, head);
});

test("event_append_order", () => {
  const log = join(tmp(), "log.jsonl");
  event(["handoff", "hari"], log);
  event(["agent.resumed"], log);
  assert.deepEqual(lines(log).map((l) => JSON.parse(l).event), ["handoff", "agent.resumed"]);
});

test("event_write_failure", { skip: isRoot ? "running as root: every path is writable" : false }, () => {
  const base = tmp();
  const ro = join(base, "ro");
  mkdirSync(ro);
  chmodSync(ro, 0o500);
  const r = event(["handoff", "hari"], join(ro, "x.jsonl"));
  assert.equal(r.status, 1);
  assert.equal(r.stderr, "The timeline event could not be written.\n");
});

// ---------- render.sh ----------

const logLines = [
  { ts: "2026-11-13T10:00:00Z", event: "gate.start", detail: "sandbox" },
  { ts: "2026-11-13T09:00:00Z", event: "gate.start", detail: "sandbox", branch: "claude/999-other" },
  { ts: "2026-11-13T10:05:30Z", event: "gate.pass", detail: "sandbox" },
  { ts: "2026-11-13T10:06:00Z", event: "review.start", detail: "comprehensive" },
  { ts: "2026-11-13T09:30:00Z", event: "handoff", detail: "hari", branch: null },
  { ts: "2026-11-13T10:20:00Z", event: "review.end", detail: "comprehensive P0=0 P1=1 P2=2" },
  { ts: "2026-11-13T10:21:00Z", event: "gate.fail", detail: "docs" },
  { ts: "2026-11-13T10:30:00Z", event: "focused.run", detail: "project" },
  { ts: "2026-11-13T11:00:00Z", event: "gate.start", detail: "sandbox" },
  { ts: "2026-11-13T11:01:00Z", event: "gate.start", detail: "sandbox" },
  { ts: "2026-11-13T11:02:00Z", event: "gate.stop", detail: "sandbox" },
].map((l) => JSON.stringify({ v: 1, ts: l.ts, event: l.event, detail: l.detail, branch: "branch" in l ? l.branch : BRANCH, head: null }));

function logFile(dir, name, picks) {
  const path = join(dir, name);
  writeFileSync(path, picks.map((n) => logLines[n - 1]).join("\n") + "\n");
  return path;
}

function render(dir, args, opts = {}) {
  return run(renderSh, args, { cwd: opts.cwd ?? dir });
}

function json(dir, name, value) {
  const path = join(dir, name);
  writeFileSync(path, typeof value === "string" ? value : JSON.stringify(value));
  return path;
}

const summaryFixture = {
  schema_version: 1,
  started_utc: "2026-11-13T10:00:05Z",
  ended_utc: "2026-11-13T10:05:25Z",
  tier: "sandbox",
  outcome: "partial_passed",
  exit_code: 0,
  stages: [{ label: "Restore", elapsed_s: 99, start_utc: "2026-11-13T10:00:06Z", end_utc: "2026-11-13T10:00:16Z", exit_code: 0 }],
};
const commentsFixture = [{ id: 1, created_at: "2026-11-13T10:25:00Z", body: "x | y" }, { id: 2, created_at: "2026-11-13T08:00:00Z" }];
const reviewsFixture = [
  { id: 7, user: { login: "chatgpt-codex-connector[bot]" }, state: "COMMENTED", submitted_at: "2026-11-13T10:40:00.123Z", commit_id: "0123456789abcdef0123456789abcdef01234567" },
  { id: 8, user: null, state: "PENDING", commit_id: null },
  { id: 9, user: null, state: "PENDING", submitted_at: null, commit_id: null },
];
const jobsFixture = {
  total_count: 1,
  jobs: [{
    name: "dotnet-tests (A|B) <x> & y", started_at: "2026-11-13T10:35:00Z", completed_at: "2026-11-13T10:39:30Z", conclusion: "success",
    steps: [
      { name: "Run", number: 1, started_at: "2026-11-13T10:35:10Z", completed_at: "2026-11-13T10:39:00Z", conclusion: "success" },
      { name: "Skipped", number: 2, started_at: null, completed_at: null, conclusion: "skipped" },
      { name: "Upload", number: 3, started_at: "2026-11-13T10:39:05Z", completed_at: null, conclusion: null },
      { name: "Queued", number: 4, conclusion: null },
    ],
  }],
};

const NO_ROWS_HEAD = "<details><summary>Timeline: no rows</summary>\n\nhook data: absent\n";
const NO_ROWS_TAIL =
  "Rows: log absent, summary absent, git absent, comment absent, review absent, ci absent.\nSeconds are computed only from recorded start and end pairs.\n\nNo rows from any source.\n\n</details>\n";

function gitFixture(base) {
  const repo = join(base, "gitrepo");
  execFileSync("git", ["init", "-q", repo]);
  const a = (gitIn(repo, ["commit", "-q", "--allow-empty", "-m", "A"]), gitIn(repo, ["rev-parse", "HEAD"]));
  gitIn(repo, ["commit", "-q", "--allow-empty", "-m", "B"], { GIT_COMMITTER_DATE: "@1794564600 +0000" });
  const b = gitIn(repo, ["rev-parse", "HEAD"]);
  gitIn(repo, ["commit", "-q", "--allow-empty", "-m", "C"], { GIT_COMMITTER_DATE: "@1794564900 +0000" });
  const c = gitIn(repo, ["rev-parse", "HEAD"]);
  return { repo, a, b, c };
}

test("render_full", () => {
  const dir = tmp();
  const g = gitFixture(dir);
  const result = render(dir, [
    "--branch", BRANCH, "--log", logFile(dir, "log.jsonl", [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]),
    "--summary", json(dir, "summary.json", summaryFixture), "--git-range", `${g.a}..${g.c}`,
    "--comments", json(dir, "comments.json", commentsFixture), "--reviews", json(dir, "reviews.json", reviewsFixture),
    "--jobs", json(dir, "jobs.json", jobsFixture),
  ], { cwd: g.repo });
  const expected = `<details><summary>Timeline 2026-11-13T10:00:00Z to 2026-11-13T11:02:00Z (UTC)</summary>

hook data: absent
event log: present
pre-PR local gate: recorded
Rows: log 9, summary 2, git 2, comment 1, review 1, ci 3.
Seconds are computed only from recorded start and end pairs.

| UTC | Source | Event | Detail | Seconds |
|---|---|---|---|---|
| 2026-11-13T10:00:00Z | log | gate.start | sandbox |  |
| 2026-11-13T10:00:16Z | summary | verify.stage | Restore exit=0 | 10 |
| 2026-11-13T10:05:25Z | summary | verify.run | tier=sandbox outcome=partial_passed exit=0 | 320 |
| 2026-11-13T10:05:30Z | log | gate.pass | sandbox | 330 |
| 2026-11-13T10:06:00Z | log | review.start | comprehensive |  |
| 2026-11-13T10:10:00Z | git | commit | ${g.b.slice(0, 12)} |  |
| 2026-11-13T10:15:00Z | git | commit | ${g.c.slice(0, 12)} |  |
| 2026-11-13T10:20:00Z | log | review.end | comprehensive P0=0 P1=1 P2=2 | 840 |
| 2026-11-13T10:21:00Z | log | gate.fail | docs |  |
| 2026-11-13T10:25:00Z | comment | comment | 1 |  |
| 2026-11-13T10:30:00Z | log | focused.run | project |  |
| 2026-11-13T10:39:00Z | ci | ci.step | dotnet-tests (A\\|B) &lt;x&gt; &amp; y / Run success | 230 |
| 2026-11-13T10:39:05Z | ci | ci.step | dotnet-tests (A\\|B) &lt;x&gt; &amp; y / Upload pending |  |
| 2026-11-13T10:39:30Z | ci | ci.job | dotnet-tests (A\\|B) &lt;x&gt; &amp; y success | 270 |
| 2026-11-13T10:40:00Z | review | review | COMMENTED by chatgpt-codex-connector[bot] at 0123456789ab |  |
| 2026-11-13T11:00:00Z | log | gate.start | sandbox |  |
| 2026-11-13T11:01:00Z | log | gate.start | sandbox |  |
| 2026-11-13T11:02:00Z | log | gate.stop | sandbox | 60 |

</details>
`;
  assert.equal(result.stderr, "");
  assert.equal(result.status, 0);
  assert.equal(result.stdout, expected);
});

test("render_since", () => {
  const dir = tmp();
  const r = render(dir, [
    "--branch", BRANCH, "--since", "2026-11-13T10:30:00Z", "--log", logFile(dir, "log.jsonl", [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]),
    "--comments", json(dir, "comments.json", commentsFixture),
  ]);
  assert.equal(r.status, 0);
  assert.match(r.stdout, /\| 2026-11-13T10:30:00Z \| log \| focused\.run \| project \|  \|\n/);
  assert.match(r.stdout, /\| 2026-11-13T11:02:00Z \| log \| gate\.stop \| sandbox \| 60 \|\n/);
  assert.match(r.stdout, /Rows: log 4, summary absent, git absent, comment 0, review absent, ci absent\.\n/);
  assert.match(r.stdout, /pre-PR local gate: recorded\n/);
  assert.equal(r.stdout.includes("10:25:00Z"), false);
});

test("render_since_default_branch_only", () => {
  const dir = tmp();
  const r = render(dir, [
    "--branch", BRANCH, "--local-gate", "not-applicable", "--log", logFile(dir, "log.jsonl", [2, 1]),
    "--comments", json(dir, "comments.json", [{ id: 3, created_at: "2026-11-13T09:30:00Z" }]),
  ]);
  assert.equal(r.status, 0);
  assert.equal(r.stderr, "");
  assert.match(r.stdout, /Rows: log 1, summary absent, git absent, comment 0, review absent, ci absent\.\n/);
});

test("render_missing_log", () => {
  const dir = tmp();
  const r = render(dir, ["--branch", BRANCH, "--log", join(dir, "none.jsonl"), "--comments", json(dir, "comments.json", commentsFixture)]);
  assert.equal(r.status, 3);
  assert.equal(r.stderr, GATE_ERR);
  assert.match(r.stdout, /event log: missing\n/);
  assert.match(r.stdout, /pre-PR local gate: MISSING, no recorded start and end\n/);
  assert.match(r.stdout, /Rows: log absent, summary absent, git absent, comment 2, review absent, ci absent\.\n/);
  assert.match(r.stdout, /\| 2026-11-13T08:00:00Z \| comment \| comment \| 2 \|  \|\n/);
  assert.match(r.stdout, /\| 2026-11-13T10:25:00Z \| comment \| comment \| 1 \|  \|\n/);
});

const emptyBlock = (gate) => `${NO_ROWS_HEAD}event log: missing\npre-PR local gate: ${gate}\n${NO_ROWS_TAIL}`;

test("render_empty", () => {
  const dir = tmp();
  const r = render(dir, ["--branch", "x", "--log", join(dir, "none.jsonl")]);
  assert.equal(r.status, 3);
  assert.equal(r.stderr, GATE_ERR);
  assert.equal(r.stdout, emptyBlock("MISSING, no recorded start and end"));
});

test("render_empty_declared", () => {
  const dir = tmp();
  const r = render(dir, ["--branch", "x", "--log", join(dir, "none.jsonl"), "--local-gate", "not-applicable"]);
  assert.equal(r.status, 0);
  assert.equal(r.stderr, "");
  assert.equal(r.stdout, emptyBlock("not applicable (declared)"));
});

test("render_log_empty_file", () => {
  const dir = tmp();
  for (const content of ["", "\n\n"]) {
    const r = render(dir, ["--branch", BRANCH, "--local-gate", "not-applicable", "--log", json(dir, "e.jsonl", content)]);
    assert.equal(r.status, 0);
    assert.match(r.stdout, /event log: empty\n/);
    assert.match(r.stdout, /Rows: log 0, /);
    assert.match(r.stdout, /No rows from any source\.\n/);
    assert.equal(r.stdout.split("\n").some((l) => l.startsWith("| UTC")), false);
  }
});

test("render_log_other_branch", () => {
  const dir = tmp();
  const r = render(dir, ["--branch", BRANCH, "--local-gate", "not-applicable", "--log", logFile(dir, "log.jsonl", [2, 5])]);
  assert.equal(r.status, 0);
  assert.match(r.stdout, /event log: no events for this branch\n/);
  assert.match(r.stdout, /Rows: log 0, /);
});

test("render_gate_boundaries", () => {
  const dir = tmp();
  const mk = (rows, branch = BRANCH) =>
    json(dir, "g.jsonl", rows.map(([ts, ev, d]) => JSON.stringify({ v: 1, ts, event: ev, detail: d, branch, head: null })).join("\n") + "\n");
  const start = ["2026-11-13T10:00:00Z", "gate.start", "sandbox"];
  const cases = [
    { rows: [start], status: 3 },
    { rows: [["2026-11-13T10:00:00Z", "gate.pass", "sandbox"]], status: 3 },
    { rows: [["2026-11-13T10:00:00Z", "gate.start", "full"], ["2026-11-13T10:01:00Z", "gate.pass", "full"]], status: 3 },
    { rows: [start, ["2026-11-13T10:01:00Z", "gate.fail", "sandbox"]], status: 0 },
    { rows: [start, ["2026-11-13T10:01:00Z", "gate.stop", "sandbox"]], status: 0 },
    { rows: [start, ["2026-11-13T10:01:00Z", "gate.fail", "sandbox"]], status: 0, since: "2026-11-13T10:00:30Z" },
    { rows: [start, ["2026-11-13T10:01:00Z", "gate.pass", "sandbox"]], status: 3, branch: "claude/999-other" },
  ];
  cases.forEach((c, i) => {
    const args = ["--branch", BRANCH, "--log", mk(c.rows, c.branch)];
    if (c.since) args.push("--since", c.since);
    const r = render(dir, args);
    assert.equal(r.status, c.status, `sub-case ${i + 1}`);
    if (c.status === 3) assert.match(r.stdout, /pre-PR local gate: MISSING, no recorded start and end\n/);
    else assert.match(r.stdout, /pre-PR local gate: recorded\n/);
    if (c.since) {
      assert.match(r.stdout, /\| gate\.fail \| sandbox \| 60 \|\n/);
      assert.equal(r.stdout.includes("| gate.start |"), false);
    }
  });
});

test("render_gate_declared", () => {
  const dir = tmp();
  let r = render(dir, ["--branch", BRANCH, "--local-gate", "earlier-session", "--log", logFile(dir, "a.jsonl", [1])]);
  assert.equal(r.status, 0);
  assert.match(r.stdout, /pre-PR local gate: run in an earlier session \(declared\)\n/);
  r = render(dir, ["--branch", BRANCH, "--local-gate", "not-applicable", "--log", logFile(dir, "b.jsonl", [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11])]);
  assert.equal(r.status, 0);
  assert.match(r.stdout, /pre-PR local gate: not applicable \(declared\)\n/);
});

test("render_bad_inputs", () => {
  const dir = tmp();
  const missingLog = join(dir, "none.jsonl");
  const badLog = json(dir, "bad.jsonl", logLines[0] + "\n" + '{"v":1}\n');
  const badSummary = json(dir, "s2.json", { ...summaryFixture, schema_version: 2 });
  const notArray = json(dir, "obj.json", { a: 1 });
  const L = ["--log", missingLog];
  const cases = [
    { args: ["--branch", "a"], err: USAGE_RENDER },
    { args: [...L], err: USAGE_RENDER },
    { args: ["--branch", "a", ...L, "--frobnicate", "1"], err: USAGE_RENDER },
    { args: ["--branch", "a", "--branch", "b", ...L], err: USAGE_RENDER },
    { args: ["--branch", "a", ...L, "--local-gate", "required", "--local-gate", "required"], err: USAGE_RENDER },
    { args: ["--branch", "a", ...L, "--local-gate", "maybe"], err: "--local-gate must be required, not-applicable or earlier-session.\n" },
    { args: ["--branch", "a", ...L, "--since", "2026-11-13 10:00:00"], err: "--since must be a UTC time like 2026-11-13T10:00:00Z.\n" },
    { args: ["--branch", "a", ...L, "--comments", join(dir, "missing.json")], err: "The file given to --comments is not valid input.\n" },
    { args: ["--branch", "a", ...L, "--comments", notArray], err: "The file given to --comments is not valid input.\n" },
    { args: ["--branch", "a", ...L, "--summary", badSummary], err: "The file given to --summary is not valid input.\n" },
    { args: ["--branch", BRANCH, "--log", badLog], err: "Line 2 of the --log file is not a timeline event.\n" },
    { args: ["--branch", "a", ...L, "--git-range", "nope..nada"], err: "git log failed for --git-range.\n" },
    { args: ["--branch", "a", ...L, "--git-range", "--output=x"], err: USAGE_RENDER },
    { args: ["--branch", "a", ...L, "stray"], err: USAGE_RENDER },
  ];
  for (const c of cases) {
    const r = render(dir, c.args);
    assert.equal(r.status, 2, c.args.join(" "));
    assert.equal(r.stdout, "");
    assert.equal(r.stderr, c.err);
  }
});

test("render_unreadable", { skip: isRoot ? "running as root: mode 000 is still readable" : false }, () => {
  const dir = tmp();
  const log = json(dir, "x.jsonl", "");
  chmodSync(log, 0o000);
  const r = render(dir, ["--branch", "a", "--log", log]);
  assert.equal(r.status, 2);
  assert.equal(r.stderr, "Cannot read the file given to --log.\n");
});

test("render_hook_present", () => {
  const dir = tmp();
  const line = JSON.stringify({ v: 1, ts: "2026-11-13T10:00:00Z", event: "hook.post", detail: "", branch: BRANCH, head: null });
  const r = render(dir, ["--branch", BRANCH, "--local-gate", "not-applicable", "--log", json(dir, "h.jsonl", line + "\n")]);
  assert.equal(r.status, 0);
  assert.match(r.stdout, /hook data: present\n/);
});

test("render_jobs_no_steps", () => {
  const dir = tmp();
  const jobs = '{"total_count":1,"jobs":[{"name":"j","started_at":"2026-11-13T10:00:00Z","completed_at":"2026-11-13T10:01:00Z","conclusion":"success"}]}';
  const r = render(dir, ["--branch", BRANCH, "--log", join(dir, "none.jsonl"), "--local-gate", "not-applicable", "--jobs", json(dir, "jobs.json", jobs)]);
  assert.equal(r.status, 0);
  assert.equal(r.stderr, "");
  assert.match(r.stdout, /Rows: log absent, summary absent, git absent, comment absent, review absent, ci 1\.\n/);
  assert.match(r.stdout, /\| 2026-11-13T10:01:00Z \| ci \| ci\.job \| j success \| 60 \|\n/);
});

// ---------- text checks ----------

const SKILL_SECTION = `## Timeline record

Record this session's events with \`scripts/timeline/event.sh <event> [<detail>]\`, whose header lists each event, the detail it takes and when it applies. Record each event when it happens, not afterwards: \`gate.start\` before, and \`gate.pass\`, \`gate.fail\` or \`gate.stop\` after, each gate command it names; \`focused.run\` for each focused test run; \`review.start\` when cold-review inputs are frozen under docs/agents/pull-requests.md rule 5 and \`review.end\` when the complete report arrives; \`env.interrupted\` and \`agent.resumed\` as they occur; \`decision.received\` for each new decision or approval comment the catch-up or a refresh finds; \`handoff\` just before a handoff or completion comment. A failed \`event.sh\` call does not stop the work; note it in the authored receipt.

Before each edit of the pull request description, fetch the issue's and the pull request's comments, the pull request's reviews and the jobs of each workflow run on its head with \`gh api\`, under AGENTS.md "GitHub issues and pull requests", into files outside the repository. Then run \`scripts/timeline/render.sh\` with the branch, the timeline log, each \`summary.json\` this session produced, the commit range from the merge base with \`origin/main\` to the head, and those files. Pass \`--local-gate not-applicable\` or \`--local-gate earlier-session\`, the value that names the reason, only when docs/agents/pull-requests.md "Verification gates", its last two paragraphs, does not require this session to run its step 3; otherwise leave the default. Put its output under the self-review record's "Timeline record" line, as this session's block, also when it exits 3: that exit means the pre-PR local gate has no recorded start and end, and the block shows it. State any exit 3 in the authored receipt. Each session adds or replaces only its own block and leaves earlier sessions' blocks unchanged. Commit no timeline output (docs/agents/pull-requests.md rule 8).
`;

test("skill_section", () => {
  const text = readFileSync(join(repoRoot, ".agents/skills/implement-plan/SKILL.md"), "utf8");
  const steps = text.indexOf("\n## Steps\n");
  const at = text.indexOf(SKILL_SECTION);
  assert.ok(steps >= 0);
  assert.ok(at > steps);
  assert.equal(text.endsWith(SKILL_SECTION), true);
});

test("template_line", () => {
  const l = readFileSync(join(repoRoot, ".github/pull_request_template.md"), "utf8").split("\n");
  assert.equal(l[14], "- Hosted Codex review and final-head CI (separate from cold review):");
  assert.equal(l[15], "- Timeline record (one block per session from `scripts/timeline/render.sh`; implement-plan \"Timeline record\"):");
});

test("vocabulary_header", () => {
  const text = readFileSync(eventSh, "utf8");
  const vocab = [
    ["gate.start", "sandbox|full|docs"], ["gate.pass", "sandbox|full|docs"], ["gate.fail", "sandbox|full|docs"],
    ["gate.stop", "sandbox|full|docs"], ["focused.run", "fast|project"], ["review.start", "comprehensive|focused|p2-correction"],
    ["review.end", "(comprehensive|focused|p2-correction) P0=[0-9]{1,6} P1=[0-9]{1,6} P2=[0-9]{1,6}"],
    ["env.interrupted", "empty only"], ["agent.resumed", "empty only"], ["handoff", "hari|next-session|completion"],
    ["decision.received", "[0-9]{1,20}"],
  ];
  for (const [name, pattern] of vocab) {
    assert.ok(text.includes(`#   ${name}`) && text.split("\n").some((l) => l.startsWith(`#   ${name}`) && l.endsWith(pattern)), name);
    assert.equal(pattern.includes("\\"), false);
  }
});
