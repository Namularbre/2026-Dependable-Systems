#!/usr/bin/env bash
# Simulate an attacker editing a replica's log on disk, behind the server's back.
# Flips one byte at OFFSET in FILE (relative to the replica's data directory, /data).
# Format-agnostic: it works whatever the replica's storage layout is, as long as the image
# has sh and dd (busybox, alpine, debian...). The auditor should then flag that replica.
#
#   ./tamper.sh log-replica-1 log.jsonl 100
#   ./tamper.sh log-replica-1               # largest file under /data, offset 100
set -euo pipefail
pod="${1:-log-replica-1}"
file="${2:-}"
offset="${3:-100}"

kubectl -n merkle-log exec "$pod" -- sh -c '
  set -e
  f="$1"; off="$2"
  if [ -z "$f" ]; then f=$(ls -S $(find /data -type f) | head -n 1); else f="/data/$f"; fi
  b=$(dd if="$f" bs=1 skip="$off" count=1 2>/dev/null)
  if [ "$b" = "X" ]; then n="Y"; else n="X"; fi
  printf %s "$n" | dd of="$f" bs=1 seek="$off" count=1 conv=notrunc 2>/dev/null
  echo "tampered $f at byte $off: \"$b\" -> \"$n\""
' sh "$file" "$offset"
echo "$(date -u +%FT%T.%3NZ) tampered $pod"
