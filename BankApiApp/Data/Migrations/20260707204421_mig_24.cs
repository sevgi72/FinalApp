using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankApiApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class mig_24 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FromBalanceSnapshot",
                table: "Transactions",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ToBalanceSnapshot",
                table: "Transactions",
                type: "numeric(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FromBalanceSnapshot",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ToBalanceSnapshot",
                table: "Transactions");
        }
    }
}
