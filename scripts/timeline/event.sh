#!/usr/bin/env bash
# Appends one UTC timeline event to a log outside the repository.
#
# Usage: scripts/timeline/event.sh <event> [<detail>]
#
# The log is one JSON object per line, in this key order:
#   {"v":1,"ts":"<UTC>","event":"<event>","detail":"<detail>","branch":<string or null>,"head":<string or null>}
# The log path is PARCHWELL_TIMELINE_LOG when set and non-empty, otherwise
# ${TMPDIR:-/tmp}/parchwell-timeline.jsonl. It must be absolute and outside
# this repository (a lexical check; symlinks and ".." are not resolved).
# scripts/timeline/render.sh reads the log and writes the pull request block.
#
# Exit 0: one line appended. Exit 2: refused, nothing written. Exit 1: the
# append failed. No message echoes the detail or the log path.
#
# Vocabulary. Each event, the detail pattern it takes (matched against the
# whole detail; an absent detail counts as the empty string), and when to use it:
#   gate.start        sandbox|full|docs
#       A gate command starts: ./scripts/verify-dotnet.sh --tier sandbox,
#       ./scripts/verify-dotnet.sh with no options, or npm run check:docs.
#   gate.pass         sandbox|full|docs
#       That command ended with exit 0.
#   gate.fail         sandbox|full|docs
#       It ended with a nonzero exit.
#   gate.stop         sandbox|full|docs
#       It ended without a result: cancelled, timed out or cut off.
#   focused.run       fast|project
#       One focused test run: --tier fast, or --project.
#   review.start      comprehensive|focused|p2-correction
#       Cold-review inputs frozen, by review mode.
#   review.end        (comprehensive|focused|p2-correction) P0=[0-9]{1,6} P1=[0-9]{1,6} P2=[0-9]{1,6}
#       The complete cold-review report arrived, with finding counts by severity.
#   env.interrupted   empty only
#       The session found that the environment cut off a command or restarted.
#   agent.resumed     empty only
#       The session continues after an interruption or a handoff.
#   session.start     empty only
#       The session's first event on its pull request branch, recorded once.
#       render.sh starts the block at the last one in the log for this branch.
#       A resume within the same session records agent.resumed instead.
#   handoff           hari|next-session|completion
#       Just before a handoff or completion comment.
#   decision.received [0-9]{1,20}
#       A new decision or approval comment was found; the detail is its comment ID.

set -u

usage='Usage: scripts/timeline/event.sh <event> [<detail>]'

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  echo "$usage" >&2
  exit 2
fi

event=$1
detail=${2-}

case "$event" in
  gate.start | gate.pass | gate.fail | gate.stop) pattern='^(sandbox|full|docs)$' ;;
  focused.run) pattern='^(fast|project)$' ;;
  review.start) pattern='^(comprehensive|focused|p2-correction)$' ;;
  review.end) pattern='^(comprehensive|focused|p2-correction) P0=[0-9]{1,6} P1=[0-9]{1,6} P2=[0-9]{1,6}$' ;;
  env.interrupted | agent.resumed | session.start) pattern='^$' ;;
  handoff) pattern='^(hari|next-session|completion)$' ;;
  decision.received) pattern='^[0-9]{1,20}$' ;;
  *)
    if [[ $event =~ ^[a-z.]{1,40}$ ]]; then
      echo "Unknown event: $event" >&2
    else
      echo 'Unknown event.' >&2
    fi
    exit 2
    ;;
esac

if ! [[ $detail =~ $pattern ]]; then
  echo "Invalid detail for $event." >&2
  exit 2
fi

log=${PARCHWELL_TIMELINE_LOG:-}
if [ -z "$log" ]; then
  log="${TMPDIR:-/tmp}/parchwell-timeline.jsonl"
fi

if [[ $log != /* ]]; then
  echo 'The timeline log path must be absolute.' >&2
  exit 2
fi

root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -L)
if [ "$log" = "$root" ] || [[ $log == "$root"/* ]]; then
  echo 'The timeline log must be outside the repository.' >&2
  exit 2
fi

branch=$(git -C "$root" rev-parse --abbrev-ref HEAD 2>/dev/null) || branch=
if [ "$branch" = HEAD ] || ! [[ $branch =~ ^[A-Za-z0-9._/-]{1,200}$ ]]; then
  branch_json=null
else
  branch_json="\"$branch\""
fi

head=$(git -C "$root" rev-parse HEAD 2>/dev/null) || head=
if [[ $head =~ ^[0-9a-f]{40}$ ]]; then
  head_json="\"$head\""
else
  head_json=null
fi

ts=$(date -u +%Y-%m-%dT%H:%M:%SZ)
line="{\"v\":1,\"ts\":\"$ts\",\"event\":\"$event\",\"detail\":\"$detail\",\"branch\":$branch_json,\"head\":$head_json}"

if ! { printf '%s\n' "$line" >>"$log"; } 2>/dev/null; then
  echo 'The timeline event could not be written.' >&2
  exit 1
fi
