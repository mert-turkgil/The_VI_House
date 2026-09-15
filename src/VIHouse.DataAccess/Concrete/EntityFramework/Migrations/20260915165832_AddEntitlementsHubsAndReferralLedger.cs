using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddEntitlementsHubsAndReferralLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LiveStreamUrl",
                table: "Seminars",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GrantNote",
                table: "Memberships",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GrantedByUserId",
                table: "Memberships",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsComplimentary",
                table: "Memberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DiscordRoleId",
                table: "MembershipPlans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesCommunity",
                table: "MembershipPlans",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesDirectory",
                table: "MembershipPlans",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesMemberCard",
                table: "MembershipPlans",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludesSessions",
                table: "MembershipPlans",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "LiveStreamUrl",
                table: "Experiences",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingUrl",
                table: "Experiences",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordChannelId",
                table: "CommunityLinks",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExperienceId",
                table: "CommunityLinks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MembershipPlanId",
                table: "CommunityLinks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeminarId",
                table: "CommunityLinks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReferralConversions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AmbassadorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    CommissionMinor = table.Column<long>(type: "bigint", nullable: true),
                    SourceEntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralConversions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralConversions_Ambassadors_AmbassadorId",
                        column: x => x.AmbassadorId,
                        principalTable: "Ambassadors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityLinks_ExperienceId",
                table: "CommunityLinks",
                column: "ExperienceId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityLinks_MembershipPlanId",
                table: "CommunityLinks",
                column: "MembershipPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityLinks_SeminarId",
                table: "CommunityLinks",
                column: "SeminarId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralConversions_AmbassadorId_OccurredAt",
                table: "ReferralConversions",
                columns: new[] { "AmbassadorId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralConversions_SourceEntityType_SourceEntityId_Kind",
                table: "ReferralConversions",
                columns: new[] { "SourceEntityType", "SourceEntityId", "Kind" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferralConversions");

            migrationBuilder.DropIndex(
                name: "IX_CommunityLinks_ExperienceId",
                table: "CommunityLinks");

            migrationBuilder.DropIndex(
                name: "IX_CommunityLinks_MembershipPlanId",
                table: "CommunityLinks");

            migrationBuilder.DropIndex(
                name: "IX_CommunityLinks_SeminarId",
                table: "CommunityLinks");

            migrationBuilder.DropColumn(
                name: "LiveStreamUrl",
                table: "Seminars");

            migrationBuilder.DropColumn(
                name: "GrantNote",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "GrantedByUserId",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "IsComplimentary",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "DiscordRoleId",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "IncludesCommunity",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "IncludesDirectory",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "IncludesMemberCard",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "IncludesSessions",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "LiveStreamUrl",
                table: "Experiences");

            migrationBuilder.DropColumn(
                name: "MeetingUrl",
                table: "Experiences");

            migrationBuilder.DropColumn(
                name: "DiscordChannelId",
                table: "CommunityLinks");

            migrationBuilder.DropColumn(
                name: "ExperienceId",
                table: "CommunityLinks");

            migrationBuilder.DropColumn(
                name: "MembershipPlanId",
                table: "CommunityLinks");

            migrationBuilder.DropColumn(
                name: "SeminarId",
                table: "CommunityLinks");
        }
    }
}
