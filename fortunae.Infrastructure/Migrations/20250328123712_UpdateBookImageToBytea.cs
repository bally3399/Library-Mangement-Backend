// fortunae.Infrastructure/Migrations/20250328123712_UpdateBookImageToBytea.cs
#nullable disable
using Microsoft.EntityFrameworkCore.Migrations;

namespace fortunae.Infrastructure.Migrations
{
    public partial class UpdateBookImageToBytea : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the old BookImage column
            migrationBuilder.DropColumn(
                name: "BookImage",
                table: "Books");

            // Add the new Image column as bytea
            migrationBuilder.AddColumn<byte[]>(
                name: "Image",
                table: "Books",
                type: "bytea",
                nullable: true); // Set to false if Image is required
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the changes for rollback
            migrationBuilder.DropColumn(
                name: "Image",
                table: "Books");

            migrationBuilder.AddColumn<string>(
                name: "BookImage",
                table: "Books",
                type: "text",
                nullable: true);
        }
    }
}