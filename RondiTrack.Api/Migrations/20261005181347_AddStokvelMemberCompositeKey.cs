using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStokvelMemberCompositeKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "JoinedAt",
                table: "StokvelMembers",
                newName: "JoinedAtUtc");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "StokvelMembers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Member");

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "ContributionCycles",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ContributionCycles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_StokvelMembers_UserId",
                table: "StokvelMembers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_StokvelMembers_Users_UserId",
                table: "StokvelMembers",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StokvelMembers_Users_UserId",
                table: "StokvelMembers");

            migrationBuilder.DropIndex(
                name: "IX_StokvelMembers_UserId",
                table: "StokvelMembers");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "StokvelMembers");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ContributionCycles");

            migrationBuilder.RenameColumn(
                name: "JoinedAtUtc",
                table: "StokvelMembers",
                newName: "JoinedAt");

            migrationBuilder.AlterColumn<string>(
                name: "Label",
                table: "ContributionCycles",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);
        }
    }
}
