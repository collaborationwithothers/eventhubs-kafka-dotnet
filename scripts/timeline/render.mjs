// Renders the session timeline block for a pull request description.
//
// Run through scripts/timeline/render.sh. Inputs: the event log written by
// scripts/timeline/event.sh, summary.json files of scripts/verify-dotnet.sh,
// a git range, and GitHub REST responses (comments, reviews, jobs) that the
// caller fetched. Seconds come only from recorded start and end pairs of the
// same source row set, never from a gap between unrelated rows.
//
// Exit 0: block written. Exit 3: block written, and the pre-PR local gate has
// no recorded start and end. Exit 2: bad input, one line on standard error.

import { readFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import process from "node:process";

const USAGE =
  "Usage: scripts/timeline/render.sh --branch <name> --log <file> [--local-gate required|not-applicable|earlier-session] [--summary <file>]... [--git-range <base>..<head>] [--comments <file>]... [--reviews <file>]... [--jobs <file>]... [--since <UTC>]";
const GATE_MISSING_MESSAGE = "The pre-PR local gate has no recorded start and end.";
const UTC_FORM = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})Z$/;
const GITHUB_TIME = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d+)?(Z|[+-]\d{2}:\d{2})$/;
const SOURCE_ORDER = ["log", "summary", "git", "comment", "review", "ci"];
const SINGLE_FLAGS = ["branch", "log", "local-gate", "git-range", "since"];
const REPEAT_FLAGS = ["summary", "comments", "reviews", "jobs"];

class InputError extends Error {}

function fail(message) {
  throw new InputError(message);
}

function validDate(y, mo, d, h, mi, s) {
  if (mo < 1 || mo > 12 || h > 23 || mi > 59 || s > 59 || d < 1) return false;
  return d <= new Date(Date.UTC(y, mo, 0)).getUTCDate();
}

// Returns epoch seconds, or null when the text is not a valid UTC-form time.
function parseUtc(text) {
  if (typeof text !== "string") return null;
  const m = UTC_FORM.exec(text);
  if (!m) return null;
  const [y, mo, d, h, mi, s] = m.slice(1).map(Number);
  if (!validDate(y, mo, d, h, mi, s)) return null;
  return Date.UTC(y, mo - 1, d, h, mi, s) / 1000;
}

// GitHub times may carry fractions or an offset. Fractions are truncated.
function parseGithub(text) {
  if (typeof text !== "string") return null;
  const m = GITHUB_TIME.exec(text);
  if (!m) return null;
  const [y, mo, d, h, mi, s] = m.slice(1, 7).map(Number);
  if (!validDate(y, mo, d, h, mi, s)) return null;
  let seconds = Date.UTC(y, mo - 1, d, h, mi, s) / 1000;
  const zone = m[7];
  if (zone !== "Z") {
    const sign = zone[0] === "-" ? -1 : 1;
    const oh = Number(zone.slice(1, 3));
    const om = Number(zone.slice(4, 6));
    if (oh > 23 || om > 59) return null;
    seconds -= sign * (oh * 3600 + om * 60);
  }
  return seconds;
}

function formatUtc(seconds) {
  return new Date(seconds * 1000).toISOString().slice(0, 19) + "Z";
}

function parseArgs(argv) {
  const single = {};
  const repeated = { summary: [], comments: [], reviews: [], jobs: [] };
  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (!arg.startsWith("--")) fail(USAGE);
    const name = arg.slice(2);
    const isSingle = SINGLE_FLAGS.includes(name);
    if (!isSingle && !REPEAT_FLAGS.includes(name)) fail(USAGE);
    if (i + 1 >= argv.length) fail(USAGE);
    const value = argv[i + 1];
    i += 1;
    if (isSingle) {
      if (name in single) fail(USAGE);
      single[name] = value;
    } else {
      repeated[name].push(value);
    }
  }
  if (!("branch" in single) || !("log" in single)) fail(USAGE);
  // A range that starts with "-" would reach git as an option.
  if ("git-range" in single && single["git-range"].startsWith("-")) fail(USAGE);
  return { single, repeated };
}

