using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankApiApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class mig_26 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommissionRule",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    MaxCommission = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionRule", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommissionRule");
        }
    }
}
