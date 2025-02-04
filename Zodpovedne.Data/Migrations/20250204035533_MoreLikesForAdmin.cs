using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zodpovedne.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoreLikesForAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DiscussionLikes_DiscussionId_UserId",
                table: "DiscussionLikes");

            migrationBuilder.DropIndex(
                name: "IX_CommentLikes_CommentId_UserId",
                table: "CommentLikes");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionLikes_DiscussionId",
                table: "DiscussionLikes",
                column: "DiscussionId");

            migrationBuilder.CreateIndex(
                name: "IX_CommentLikes_CommentId",
                table: "CommentLikes",
                column: "CommentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DiscussionLikes_DiscussionId",
                table: "DiscussionLikes");

            migrationBuilder.DropIndex(
                name: "IX_CommentLikes_CommentId",
                table: "CommentLikes");

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionLikes_DiscussionId_UserId",
                table: "DiscussionLikes",
                columns: new[] { "DiscussionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommentLikes_CommentId_UserId",
                table: "CommentLikes",
                columns: new[] { "CommentId", "UserId" },
                unique: true);
        }
    }
}
