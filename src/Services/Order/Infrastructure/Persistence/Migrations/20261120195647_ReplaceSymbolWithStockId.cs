using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Order.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceSymbolWithStockId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "symbol",
                schema: "order",
                table: "order");

            migrationBuilder.AddColumn<Guid>(
                name: "stock_id",
                schema: "order",
                table: "order",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "stock_id",
                schema: "order",
                table: "order");

            migrationBuilder.AddColumn<string>(
                name: "symbol",
                schema: "order",
                table: "order",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
