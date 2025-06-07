using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Zodpovedne.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddParametrNumberTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParametrNumbers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParametrName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ParametrValue = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametrNumbers", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ParametrNumbers",
                columns: new[] { "Id", "ParametrName" },
                values: new object[] { 1, "AccessCount" });

            migrationBuilder.CreateIndex(
                name: "IX_ParametrNumbers_ParametrName",
                table: "ParametrNumbers",
                column: "ParametrName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParametrNumbers");
        }
    }
}
