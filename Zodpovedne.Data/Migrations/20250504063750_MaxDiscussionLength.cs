using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zodpovedne.Data.Migrations
{
    /// <inheritdoc />
    public partial class MaxDiscussionLength : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Discussions",
                type: "character varying(10000)",
                maxLength: 10000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3000)",
                oldMaxLength: 3000);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "Discussions",
                type: "character varying(3000)",
                maxLength: 3000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10000)",
                oldMaxLength: 10000);
        }
    }
}
