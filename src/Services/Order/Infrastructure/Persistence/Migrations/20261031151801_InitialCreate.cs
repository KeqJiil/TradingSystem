using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Order.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "order");

            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateSequence(
                name: "outbox_id_seq",
                schema: "messaging");

            migrationBuilder.CreateTable(
                name: "inbox",
                schema: "messaging",
                columns: table => new
                {
                    consumer_group = table.Column<string>(type: "VARCHAR(200)", nullable: false),
                    message_id = table.Column<string>(type: "VARCHAR(100)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox", x => new { x.consumer_group, x.message_id });
                });

            migrationBuilder.CreateTable(
                name: "order",
                schema: "order",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    filled_quantity = table.Column<int>(type: "int", nullable: false),
                    order_side = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    cancellation_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    canceled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    rejection_reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    rejected_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    filled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    expired_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    limit_price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    time_in_force = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    order_type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "messaging",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "NEXT VALUE FOR [messaging].[outbox_id_seq]"),
                    message_id = table.Column<string>(type: "VARCHAR(100)", nullable: false),
                    message_type = table.Column<string>(type: "VARCHAR(200)", nullable: false),
                    topic = table.Column<string>(type: "VARCHAR(255)", nullable: false),
                    message_key = table.Column<string>(type: "VARCHAR(200)", nullable: false),
                    payload = table.Column<byte[]>(type: "VARBINARY(MAX)", nullable: false),
                    headers = table.Column<string>(type: "NVARCHAR(MAX)", nullable: false),
                    status = table.Column<string>(type: "VARCHAR(20)", nullable: false),
                    attempts = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSDATETIMEOFFSET()"),
                    processed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fill",
                schema: "order",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    execution_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    order_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    price = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    filled_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fill", x => x.id);
                    table.ForeignKey(
                        name: "FK_fill_order_order_id",
                        column: x => x.order_id,
                        principalSchema: "order",
                        principalTable: "order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "fill_execution_id_idx",
                schema: "order",
                table: "fill",
                column: "execution_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fill_order_id",
                schema: "order",
                table: "fill",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "idx_inbox_created_at",
                schema: "messaging",
                table: "inbox",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "order_status_partial_idx",
                schema: "order",
                table: "order",
                column: "status",
                filter: "[status] IN (N'PendingNew', N'New', N'PartiallyFilled', N'PendingCancel')");

            migrationBuilder.CreateIndex(
                name: "order_user_id_idx",
                schema: "order",
                table: "order",
                columns: new[] { "user_id", "created_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_outbox_completed",
                schema: "messaging",
                table: "outbox",
                column: "created_at",
                filter: "[status] = 'Completed'");

            migrationBuilder.CreateIndex(
                name: "idx_outbox_pending",
                schema: "messaging",
                table: "outbox",
                column: "id",
                filter: "[status] IN ('Pending', 'Processing')")
                .Annotation("SqlServer:Include", new[] { "processed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fill",
                schema: "order");

            migrationBuilder.DropTable(
                name: "inbox",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "order",
                schema: "order");

            migrationBuilder.DropSequence(
                name: "outbox_id_seq",
                schema: "messaging");
        }
    }
}
