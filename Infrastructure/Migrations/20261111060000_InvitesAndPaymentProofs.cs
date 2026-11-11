using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RondiTrack.Infrastructure.Data;

#nullable disable
namespace RondiTrack.Infrastructure.Migrations;

// Manual migration: no SDK is available in the preparation environment.
[DbContext(typeof(RondiTrackDbContext))]
[Migration("20261111060000_InvitesAndPaymentProofs")]
public sealed class InvitesAndPaymentProofs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InviteCodes",
            columns: table => new
            {
                CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                StokvelId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_InviteCodes", x => x.CodeHash);
                table.ForeignKey("FK_InviteCodes_Stokvels_StokvelId", x => x.StokvelId, "Stokvels", "Id", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex(name: "IX_InviteCodes_StokvelId", table: "InviteCodes", column: "StokvelId");
        migrationBuilder.CreateTable(
            name: "PaymentProofs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                StokvelId = table.Column<Guid>(type: "uuid", nullable: false),
                ContributionId = table.Column<Guid>(type: "uuid", nullable: false),
                UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                MimeType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Bytes = table.Column<byte[]>(type: "bytea", nullable: false),
                UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_PaymentProofs", x => x.Id);
                table.ForeignKey("FK_PaymentProofs_Contributions_ContributionId", x => x.ContributionId, "Contributions", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_PaymentProofs_ContributionId", table: "PaymentProofs", column: "ContributionId", unique: true);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PaymentProofs");
        migrationBuilder.DropTable(name: "InviteCodes");
    }
}
