using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RondiTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPayoutXminToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Payouts",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Payouts");
        }
    }
}
