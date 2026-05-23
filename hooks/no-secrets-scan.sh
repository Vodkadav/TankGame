#!/usr/bin/env bash
# TankGame pre-commit hook: scans staged additions/modifications for likely
# hardcoded secrets. Exits non-zero on the first match so the commit is
# blocked. Patterns mirror C:/programming/Claude/rules/sec-no-hardcoded-secrets.md
# and scripts/hooks/no-secrets-scan.ps1 — keep the two in sync.

set -u

# Each entry: "<label>|<extended-regex>". grep -iE makes matching case-insensitive.
patterns=(
  'password=|password[[:space:]]*=[[:space:]]*"[^"]+"'
  'token=|token[[:space:]]*=[[:space:]]*"[^"]{16,}"'
  'secret=|secret[[:space:]]*=[[:space:]]*"[^"]{8,}"'
  'bearer-token|Bearer[[:space:]]+[A-Za-z0-9_.=-]{20,}'
  'aws-access-key|AKIA[0-9A-Z]{16}'
  'github-token|gh[pousr]_[A-Za-z0-9]{30,}'
  'base64-credential|(secret|token|password|key)[^[:alnum:]][[:space:]]*[:=][[:space:]]*"?[A-Za-z0-9+/]{40,}={0,2}"?'
)

staged="$(git diff --cached --name-only --diff-filter=ACM)"
if [ -z "$staged" ]; then
  exit 0
fi

rc=0
while IFS= read -r file; do
  [ -z "$file" ] && continue
  [ ! -f "$file" ] && continue
  for entry in "${patterns[@]}"; do
    label="${entry%%|*}"
    regex="${entry#*|}"
    if grep -iE -- "$regex" "$file" >/dev/null 2>&1; then
      echo "blocked: $file contains likely secret <$label>" >&2
      rc=1
      break
    fi
  done
  [ "$rc" -ne 0 ] && break
done <<EOF
$staged
EOF

exit "$rc"
