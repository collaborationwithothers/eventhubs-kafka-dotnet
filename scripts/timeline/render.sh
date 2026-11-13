#!/usr/bin/env bash
# Renders the session timeline block for a pull request description.
# See scripts/timeline/render.mjs for the inputs and the output format.
set -euo pipefail
exec node "$(dirname "${BASH_SOURCE[0]}")/render.mjs" "$@"
