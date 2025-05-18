using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zodpovedne.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscussionAndCommentIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Discussions_Type",
                table: "Discussions",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Discussions_Type_UpdatedWhateverAt",
                table: "Discussions",
                columns: new[] { "Type", "UpdatedWhateverAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Discussions_UpdatedWhateverAt",
                table: "Discussions",
                column: "UpdatedWhateverAt");

            migrationBuilder.CreateIndex(
                name: "IX_Discussions_ViewCount",
                table: "Discussions",
                column: "ViewCount");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_Type",
                table: "Comments",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_Type_DiscussionId",
                table: "Comments",
                columns: new[] { "Type", "DiscussionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UpdatedAt",
                table: "Comments",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Discussions_Type",
                table: "Discussions");

            migrationBuilder.DropIndex(
                name: "IX_Discussions_Type_UpdatedWhateverAt",
                table: "Discussions");

            migrationBuilder.DropIndex(
                name: "IX_Discussions_UpdatedWhateverAt",
                table: "Discussions");

            migrationBuilder.DropIndex(
                name: "IX_Discussions_ViewCount",
                table: "Discussions");

            migrationBuilder.DropIndex(
                name: "IX_Comments_Type",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_Type_DiscussionId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_UpdatedAt",
                table: "Comments");
        }
    }
}
