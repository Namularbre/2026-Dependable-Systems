#!/usr/bin/env bash
# End every simulated partition: remove the label from all pods of both namespaces.
set -euo pipefail
for ns in merkle-log myshop; do
  kubectl -n "$ns" label pod --all merkle-partition- > /dev/null
done
echo "$(date -u +%FT%T.%3NZ) partition healed"
