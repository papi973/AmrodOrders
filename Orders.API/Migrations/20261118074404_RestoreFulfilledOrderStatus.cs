using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orders.API.Migrations
{
    /// <inheritdoc />
    public partial class RestoreFulfilledOrderStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders",
                sql: "[Status] IN ('Pending', 'Paid', 'Fulfilled', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Orders_Status",
                table: "Orders",
                sql: "[Status] IN ('Pending', 'Paid', 'Delivered', 'Cancelled')");
        }
    }
}
