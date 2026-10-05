using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionUserForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Contributions_UserId",
                table: "Contributions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_Users_UserId",
                table: "Contributions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_Users_UserId",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_UserId",
                table: "Contributions");
        }
    }
}
