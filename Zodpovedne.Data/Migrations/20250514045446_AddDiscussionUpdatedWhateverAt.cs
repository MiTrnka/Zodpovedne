using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zodpovedne.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionUpdatedWhateverAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedWhateverAt",
                table: "Discussions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Aktualizace hodnoty nového sloupce pro existující záznamy
            // Nastaví UpdatedWhateverAt na stejnou hodnotu jako UpdatedAt
            migrationBuilder.Sql(
                "UPDATE \"Discussions\" SET \"UpdatedWhateverAt\" = \"UpdatedAt\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedWhateverAt",
                table: "Discussions");
        }
    }
}
