using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankApiApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class mig_27 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CommissionRule",
                table: "CommissionRule");

            migrationBuilder.RenameTable(
                name: "CommissionRule",
                newName: "CommissionRules");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CommissionRules",
                table: "CommissionRules",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CommissionRules",
                table: "CommissionRules");

            migrationBuilder.RenameTable(
                name: "CommissionRules",
                newName: "CommissionRule");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CommissionRule",
                table: "CommissionRule",
                column: "Id");
        }
    }
}
