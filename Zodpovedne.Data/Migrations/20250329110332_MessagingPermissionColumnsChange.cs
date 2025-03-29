using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zodpovedne.Data.Migrations
{
    /// <inheritdoc />
    public partial class MessagingPermissionColumnsChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessagingPermissions_Users_AllowedUserId",
                table: "MessagingPermissions");

            migrationBuilder.RenameColumn(
                name: "AllowedUserId",
                table: "MessagingPermissions",
                newName: "RequesterUserId");

            migrationBuilder.RenameIndex(
                name: "IX_MessagingPermissions_GranterUserId_AllowedUserId",
                table: "MessagingPermissions",
                newName: "IX_MessagingPermissions_GranterUserId_RequesterUserId");

            migrationBuilder.RenameIndex(
                name: "IX_MessagingPermissions_AllowedUserId",
                table: "MessagingPermissions",
                newName: "IX_MessagingPermissions_RequesterUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MessagingPermissions_Users_RequesterUserId",
                table: "MessagingPermissions",
                column: "RequesterUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MessagingPermissions_Users_RequesterUserId",
                table: "MessagingPermissions");

            migrationBuilder.RenameColumn(
                name: "RequesterUserId",
                table: "MessagingPermissions",
                newName: "AllowedUserId");

            migrationBuilder.RenameIndex(
                name: "IX_MessagingPermissions_RequesterUserId",
                table: "MessagingPermissions",
                newName: "IX_MessagingPermissions_AllowedUserId");

            migrationBuilder.RenameIndex(
                name: "IX_MessagingPermissions_GranterUserId_RequesterUserId",
                table: "MessagingPermissions",
                newName: "IX_MessagingPermissions_GranterUserId_AllowedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MessagingPermissions_Users_AllowedUserId",
                table: "MessagingPermissions",
                column: "AllowedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
