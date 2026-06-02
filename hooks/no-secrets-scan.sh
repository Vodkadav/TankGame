#!/usr/bin/env bash
# Scans the given files for hardcoded secrets; exits 1 if any match.
# Usage: no-secrets-scan.sh <file>...   (with no args, scans git-staged files)
# Patterns mirror docs/security expectations (see CLAUDE.md sec-no-hardcoded-secrets).
set -euo pipefail

PATTERNS=(
  'password[[:space:]]*=[[:space:]]*"[^"]+"'
  'passwd[[:space:]]*=[[:space:]]*"[^"]+"'
  'api[_-]?key[[:space:]]*=[[:space:]]*"[^"]{16,}"'
  'secret[[:space:]]*=[[:space:]]*"[^"]{8,}"'
  'token[[:space:]]*=[[:space:]]*"[^"]{16,}"'
  'bearer[[:space:]]+[a-zA-Z0-9_-]{20,}'
  'AKIA[0-9A-Z]{16}'
  'ghp_[A-Za-z0-9]{36}'
  'sk-[A-Za-z0-9]{20,}'
)

files=("$@")
if [[ ${#files[@]} -eq 0 ]]; then
  mapfile -t files < <(git diff --cached --name-only --diff-filter=ACM)
fi

status=0
for f in "${files[@]}"; do
  [[ -f "$f" ]] || continue
  case "$f" in
    *.png|*.jpg|*.jpeg|*.gif|*.webp|*.ico|*.pdf|*.zip|*.tar|*.gz|*.translation) continue ;;
  esac
  for pattern in "${PATTERNS[@]}"; do
    if grep -EinI "$pattern" "$f" >/dev/null 2>&1; then
      echo "BLOCKED: possible hardcoded secret in $f" >&2
      echo "  matched pattern: $pattern" >&2
      status=1
      break
    fi
  done
done

if [[ $status -ne 0 ]]; then
  echo "Move secrets to GitHub Actions secrets / 'wrangler secret put' / a gitignored .env." >&2
fi
exit $status
