#!/usr/bin/env bash
# Runs once, when the coordinator's data directory is first initialised
# (after the image's own script has created the citus extension).
# Registers the coordinator and both workers so distributed tables get sharded across them.
set -euo pipefail

for worker in ${CITUS_WORKERS}; do
  until pg_isready -h "$worker" -p 5432 -U "$POSTGRES_USER" -d "$POSTGRES_DB" > /dev/null 2>&1; do
    echo "waiting for citus worker $worker..."
    sleep 1
  done
done

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-SQL
  SELECT citus_set_coordinator_host('${CITUS_COORDINATOR_HOST}', 5432);
SQL

for worker in ${CITUS_WORKERS}; do
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    -c "SELECT citus_add_node('$worker', 5432);"
done

psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" -c "SELECT nodename, nodeport, shouldhaveshards FROM pg_dist_node;"
