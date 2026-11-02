# Kubernetes deployment

The Kubernetes version of `compose.yaml`, plus the Merkle logging layer and the tools to
break it on purpose (crash, partition, tampering, node loss).

```
deploy/k8s/
  kustomization.yaml          whole stack:  kubectl apply -k deploy/k8s
  myshop/                     namespace myshop: Citus (1 coordinator + 2 workers), migrator Job, web x3, ingress
  merkle-log/                 namespace merkle-log: 3 log replicas, 1 auditor, 3 synthetic clients
  components/partition/       NetworkPolicy that simulates a network partition (used by both namespaces)
  chaos/                      failure-injection scripts (bash)
```

## What runs

```
 namespace myshop                          namespace merkle-log
 ─────────────────                         ──────────────────────────────────────────────
 Ingress ─► svc/web ─► web x3  ── logs ──► log-replica-0 ◄──┐
                         │      (future)   log-replica-1 ◄──┼── anti-entropy (Merkle roots)
                         ▼                 log-replica-2 ◄──┘
               citus-coordinator               ▲   ▲ STH + proofs
                 │          │                  │   └──────── auditor-0 (independent, own volume)
          citus-worker-0  citus-worker-1       │
                                    log-client-0/1/2 (C1..C3, synthetic load)
```

| Component | Kind | Why this kind |
|---|---|---|
| `log-replica-0..2` | StatefulSet + headless service | Stable names (`log-replica-2.log-replica`) and one volume per replica: a crashed replica comes back with its own log and its own tree |
| `log` | Service (ClusterIP) | Shortcut to any Ready replica. Clients that must survive partitions iterate over `LOG_REPLICAS` instead |
| `auditor-0` | StatefulSet, 1 replica, own volume | Remembers past signed tree heads across restarts, otherwise a rewritten log would look new to it |
| `log-client-0..2` | StatefulSet | Stable names so "C3" is always `log-client-2` in the scenarios |
| `merkle-config` | ConfigMap | The replica list and intervals, read by every component |
| `log-replica` PDB | minAvailable 2 | A node drain never leaves the log with a single copy |

Replicas spread over nodes (`podAntiAffinity`, preferred), and the auditor prefers nodes
without replicas, so one node failure does not take out both the log and its validator.

## Contract for the log images

The manifests reference three images that the project still has to build:
`merkle-log-replica`, `merkle-auditor` and `merkle-log-client` (tag `dev`, see the `images:`
section of `merkle-log/kustomization.yaml`). Whatever language they are written in, they
only need to follow this contract:

| Image | Port | Must serve | Gets in its environment |
|---|---|---|---|
| `merkle-log-replica` | 7000 | `GET /health` (200 while the process runs, even when cut from its peers) | `REPLICA_ID` (pod name), `LOG_REPLICAS`, `ANTI_ENTROPY_INTERVAL`, `DATA_DIR=/data` (persistent) |
| `merkle-auditor` | 7100 | `GET /health` | `LOG_REPLICAS`, `AUDIT_INTERVAL`, `DATA_DIR=/data` (persistent) |
| `merkle-log-client` | 7200 | `GET /health` | `CLIENT_ID` (pod name), `LOG_REPLICAS`, `ENTRIES_PER_SECOND` |

`LOG_REPLICAS` is a comma-separated list of URLs, for example
`http://log-replica-0.log-replica.merkle-log.svc.cluster.local:7000`. A replica finds its
peers by removing its own entry. The replica image should contain `sh` and `dd` (any
alpine, debian or busybox base) so that `chaos/tamper.sh` can edit its files.

The web pods already receive the same list in `MerkleLog__Replicas`; the app does not read
it yet.

## Local cluster

NetworkPolicy must be enforced by the cluster for the partition scenarios to work.

**minikube** (3 nodes, Calico enforces NetworkPolicy):

```bash
minikube start --nodes 3 --cni calico
minikube image build -t myshop-web:dev -f src/MyShop.Web/Dockerfile .
minikube image load merkle-log-replica:dev merkle-auditor:dev merkle-log-client:dev
kubectl apply -k deploy/k8s
```

