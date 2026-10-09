using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddFounderProgrammeAndEarlyAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FounderBadgeEnabled",
                table: "SiteSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "FounderEarlyAccessDays",
                table: "SiteSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FounderExtraDiscountPercent",
                table: "SiteSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FounderWindowEndsAtUtc",
                table: "SiteSettings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MembersOpenAtUtc",
                table: "Seminars",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "MembersOpenAtUtc",
                table: "Experiences",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FounderSince",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FounderBadgeEnabled",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "FounderEarlyAccessDays",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "FounderExtraDiscountPercent",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "FounderWindowEndsAtUtc",
                table: "SiteSettings");

            migrationBuilder.DropColumn(
                name: "MembersOpenAtUtc",
                table: "Seminars");

            migrationBuilder.DropColumn(
                name: "MembersOpenAtUtc",
                table: "Experiences");

            migrationBuilder.DropColumn(
                name: "FounderSince",
                table: "AspNetUsers");
        }
    }
}
