# MyShop

A demo shop built with ASP.NET Core (.NET 10). It has:

- a **storefront** where buyers browse the catalog, fill a cart and place orders;
- a **backoffice** where admins create items, refill stock, put items on sale or take them off, and delete them.

The design follows these rules:

- **Hexagonal architecture**, with one project per layer.
- **Modern, functional C#**: records, primary constructors, `Result<T>` instead of exceptions, and pure domain functions.
- **Stateless** web servers.
- **PostgreSQL sharded with Citus**, accessed through EF Core migrations.
- Every entity has `CreatedAt` and `UpdatedAt`, and is deleted **logically** (`IsDeleted`), never physically.

---

## Quick start (full stack in Docker)

Requirement: Docker Desktop.

```bash
docker compose up --build
```

Then open **http://localhost:8080**.

| Account | Credentials |
|---|---|
| Admin (backoffice) | `admin@myshop.local` / `Admin123!` |
| Buyer | Register through the **Register** link (no email confirmation) |

To stop the stack:

```bash
docker compose down        # stop the stack, keep the data
docker compose down -v     # stop the stack and wipe the databases (fresh start next time)
```

### What starts

```
                 http://localhost:8080
                          │
                    ┌─────┴─────┐
                    │  lb (nginx)│   round-robin, no sticky sessions
                    └─────┬─────┘
          ┌───────────────┼───────────────┐
       ┌──┴──┐         ┌──┴──┐         ┌──┴──┐
       │web1 │         │web2 │         │web3 │   3 identical, stateless replicas
       └──┬──┘         └──┬──┘         └──┬──┘
          └───────────────┼───────────────┘
                 ┌────────┴────────┐
                 │citus-coordinator│ ◄── migrator (one-shot: migrations + seed)
                 └───┬─────────┬───┘
             ┌───────┴──┐   ┌──┴───────┐
             │ worker1  │   │ worker2  │   hold the shards
             └──────────┘   └──────────┘
```

Startup order (enforced by `depends_on` in `compose.yaml`):

1. The two **Citus workers** start.
2. The **coordinator** starts. On its first init, `deploy/citus/init-coordinator.sh` registers both workers.
3. The **migrator** runs the web image in one-shot mode (`Database__MigrateAndExit=true`, or `--migrate` on the command line), then exits with code 0. It:
   - applies the EF migrations, including the Citus sharding;
   - creates the shared data-protection key;
   - seeds the `Admin` role and the admin user;
   - seeds 4 demo items.
4. **web1–3** start, followed by the **nginx** load balancer.

Only one container applies migrations, so the replicas never race each other.

> **Starting it from Rider:** Rider's Docker "fast mode" adds `.idea/.../compose.generated.override.yml`, which replaces each container's command. The migrator is therefore switched on by an environment variable rather than by the `--migrate` argument. If web1–3 stay in `Created`, check the migrator: `docker compose logs migrator` should end with `Seeded … demo item(s)` and the container should exit with code 0. It must never show `Now listening on`.

### On Kubernetes

The same stack, plus the Merkle logging layer (3 log replicas, an independent auditor, synthetic clients) and failure-injection scripts, is in [`deploy/k8s`](deploy/k8s/README.md):

```bash
kubectl apply -k deploy/k8s
```

---

## Running locally (for development / debugging)

Requirements: .NET 10 SDK and Docker.

1. Start only the database cluster:

   ```bash
   docker compose up -d citus-coordinator citus-worker1 citus-worker2
   ```

2. Run the web app:

   ```bash
   dotnet run --project src/MyShop.Web
   ```

   In the `Development` environment, the app migrates and seeds the database at startup (`Database:MigrateOnStartup = true` in `appsettings.Development.json`). The default connection string, `Host=localhost;Port=5432;Database=myshop;...`, points to the coordinator. The coordinator is published on `127.0.0.1:5432`.

You can also use plain PostgreSQL instead of Citus. The sharding migration checks for the `citus` extension and does nothing if it is not installed.

### Tests

```bash
dotnet test
```

| Project | Covers | Needs Docker |
|---|---|---|
| `MyShop.Domain.Tests` | Business rules (stock, cart, order placement) | no |
| `MyShop.Application.Tests` | Use cases, with in-memory fake adapters | no |
| `MyShop.Infrastructure.Tests` | Real Citus via Testcontainers: sharding, audit timestamps, soft delete, checkout, concurrency | **yes** |

