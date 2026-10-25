#!/bin/bash
set -euo pipefail
# shellcheck source=infra/scripts/lifecycle/common.sh
source "$(dirname "${BASH_SOURCE[0]}")/../lifecycle/common.sh"
exec python3 "$PARCHWELL_REPOSITORY/infra/scripts/phase/phase.py" "$@"
