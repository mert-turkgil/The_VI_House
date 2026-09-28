using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralPayoutsAndFraudSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IpHash",
                table: "ReferralVisits",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "ReferralVisits",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VisitorUserId",
                table: "ReferralVisits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CommissionReversedMinor",
                table: "ReferralConversions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "IsSelfReferral",
                table: "ReferralConversions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "PayoutId",
                table: "ReferralConversions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RefundedMinor",
                table: "ReferralConversions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAt",
                table: "ReferralConversions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "ReferralConversions",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAt",
                table: "ReferralConversions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedByAdminId",
                table: "ReferralConversions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReferralPayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmbassadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaidByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralPayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralPayouts_Ambassadors_AmbassadorId",
                        column: x => x.AmbassadorId,
                        principalTable: "Ambassadors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralVisits_AmbassadorId_IpHash_CreatedAt",
                table: "ReferralVisits",
                columns: new[] { "AmbassadorId", "IpHash", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralConversions_PayoutId",
                table: "ReferralConversions",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralPayouts_AmbassadorId_PaidAt",
                table: "ReferralPayouts",
                columns: new[] { "AmbassadorId", "PaidAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ReferralConversions_ReferralPayouts_PayoutId",
                table: "ReferralConversions",
                column: "PayoutId",
                principalTable: "ReferralPayouts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReferralConversions_ReferralPayouts_PayoutId",
                table: "ReferralConversions");

            migrationBuilder.DropTable(
                name: "ReferralPayouts");

            migrationBuilder.DropIndex(
                name: "IX_ReferralVisits_AmbassadorId_IpHash_CreatedAt",
                table: "ReferralVisits");

            migrationBuilder.DropIndex(
                name: "IX_ReferralConversions_PayoutId",
                table: "ReferralConversions");

            migrationBuilder.DropColumn(
                name: "IpHash",
                table: "ReferralVisits");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "ReferralVisits");

            migrationBuilder.DropColumn(
                name: "VisitorUserId",
                table: "ReferralVisits");

            migrationBuilder.DropColumn(
                name: "CommissionReversedMinor",
                table: "ReferralConversions");

            migrationBuilder.DropColumn(
                name: "IsSelfReferral",
                table: "ReferralConversions");

            migrationBuilder.DropColumn(
                name: "PayoutId",
                table: "ReferralConversions");

            migrationBuilder.DropColumn(
                name: "RefundedMinor",
                table: "ReferralConversions");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                table: "ReferralConversions");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "ReferralConversions");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "ReferralConversions");

            migrationBuilder.DropColumn(
                name: "VoidedByAdminId",
                table: "ReferralConversions");
        }
    }
}
