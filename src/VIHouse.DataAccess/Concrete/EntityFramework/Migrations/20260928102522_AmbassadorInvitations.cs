using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AmbassadorInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Ambassadors",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ActivatedAt",
                table: "Ambassadors",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine1",
                table: "Ambassadors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingAddressLine2",
                table: "Ambassadors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCity",
                table: "Ambassadors",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingCountry",
                table: "Ambassadors",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BillingPostalCode",
                table: "Ambassadors",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteEmail",
                table: "Ambassadors",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InviteExpiresAt",
                table: "Ambassadors",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "InviteSentAt",
                table: "Ambassadors",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InviteTokenHash",
                table: "Ambassadors",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutAccountHolder",
                table: "Ambassadors",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutBic",
                table: "Ambassadors",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PayoutDetailsUpdatedAt",
                table: "Ambassadors",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutIban",
                table: "Ambassadors",
                type: "nvarchar(34)",
                maxLength: 34,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredCulture",
                table: "Ambassadors",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "Ambassadors",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TermsAcceptedAt",
                table: "Ambassadors",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TermsCommissionPercent",
                table: "Ambassadors",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TermsConsentId",
                table: "Ambassadors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TermsVersion",
                table: "Ambassadors",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ambassadors_InviteTokenHash",
                table: "Ambassadors",
                column: "InviteTokenHash",
                unique: true,
                filter: "[InviteTokenHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ambassadors_InviteTokenHash",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "BillingAddressLine1",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "BillingAddressLine2",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "BillingCity",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "BillingCountry",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "BillingPostalCode",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "InviteEmail",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "InviteExpiresAt",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "InviteSentAt",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "InviteTokenHash",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "PayoutAccountHolder",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "PayoutBic",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "PayoutDetailsUpdatedAt",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "PayoutIban",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "PreferredCulture",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "TermsAcceptedAt",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "TermsCommissionPercent",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "TermsConsentId",
                table: "Ambassadors");

            migrationBuilder.DropColumn(
                name: "TermsVersion",
                table: "Ambassadors");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Ambassadors",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
