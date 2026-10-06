using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContributionUniqueAndPagingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Contributions_StokvelId_RecordedAt_Id",
                table: "Contributions",
                columns: new[] { "StokvelId", "RecordedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "UX_Contributions_Stokvel_User_Cycle",
                table: "Contributions",
                columns: new[] { "StokvelId", "UserId", "ContributionCycleId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Contributions_StokvelId_RecordedAt_Id",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "UX_Contributions_Stokvel_User_Cycle",
                table: "Contributions");
        }
    }
}
