using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyShop.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Shards the shop tables across the Citus workers. A no-op on plain PostgreSQL.
    /// <list type="bullet">
    /// <item><c>Items</c>: reference table, replicated to every node so distributed tables can reference it.</item>
    /// <item><c>Carts</c>, <c>CartLines</c>, <c>Orders</c>, <c>OrderLines</c>: distributed by <c>UserId</c>, colocated,
    /// so a user's cart and orders live on one shard and checkout is a single-shard write (plus the reference table).</item>
    /// <item><c>StockMovements</c>: distributed by <c>ItemId</c>.</item>
    /// <item>Identity and data-protection tables stay local to the coordinator.</item>
    /// </list>
    /// </summary>
    public partial class AddCitusDistribution : Migration
    {
        private const string IfCitus = "IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'citus') THEN";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DO $$
                BEGIN
                    {IfCitus}
                        SET LOCAL citus.multi_shard_modify_mode TO 'sequential';

                        -- Citus refuses to distribute a parent still referenced by a local child:
                        -- drop the parent/child keys, distribute, then restore them (they include the shard key).
                        ALTER TABLE "CartLines" DROP CONSTRAINT "FK_CartLines_Carts_UserId_CartId";
                        ALTER TABLE "OrderLines" DROP CONSTRAINT "FK_OrderLines_Orders_UserId_OrderId";

                        PERFORM create_reference_table('"Items"');
                        PERFORM create_distributed_table('"Carts"', 'UserId');
                        PERFORM create_distributed_table('"CartLines"', 'UserId', colocate_with => '"Carts"');
                        PERFORM create_distributed_table('"Orders"', 'UserId', colocate_with => '"Carts"');
                        PERFORM create_distributed_table('"OrderLines"', 'UserId', colocate_with => '"Carts"');
                        PERFORM create_distributed_table('"StockMovements"', 'ItemId');

                        ALTER TABLE "CartLines" ADD CONSTRAINT "FK_CartLines_Carts_UserId_CartId"
                            FOREIGN KEY ("UserId", "CartId") REFERENCES "Carts" ("UserId", "Id") ON DELETE RESTRICT;
                        ALTER TABLE "OrderLines" ADD CONSTRAINT "FK_OrderLines_Orders_UserId_OrderId"
                            FOREIGN KEY ("UserId", "OrderId") REFERENCES "Orders" ("UserId", "Id") ON DELETE RESTRICT;
                    END IF;
                END
                $$;
                """, suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                DO $$
                BEGIN
                    {IfCitus}
                        PERFORM undistribute_table('"StockMovements"');
                        PERFORM undistribute_table('"OrderLines"', cascade_via_foreign_keys => true);
                        PERFORM undistribute_table('"CartLines"', cascade_via_foreign_keys => true);
                        PERFORM undistribute_table('"Items"', cascade_via_foreign_keys => true);
                    END IF;
                END
                $$;
                """, suppressTransaction: true);
        }
    }
}
