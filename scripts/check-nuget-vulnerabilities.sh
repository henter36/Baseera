#!/usr/bin/env bash
# Fail the job if NuGet vulnerability listing fails OR reports High/Critical.
set -euo pipefail

TARGET="${1:-src/backend/Baseera.slnx}"
TMP="$(mktemp)"
trap 'rm -f "$TMP" "${TMP}.err"' EXIT

set +e
dotnet list "$TARGET" package --vulnerable --include-transitive --format json >"$TMP" 2>"${TMP}.err"
STATUS=$?
set -e

if [[ "$STATUS" -ne 0 ]]; then
  echo "dotnet list package --vulnerable failed with exit code $STATUS" >&2
  cat "${TMP}.err" >&2 || true
  # Tool failure must not look like a clean bill of health.
  exit "$STATUS"
fi

python3 - "$TMP" <<'PY'
import json, sys
path = sys.argv[1]
with open(path, encoding="utf-8") as f:
    data = json.load(f)

findings = []
for project in data.get("projects", []):
    for framework in project.get("frameworks", []):
        for key in ("topLevelPackages", "transitivePackages"):
            for pkg in framework.get(key, []) or []:
                for vuln in pkg.get("vulnerabilities", []) or []:
                    sev = (vuln.get("severity") or "").lower()
                    if sev in ("high", "critical"):
                        findings.append((pkg.get("id"), sev, vuln.get("advisoryurl")))

if findings:
    print("Vulnerable High/Critical packages detected:", file=sys.stderr)
    for item in findings:
        print(f"  {item[0]} severity={item[1]} {item[2]}", file=sys.stderr)
    sys.exit(1)

print("No High/Critical NuGet vulnerabilities reported.")
PY
