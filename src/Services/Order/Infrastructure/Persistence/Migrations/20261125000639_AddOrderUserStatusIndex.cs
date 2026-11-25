using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Order.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderUserStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "order_status_partial_idx",
                schema: "order",
                table: "order");

            migrationBuilder.CreateIndex(
                name: "order_user_status_idx",
                schema: "order",
                table: "order",
                columns: new[] { "user_id", "status", "created_at", "id" },
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "order_user_status_idx",
                schema: "order",
                table: "order");

            migrationBuilder.CreateIndex(
                name: "order_status_partial_idx",
                schema: "order",
                table: "order",
                column: "status",
                filter: "[status] IN (N'PendingNew', N'New', N'PartiallyFilled', N'PendingCancel')");
        }
    }
}
