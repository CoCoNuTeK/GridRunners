using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GridRunners.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSasTokenProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileImageSasExpiration",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileImageSasToken",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProfileImageSasExpiration",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileImageSasToken",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
