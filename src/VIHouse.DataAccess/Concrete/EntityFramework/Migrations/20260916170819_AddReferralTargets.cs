using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddReferralTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReferralVisits_AmbassadorId",
                table: "ReferralVisits");

            migrationBuilder.AddColumn<string>(
                name: "ReferralCode",
                table: "SeminarEnrollments",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandingPath",
                table: "ReferralVisits",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetId",
                table: "ReferralVisits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetKind",
                table: "ReferralVisits",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Site");

            migrationBuilder.AddColumn<Guid>(
                name: "TargetId",
                table: "ReferralConversions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetKind",
                table: "ReferralConversions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Site");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralVisits_AmbassadorId_TargetKind_TargetId",
                table: "ReferralVisits",
                columns: new[] { "AmbassadorId", "TargetKind", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReferralVisits_AmbassadorId_TargetKind_TargetId",
                table: "ReferralVisits");

            migrationBuilder.DropColumn(
                name: "ReferralCode",
                table: "SeminarEnrollments");

            migrationBuilder.DropColumn(
                name: "LandingPath",
                table: "ReferralVisits");

            migrationBuilder.DropColumn(
                name: "TargetId",
                table: "ReferralVisits");

            migrationBuilder.DropColumn(
                name: "TargetKind",
                table: "ReferralVisits");

            migrationBuilder.DropColumn(
                name: "TargetId",
                table: "ReferralConversions");

            migrationBuilder.DropColumn(
                name: "TargetKind",
                table: "ReferralConversions");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralVisits_AmbassadorId",
                table: "ReferralVisits",
                column: "AmbassadorId");
        }
    }
}
