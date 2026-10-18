using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CycleCloseAndIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "ContributionCycles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndsOn",
                table: "ContributionCycles",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArrearsRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StokvelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionCycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShortfallCents = table.Column<long>(type: "bigint", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArrearsRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArrearsRecords_ContributionCycles_ContributionCycleId",
                        column: x => x.ContributionCycleId,
                        principalTable: "ContributionCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArrearsRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResponseBodyJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContributionCycles_Status_EndsOn",
                table: "ContributionCycles",
                columns: new[] { "Status", "EndsOn" });

            migrationBuilder.CreateIndex(
                name: "UX_ContributionCycles_Stokvel_EndsOn",
                table: "ContributionCycles",
                columns: new[] { "StokvelId", "EndsOn" },
                unique: true,
                filter: "\"EndsOn\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ArrearsRecords_UserId",
                table: "ArrearsRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UX_ArrearsRecords_Cycle_User",
                table: "ArrearsRecords",
                columns: new[] { "ContributionCycleId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_CreatedAt",
                table: "IdempotencyKeys",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArrearsRecords");

            migrationBuilder.DropTable(
                name: "IdempotencyKeys");

            migrationBuilder.DropIndex(
                name: "IX_ContributionCycles_Status_EndsOn",
                table: "ContributionCycles");

            migrationBuilder.DropIndex(
                name: "UX_ContributionCycles_Stokvel_EndsOn",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "ContributionCycles");

            migrationBuilder.DropColumn(
                name: "EndsOn",
                table: "ContributionCycles");
        }
    }
}
