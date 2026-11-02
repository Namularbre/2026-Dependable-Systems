#!/usr/bin/env bash
# Crash a pod abruptly (no graceful shutdown), like a power cut on that process.
# The StatefulSet recreates it with the same name and the same volume.
#
#   ./crash.sh                 # default: the first replica (the leader in a primary/backup setup)
#   ./crash.sh log-replica-1
#   ./crash.sh auditor-0
set -euo pipefail
pod="${1:-log-replica-0}"
kubectl -n merkle-log delete pod "$pod" --grace-period=0 --force --wait=false
echo "$(date -u +%FT%T.%3NZ) crashed $pod"
