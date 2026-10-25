#!/bin/bash
set -euo pipefail
: "${PARCHWELL_TOOLS_DIRECTORY:?set the pinned tools installation directory}"
export PATH="$PARCHWELL_TOOLS_DIRECTORY:$PATH"
python="$PARCHWELL_TOOLS_DIRECTORY/venv/bin/python"
"$python" -c 'import yaml; assert yaml.__version__ == "6.0.3"'
test "$(helm version --template '{{.Version}}')" = v4.3.1
test "$(dotnet --version)" = 10.0.402
root=$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)
cd "$root"
exec "$python" charts/parchwell/tests/test_loadgen_host.py