### EF migrations

```bash
dotnet ef migrations add <Name> -p src/MyShop.Infrastructure.Persistence -s src/MyShop.Web -o Migrations
```

Always use `MyShop.Web` as the startup project (`-s`). It configures ASP.NET Identity, and Identity's options shape the Identity tables.

---

## Architecture

```
src/
  MyShop.Domain/                      the core: entities, value objects, rules. No dependencies.
  MyShop.Application/                 use cases + ports (interfaces). Depends on Domain only.
  MyShop.Infrastructure.Persistence/  driven adapter: EF Core / PostgreSQL implementation of the ports.
  MyShop.Web/                         driving adapter (MVC controllers + views) and composition root.
tests/
  MyShop.Domain.Tests/  MyShop.Application.Tests/  MyShop.Infrastructure.Tests/
```

Dependencies always point inward: **Web → Application → Domain ← Persistence**. `MyShop.Web` references the persistence project only to register it in DI (`AddPersistence`). Controllers talk to the application through its ports.

### Domain (`src/MyShop.Domain`)

- **Entities** are immutable `sealed record`s: `Item`, `Cart`/`CartLine`, `Order`/`OrderLine`, `StockMovement`. They all derive from `Entity`, which carries `Id`, `CreatedAt`, `UpdatedAt` and `IsDeleted`.
- **Changes never mutate an entity.** A method such as `item.Refill(qty)` or `cart.Add(item, qty)` returns a new copy (built with `with`), wrapped in a `Result<T>`.
- **`Result<T>`** (`Common/Result.cs`) is either `Success` or `Failure(Error)`. It supports `Map`, `Bind`, `Ensure` and `Match`, plus LINQ query syntax:

  ```csharp
  from created in Item.Create(name, description, price)
  from stocked in created.Refill(quantity)
  from published in stocked.Publish()
  select published;
  ```

- **`Order.Place(cart, items, now)`** is the whole checkout rule as one pure function. It withdraws stock for every line, snapshots the prices, and returns the order, the updated items and the stock movements. If any line fails, nothing is produced.
- **Value objects** `Money` and `Quantity` are `readonly record struct`s that validate themselves in a `Create` method.

### Application (`src/MyShop.Application`)

- **Driving ports** (`Ports/In`) are what the UI calls: `ICatalogService`, `ICartService`, `ICheckoutService`, `IOrderQueryService` and `IBackofficeItemService`.
- **Driven ports** (`Ports/Out`) are what the core needs from the outside: the repositories and `IUnitOfWork`.
- **Use cases** (`UseCases/`) are classes with primary constructors. Each one follows the same steps:
  1. load the data;
  2. call the pure domain function;
  3. persist the result through the unit of work;
  4. log;
  5. return a DTO record.
- **Logging** uses source-generated `[LoggerMessage]` methods (`Log.cs`). These log item created, refilled, published or deleted, order placed, and rejected use cases.

### Persistence (`src/MyShop.Infrastructure.Persistence`)

- **EF classes** (`Entities/`) are mutable classes, separate from the domain records. `Mappers.cs` converts between the two, so the domain knows nothing about EF.
- **`ShopDbContext`** holds the shop tables, ASP.NET Identity (`ShopUser`, `ShopRole`) and the data-protection keys.
- **`AuditInterceptor`** runs on every `SaveChanges`. It:
  - sets `CreatedAt` and `UpdatedAt` on insert;
  - sets `UpdatedAt` on update;
  - turns every **delete into an update with `IsDeleted = true`**;
  - increments `Item.Version`.
- **Soft-delete filter:** a global query filter hides `IsDeleted` rows. Use `IgnoreQueryFilters()` to see them.
- **Optimistic concurrency:** `Item.Version` is a concurrency token. If two requests sell the same last unit, one of them gets a `Conflict` ("please retry") instead of overselling.
- **`UnitOfWork`** runs each use case in one transaction:
  - it commits if the `Result` is a success and rolls back otherwise;
  - it maps concurrency exceptions to a `Conflict` error.

### Web (`src/MyShop.Web`)