// Returns file text, or null when the file does not exist.
function readText(path, flag) {
  try {
    return readFileSync(path, "utf8");
  } catch (error) {
    if (error && error.code === "ENOENT") return null;
    return fail(`Cannot read the file given to --${flag}.`);
  }
}

function readJson(path, flag) {
  const invalid = `The file given to --${flag} is not valid input.`;
  const text = readText(path, flag);
  if (text === null) return fail(invalid);
  try {
    return JSON.parse(text);
  } catch {
    return fail(invalid);
  }
}

function isObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function isString(value) {
  return typeof value === "string";
}

function isNullable(value) {
  return value === undefined || value === null;
}

function readLog(path, branch) {
  const text = readText(path, "log");
  if (text === null) return { state: "missing", rows: [] };
  const lines = text.split("\n");
  if (lines[lines.length - 1] === "") lines.pop();
  let nonEmpty = 0;
  let onBranch = 0;
  const entries = [];
  lines.forEach((line, index) => {
    if (line.trim() === "") return;
    nonEmpty += 1;
    const notEvent = `Line ${index + 1} of the --log file is not a timeline event.`;
    let value;
    try {
      value = JSON.parse(line);
    } catch {
      fail(notEvent);
    }
    const ts = isObject(value) ? parseUtc(value.ts) : null;
    const ok =
      isObject(value) &&
      value.v === 1 &&
      ts !== null &&
      isString(value.event) &&
      isString(value.detail) &&
      (value.branch === null || isString(value.branch)) &&
      (value.head === null || isString(value.head));
    if (!ok) fail(notEvent);
    if (value.branch === branch) {
      onBranch += 1;
      entries.push({ time: ts, event: value.event, detail: value.detail, line: index });
    }
  });
  let state = "present";
  if (nonEmpty === 0) state = "empty";
  else if (onBranch === 0) state = "no events for this branch";
  return { state, rows: pairLog(entries) };
}

// Pairs end events with the latest earlier unpaired start, in time order.
function pairLog(entries) {
  const ordered = entries
    .map((entry, order) => ({ ...entry, order }))
    .sort((a, b) => a.time - b.time || a.order - b.order);
  const open = new Map();
  return ordered.map((entry) => {
    const row = { time: entry.time, source: "log", event: entry.event, detail: entry.detail, seconds: null };
    let key = null;
    let isStart = false;
    if (entry.event === "gate.start") {
      key = `gate:${entry.detail}`;
      isStart = true;
    } else if (["gate.pass", "gate.fail", "gate.stop"].includes(entry.event)) {
      key = `gate:${entry.detail}`;
    } else if (entry.event === "review.start") {
      key = `review:${entry.detail}`;
      isStart = true;
    } else if (entry.event === "review.end") {
      key = `review:${entry.detail.split(" ")[0]}`;
    }
    if (key !== null) {
      if (!open.has(key)) open.set(key, []);
      const stack = open.get(key);
      if (isStart) {
        stack.push(entry.time);
      } else if (stack.length > 0) {
        row.seconds = entry.time - stack.pop();
      }
    }
    return row;
  });
}

function summaryRows(value, flag) {
  const invalid = `The file given to --${flag} is not valid input.`;
  if (!isObject(value) || value.schema_version !== 1) fail(invalid);
  const started = parseUtc(value.started_utc);
  const ended = parseUtc(value.ended_utc);
  if (started === null || ended === null || ended < started) fail(invalid);
  if (!isString(value.tier) || !isString(value.outcome) || !Number.isInteger(value.exit_code)) fail(invalid);
  if (!Array.isArray(value.stages)) fail(invalid);
  const rows = [];
  for (const stage of value.stages) {
    if (!isObject(stage) || !isString(stage.label) || !Number.isInteger(stage.exit_code)) fail(invalid);
    const start = parseUtc(stage.start_utc);
    const end = parseUtc(stage.end_utc);
    if (start === null || end === null || end < start) fail(invalid);
    rows.push({
      time: end,
      source: "summary",
      event: "verify.stage",
      detail: `${stage.label} exit=${stage.exit_code}`,
      seconds: end - start,
    });
  }
  rows.push({
    time: ended,
    source: "summary",
    event: "verify.run",
    detail: `tier=${value.tier} outcome=${value.outcome} exit=${value.exit_code}`,
    seconds: ended - started,
  });
  return rows;
}

