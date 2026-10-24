#!/bin/bash
set -euo pipefail
: "${PARCHWELL_TOOLS_DIRECTORY:?set the pinned tools installation directory}"
export PATH="$PARCHWELL_TOOLS_DIRECTORY:$PATH"
python="$PARCHWELL_TOOLS_DIRECTORY/venv/bin/python"
"$python" -c 'import yaml; assert yaml.__version__ == "6.0.3"'
test "$(helm version --template '{{.Version}}')" = v4.3.1
kubeconform -v 2>&1 | grep -Fq 0.8.0
root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
cd "$root"
exec "$python" charts/parchwell/tests/test_chart.py
