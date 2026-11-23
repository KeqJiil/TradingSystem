using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Order.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock",
                schema: "order",
                columns: table => new
                {
                    stock_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    is_open_to_trade = table.Column<bool>(type: "bit", nullable: false),
                    trading_start = table.Column<TimeOnly>(type: "time", nullable: false),
                    trading_end = table.Column<TimeOnly>(type: "time", nullable: false),
                    name_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    status_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    trading_time_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock", x => x.stock_id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock",
                schema: "order");
        }
    }
}
