using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class WireContributionCycleToContribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Contributions_ContributionCycleId",
                table: "Contributions",
                column: "ContributionCycleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_ContributionCycles_ContributionCycleId",
                table: "Contributions",
                column: "ContributionCycleId",
                principalTable: "ContributionCycles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_ContributionCycles_ContributionCycleId",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_ContributionCycleId",
                table: "Contributions");
        }
    }
}
