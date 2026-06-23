using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankApiApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class mig4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBlocked",
                table: "Cards");

            migrationBuilder.RenameColumn(
                name: "ExpiryDate",
                table: "Cards",
                newName: "CreatedAt");

            migrationBuilder.AddColumn<string>(
                name: "CardHolderName",
                table: "Cards",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ExpiryMonth",
                table: "Cards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExpiryYear",
                table: "Cards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Cards",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Cards",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardHolderName",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ExpiryMonth",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "ExpiryYear",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Cards");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Cards",
                newName: "ExpiryDate");

            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                table: "Cards",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