function commentRows(value, flag) {
  const invalid = `The file given to --${flag} is not valid input.`;
  if (!Array.isArray(value)) fail(invalid);
  return value.map((item) => {
    if (!isObject(item) || !(Number.isInteger(item.id) || /^\d+$/.test(item.id ?? ""))) fail(invalid);
    const time = parseGithub(item.created_at);
    if (time === null) fail(invalid);
    return { time, source: "comment", event: "comment", detail: String(item.id), seconds: null };
  });
}

function reviewRows(value, flag) {
  const invalid = `The file given to --${flag} is not valid input.`;
  if (!Array.isArray(value)) fail(invalid);
  const rows = [];
  for (const item of value) {
    if (!isObject(item) || !isString(item.state)) fail(invalid);
    if (!(item.user === null || isObject(item.user))) fail(invalid);
    if (isObject(item.user) && !isString(item.user.login)) fail(invalid);
    if (!(item.commit_id === null || isString(item.commit_id))) fail(invalid);
    if (isNullable(item.submitted_at)) continue;
    const time = parseGithub(item.submitted_at);
    if (time === null) fail(invalid);
    const login = item.user === null ? "unknown" : item.user.login;
    const at = item.commit_id === null ? "" : ` at ${item.commit_id.slice(0, 12)}`;
    rows.push({ time, source: "review", event: "review", detail: `${item.state} by ${login}${at}`, seconds: null });
  }
  return rows;
}

// A step or job ends at completed_at when present; otherwise it stays at started_at.
function timedRow(item, event, detail, invalid) {
  const started = parseGithub(item.started_at);
  if (started === null) fail(invalid);
  if (isNullable(item.completed_at)) {
    return { time: started, source: "ci", event, detail, seconds: null };
  }
  const completed = parseGithub(item.completed_at);
  if (completed === null || completed < started) fail(invalid);
  return { time: completed, source: "ci", event, detail, seconds: completed - started };
}

function jobRows(value, flag) {
  const invalid = `The file given to --${flag} is not valid input.`;
  if (!isObject(value) || !Array.isArray(value.jobs)) fail(invalid);
  const rows = [];
  for (const job of value.jobs) {
    if (!isObject(job) || !isString(job.name)) fail(invalid);
    if (!(isNullable(job.conclusion) || isString(job.conclusion))) fail(invalid);
    if (isNullable(job.started_at)) continue;
    const steps = job.steps === undefined || job.steps === null ? [] : job.steps;
    if (!Array.isArray(steps)) fail(invalid);
    const ordered = steps.map((step) => {
      if (!isObject(step) || !isString(step.name) || !Number.isInteger(step.number)) fail(invalid);
      if (!(isNullable(step.conclusion) || isString(step.conclusion))) fail(invalid);
      return step;
    });
    ordered.sort((a, b) => a.number - b.number);
    for (const step of ordered) {
      if (isNullable(step.started_at)) continue;
      const conclusion = step.conclusion ?? "pending";
      rows.push(timedRow(step, "ci.step", `${job.name} / ${step.name} ${conclusion}`, invalid));
    }
    rows.push(timedRow(job, "ci.job", `${job.name} ${job.conclusion ?? "pending"}`, invalid));
  }
  return rows;
}

function gitRows(range) {
  const failure = "git log failed for --git-range.";
  let output;
  try {
    output = execFileSync("git", ["log", "--format=%H%x09%ct", "--end-of-options", range], {
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    });
  } catch {
    return fail(failure);
  }
  const rows = [];
  for (const line of output.split("\n")) {
    if (line === "") continue;
    const m = /^([0-9a-f]{40,64})\t(\d+)$/.exec(line);
    if (!m) fail(failure);
    rows.push({ time: Number(m[2]), source: "git", event: "commit", detail: m[1].slice(0, 12), seconds: null });
  }
  return rows;
}

function escapeDetail(text) {
  return text.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll("|", "\\|");
}

