#!/usr/bin/env bash
# Proves the NuGet vulnerability gate fails closed when the underlying tool errors.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
FAKE_BIN="$(mktemp -d)"
trap 'rm -rf "$FAKE_BIN"' EXIT

cat >"$FAKE_BIN/dotnet" <<'EOF'
#!/usr/bin/env bash
echo "simulated tool failure" >&2
exit 42
EOF
chmod +x "$FAKE_BIN/dotnet"

export PATH="$FAKE_BIN:$PATH"
set +e
"$ROOT/scripts/check-nuget-vulnerabilities.sh" src/backend/Baseera.slnx
STATUS=$?
set -e

if [[ "$STATUS" -eq 0 ]]; then
  echo "EXPECTED FAILURE: vulnerability gate returned success on tool error" >&2
  exit 1
fi

if [[ "$STATUS" -ne 42 ]]; then
  echo "Expected exit 42 from simulated tool failure, got $STATUS" >&2
  exit 1
fi

echo "NuGet vulnerability gate correctly failed closed on tool error."
