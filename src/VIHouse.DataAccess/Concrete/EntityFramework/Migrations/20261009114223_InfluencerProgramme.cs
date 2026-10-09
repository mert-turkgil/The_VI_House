using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class InfluencerProgramme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AuthorUserId",
                table: "JournalPosts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "JournalPosts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAt",
                table: "JournalPosts",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "Ambassadors",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalFirstName",
                table: "Ambassadors",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalLastName",
                table: "Ambassadors",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Niche",
                table: "Ambassadors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoStorageKey",
                table: "Ambassadors",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AmbassadorChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmbassadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AmbassadorChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AmbassadorChannels_Ambassadors_AmbassadorId",
                        column: x => x.AmbassadorId,
                        principalTable: "Ambassadors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReferralWithdrawalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmbassadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    RequestedMinor = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecidedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PayoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralWithdrawalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralWithdrawalRequests_Ambassadors_AmbassadorId",
                        column: x => x.AmbassadorId,
                        principalTable: "Ambassadors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReferralWithdrawalRequests_ReferralPayouts_PayoutId",
                        column: x => x.PayoutId,
                        principalTable: "ReferralPayouts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalPosts_AuthorUserId",
                table: "JournalPosts",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AmbassadorChannels_AmbassadorId_SortOrder",
                table: "AmbassadorChannels",
                columns: new[] { "AmbassadorId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralWithdrawalRequests_AmbassadorId_Currency",
                table: "ReferralWithdrawalRequests",
                columns: new[] { "AmbassadorId", "Currency" },
                unique: true,
                filter: "[Status] = 'Open'");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralWithdrawalRequests_PayoutId",
                table: "ReferralWithdrawalRequests",
                column: "PayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralWithdrawalRequests_Status_RequestedAt",
                table: "ReferralWithdrawalRequests",
                columns: new[] { "Status", "RequestedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_JournalPosts_AspNetUsers_AuthorUserId",
                table: "JournalPosts",
                column: "AuthorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JournalPosts_AspNetUsers_AuthorUserId",
                table: "JournalPosts");

            migrationBuilder.DropTable(
                name: "AmbassadorChannels");

            migrationBuilder.DropTable(
                name: "ReferralWithdrawalRequests");

            migrationBuilder.DropIndex(
                name: "IX_JournalPosts_AuthorUserId",
                table: "JournalPosts");

            migrationBuilder.DropColumn(
                name: "AuthorUserId",
                table: "JournalPosts");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "JournalPosts");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "JournalPosts");

            migrationBuilder.DropColumn(
                name: "Bio",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "LegalFirstName",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "LegalLastName",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "Niche",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "PhotoStorageKey",
                table: "Ambassadors");
        }
    }
}
