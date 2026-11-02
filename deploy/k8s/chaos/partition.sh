#!/usr/bin/env bash
# Put pods on side B of a simulated network partition (see components/partition).
# Side B pods only reach each other; everything else stays on side A.
#
#   ./partition.sh                         # default scenario: isolate R3 with its client C3
#   ./partition.sh log-replica-2 log-client-2
#   ./partition.sh myshop/web-7f9c-abcde   # namespace/pod for pods outside merkle-log
set -euo pipefail

pods=("$@")
[ ${#pods[@]} -eq 0 ] && pods=(log-replica-2 log-client-2)

for p in "${pods[@]}"; do
  ns=merkle-log
  if [[ "$p" == */* ]]; then ns="${p%%/*}"; p="${p#*/}"; fi
  kubectl -n "$ns" label pod "$p" merkle-partition=b --overwrite
done

echo "$(date -u +%FT%T.%3NZ) partition started: ${pods[*]}"