**kind** (>= 0.24, its default CNI enforces NetworkPolicy):

```bash
kind create cluster --name merkle --config - <<'EOF'
kind: Cluster
apiVersion: kind.x-k8s.io/v1alpha4
nodes: [{role: control-plane}, {role: worker}, {role: worker}]
EOF
docker build -t myshop-web:dev -f src/MyShop.Web/Dockerfile .
kind load docker-image --name merkle myshop-web:dev merkle-log-replica:dev merkle-auditor:dev merkle-log-client:dev
kubectl apply -k deploy/k8s
```

Then:

```bash
kubectl get pods -A -l 'app.kubernetes.io/part-of in (myshop,merkle-log)' -o wide
kubectl -n myshop port-forward svc/web 8080:80          # shop on http://localhost:8080
kubectl -n merkle-log port-forward svc/auditor 7100      # auditor on http://localhost:7100
```

Only one layer? `kubectl apply -k deploy/k8s/myshop` or `kubectl apply -k deploy/k8s/merkle-log`.

To start over with empty logs: `kubectl delete -k deploy/k8s` then
`kubectl -n merkle-log delete pvc --all` (volumes outlive the StatefulSets on purpose).

## Failure scenarios

The scripts in `chaos/` need bash and `kubectl` (Git Bash or WSL on Windows). Each one
prints a UTC timestamp, so you can line up the injected failure with the logs and the
auditor's reports when you measure.

| Scenario | Command | What should happen |
|---|---|---|
| Replica crash | `chaos/crash.sh log-replica-0` | The pod is killed without a graceful stop and recreated with the same name and volume. Clients fall back to another replica meanwhile; it catches up through anti-entropy |
| Network partition | `chaos/partition.sh` (R3 + C3 by default) | `log-replica-2` and `log-client-2` can only reach each other. Both sides keep accepting writes, and their Merkle roots diverge |
| Heal | `chaos/heal.sh` | Replicas see each other again and reconcile by comparing roots |
| Tampering | `chaos/tamper.sh log-replica-1` | One byte of the replica's largest data file changes on disk. The auditor must flag that replica |
| Node loss | `chaos/node-down.sh` then `chaos/node-down.sh --up <node>` | The node running `log-replica-0` (kind/minikube docker driver) stops; Kubernetes marks it NotReady after ~40 s |
| Auditor crash | `chaos/crash.sh auditor-0` | It restarts with its STH history intact |

How the partition works: `components/partition/networkpolicy.yaml` is always installed but
selects no pod until one is labelled `merkle-partition=b`. Labelled pods only reach other
labelled pods (in any namespace), plus DNS and the Citus coordinator. Anything can be put on
side B, including a shop pod: `chaos/partition.sh log-replica-2 myshop/<web-pod-name>`.
The same thing without the scripts:

```bash
kubectl -n merkle-log label pod log-replica-2 log-client-2 merkle-partition=b   # cut
kubectl -n merkle-log label pod --all merkle-partition-                          # heal
```

A pod recreated during a partition comes back without the label, so it rejoins side A.

To repeat a scenario for the measurements (e.g. 30 runs of a 60 s partition):

```bash
for run in $(seq 1 30); do
  deploy/k8s/chaos/partition.sh
  sleep 60
  deploy/k8s/chaos/heal.sh
  sleep 30   # let the replicas reconcile before the next run
done 2>&1 | tee partition-60s.log
```

## Status and known limits

- The manifests render with `kubectl kustomize` (kustomize v5.5) but have not been run on a
  live cluster yet, and the three log images do not exist yet. Test them on minikube first.
- Liveness/readiness probes come from the node's kubelet, not from a pod. Calico lets that
  traffic through to a partitioned pod; if another CNI drops it, the partitioned replica
  turns NotReady and is restarted after 3 failed liveness checks (which also removes its
  partition label). If you see that, switch the probes to `exec` probes.
- Existing TCP connections may survive the start of a partition for a few seconds on some
  CNIs. Clients that open a new connection per request see the cut immediately.