function render() {
  const { single, repeated } = parseArgs(process.argv.slice(2));
  const gateMode = single["local-gate"] ?? "required";
  if (!["required", "not-applicable", "earlier-session"].includes(gateMode)) {
    fail("--local-gate must be required, not-applicable or earlier-session.");
  }
  let since = null;
  if ("since" in single) {
    since = parseUtc(single.since);
    if (since === null) fail("--since must be a UTC time like 2026-11-13T10:00:00Z.");
  }

  const log = readLog(single.log, single.branch);
  const groups = [{ source: "log", present: log.state !== "missing", rows: log.rows }];
  groups.push({ source: "summary", present: repeated.summary.length > 0, rows: repeated.summary.flatMap((f) => summaryRows(readJson(f, "summary"), "summary")) });
  const comments = repeated.comments.flatMap((f) => commentRows(readJson(f, "comments"), "comments"));
  const reviews = repeated.reviews.flatMap((f) => reviewRows(readJson(f, "reviews"), "reviews"));
  const jobs = repeated.jobs.flatMap((f) => jobRows(readJson(f, "jobs"), "jobs"));
  groups.push({ source: "git", present: "git-range" in single, rows: "git-range" in single ? gitRows(single["git-range"]) : [] });
  groups.push({ source: "comment", present: repeated.comments.length > 0, rows: comments });
  groups.push({ source: "review", present: repeated.reviews.length > 0, rows: reviews });
  groups.push({ source: "ci", present: repeated.jobs.length > 0, rows: jobs });

  if (since === null && log.rows.length > 0) since = Math.min(...log.rows.map((row) => row.time));

  let index = 0;
  const table = [];
  const counts = {};
  for (const group of groups) {
    const kept = group.rows.filter((row) => since === null || row.time >= since);
    counts[group.source] = group.present ? String(kept.length) : "absent";
    for (const row of kept) {
      table.push({ ...row, index });
      index += 1;
    }
  }
  table.sort((a, b) => a.time - b.time || SOURCE_ORDER.indexOf(a.source) - SOURCE_ORDER.indexOf(b.source) || a.index - b.index);

  const hookData = table.some((row) => row.source === "log" && row.event.startsWith("hook.")) ? "present" : "absent";
  const recorded = table.some(
    (row) =>
      row.source === "log" &&
      ["gate.pass", "gate.fail", "gate.stop"].includes(row.event) &&
      row.detail === "sandbox" &&
      row.seconds !== null,
  );
  let gateLine;
  if (gateMode === "not-applicable") gateLine = "not applicable (declared)";
  else if (gateMode === "earlier-session") gateLine = "run in an earlier session (declared)";
  else gateLine = recorded ? "recorded" : "MISSING, no recorded start and end";

  const out = [];
  if (table.length === 0) out.push("<details><summary>Timeline: no rows</summary>");
  else out.push(`<details><summary>Timeline ${formatUtc(table[0].time)} to ${formatUtc(table[table.length - 1].time)} (UTC)</summary>`);
  out.push("");
  out.push(`hook data: ${hookData}`);
  out.push(`event log: ${log.state}`);
  out.push(`pre-PR local gate: ${gateLine}`);
  out.push(`Rows: ${SOURCE_ORDER.map((source) => `${source} ${counts[source]}`).join(", ")}.`);
  out.push("Seconds are computed only from recorded start and end pairs.");
  out.push("");
  if (table.length === 0) {
    out.push("No rows from any source.");
  } else {
    out.push("| UTC | Source | Event | Detail | Seconds |");
    out.push("|---|---|---|---|---|");
    for (const row of table) {
      const seconds = row.seconds === null ? "" : String(row.seconds);
      out.push(`| ${formatUtc(row.time)} | ${row.source} | ${row.event} | ${escapeDetail(row.detail)} | ${seconds} |`);
    }
  }
  out.push("");
  out.push("</details>");
  process.stdout.write(out.join("\n") + "\n");

  if (gateMode === "required" && !recorded) {
    process.stderr.write(GATE_MISSING_MESSAGE + "\n");
    return 3;
  }
  return 0;
}

try {
  process.exitCode = render();
} catch (error) {
  if (!(error instanceof InputError)) throw error;
  process.stderr.write(error.message + "\n");
  process.exitCode = 2;
}