| Area | Controllers | Access |
|---|---|---|
| Storefront | `Catalog` (list, details) | anonymous |
| | `Cart` (add, update, remove, checkout), `Orders` (my orders) | logged-in users |
| Backoffice (`Areas/Backoffice`) | `Items` (list, create, edit, refill, publish/unpublish, delete), `Orders` (all orders) | `Admin` role |

- **POST actions** use Post-Redirect-Get with a flash message (`ControllerExtensions.RedirectWithOutcome`).
- **GET actions** map domain errors to HTTP status codes: `NotFound` → 404, `Conflict` → 409.
- **Identity** uses the default Identity UI pages (`/Identity/Account/Login`, `/Register`, …).
- **Health check:** `GET /health`.

---

## How the app is stateless

Any replica can serve any request, which is why nginx can use round-robin without sticky sessions:

| State | Where it lives |
|---|---|
| Cart | Database (`Carts`/`CartLines`), keyed by user |
| Login | Auth cookie, encrypted with **data-protection keys shared through the database** (`DataProtectionKeys` table) |
| Antiforgery tokens | Same shared keys |
| Flash messages (TempData) | Cookie provider |
| Server session / in-memory cache | **None** |

To see it in action:

- Every response carries an **`X-Served-By`** header, and the page footer shows which replica served it. Refresh the page and watch it change.
- The logs include an `Instance:webN` scope.

```bash
curl -sI http://localhost:8080/Catalog | grep -i x-served-by   # run it a few times
docker compose logs -f web1 web2 web3
```

---

## How the sharding works (Citus)

The app connects **only to the coordinator**. Citus routes each query to the right worker. The migration `AddCitusDistribution` sets up this layout:

| Table | Citus type | Shard key | Why |
|---|---|---|---|
| `Carts`, `CartLines`, `Orders`, `OrderLines` | distributed (colocated) | `UserId` | A user's cart and orders live on the same shard, so cart and order queries hit one node |
| `StockMovements` | distributed | `ItemId` | Stock history of one item on one shard |
| `Items` | reference | — | Small and read everywhere, so it is copied to every node; sharded tables can reference it |
| Identity tables, `DataProtectionKeys` | local (coordinator) | — | Only the coordinator needs them |

Because of Citus's rules, the sharded tables have **composite primary keys that include the shard key**, for example `(UserId, Id)`. Their foreign keys include it too.

Checkout writes both the `Items` reference table and the user's shard in one distributed transaction. `UnitOfWork` sets `citus.multi_shard_modify_mode = 'sequential'` for this. Citus requires that setting when a transaction mixes the two kinds of table.

Inspect the cluster:

```bash
docker compose exec citus-coordinator psql -U postgres -d myshop
```

```sql
SELECT nodename, shouldhaveshards FROM pg_dist_node;                         -- coordinator + 2 workers
SELECT table_name, citus_table_type, distribution_column FROM citus_tables;  -- table layout
SELECT table_name, nodename, count(*) FROM citus_shards GROUP BY 1, 2 ORDER BY 1, 2;  -- 16 shards per worker
SELECT "Name", "Stock", "IsDeleted", "CreatedAt", "UpdatedAt" FROM "Items";  -- soft-deleted rows are still here
```

---

## Suggested walkthrough

1. Log in as the **admin** and go to **Backoffice → Items**.
   - Refill the *Desk lamp*, then put it on sale.
   - Open an item to see its stock history.
2. Log out, **register** a buyer, add items to the cart and **place the order**. The stock goes down in the backoffice, and the order shows up under **My orders** and **Backoffice → All orders**.
3. As the admin, **delete** an item. It disappears from the app but stays in the `Items` table with `IsDeleted = true`.
4. Watch the footer and `X-Served-By`: your session survives being served by web1, web2 and web3.

---

## Configuration

| Key | Default | Purpose |
|---|---|---|
| `ConnectionStrings:Shop` | `Host=localhost;Port=5432;Database=myshop;...` | PostgreSQL / Citus coordinator |
| `Database:MigrateOnStartup` | `false` (`true` in Development) | Migrate and seed when the app starts |
| `Seed:AdminEmail` / `Seed:AdminPassword` | `admin@myshop.local` / `Admin123!` | Initial admin account |
| `INSTANCE_NAME` | machine name | Replica name shown in logs, footer and `X-Served-By` |

The demo credentials and the `trust` authentication between Citus nodes are for local use only.
