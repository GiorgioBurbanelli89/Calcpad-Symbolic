#!/usr/bin/env bash
# Fast parallel check: runs all .cpd examples through Calcpad CLI and
# flags those with parse errors. Outputs CSV: errorCount|path|firstError
set -u
CLI="/c/Users/j-b-j/Documents/Hekatan Calc 1.0.0/Calcpad-Symbolic/Symbolic.Cli/bin/Release/net10.0/Cli.exe"
EX_DIR="/c/Users/j-b-j/Documents/Hekatan Calc 1.0.0/Calcpad-Symbolic/Website/src/editor/examples"
OUT_CSV="/c/Users/j-b-j/Documents/Hekatan Calc 1.0.0/Calcpad-Symbolic/Website/scripts/results.csv"
TMP=$(mktemp -d)
trap 'rm -rf "$TMP"' EXIT

check_one() {
  local cpd="$1"
  local rel="${cpd#$EX_DIR/}"
  local safe=$(echo "$rel" | tr '/ ' '__')
  local out="$TMP/$safe.html"
  "$CLI" "$cpd" "$out -s" >/dev/null 2>&1
  if [ -f "$out" ]; then
    local errs=$(grep -c 'class="err"' "$out" 2>/dev/null || echo 0)
    local first=$(grep -oE 'class="err">[^<]+' "$out" | head -1 | sed 's|class="err">||' | cut -c1-120)
    echo "$errs|$rel|$first"
  else
    echo "-1|$rel|NO_OUTPUT"
  fi
}

export -f check_one
export CLI EX_DIR TMP

find "$EX_DIR" -name "*.cpd" -print0 | xargs -0 -I {} -P 8 bash -c 'check_one "$@"' _ {} | sort > "$OUT_CSV"
echo "Saved $(wc -l < "$OUT_CSV") results to $OUT_CSV"
echo "FAILING ($(awk -F'|' '$1 > 0 || $1 == -1' "$OUT_CSV" | wc -l)):"
awk -F'|' '$1 > 0 || $1 == -1' "$OUT_CSV"
