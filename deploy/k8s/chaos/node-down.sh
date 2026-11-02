#!/usr/bin/env bash
# Take a whole node out (several pods fail together), then bring it back.
# Works on kind and on minikube with the docker driver, where every node is a container.
#
#   ./node-down.sh                 # stop the node that runs log-replica-0
#   ./node-down.sh merkle-worker2  # stop that node
#   ./node-down.sh --up merkle-worker2
set -euo pipefail
if [ "${1:-}" = "--up" ]; then
  docker start "$2" > /dev/null
  echo "$(date -u +%FT%T.%3NZ) node $2 started"
  exit 0
fi
node="${1:-$(kubectl -n merkle-log get pod log-replica-0 -o jsonpath='{.spec.nodeName}')}"
docker stop "$node" > /dev/null
echo "$(date -u +%FT%T.%3NZ) node $node stopped (bring it back with: $0 --up $node)"
