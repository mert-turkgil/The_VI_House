using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class MembershipCatalogAndProfileRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Memberships_UserId",
                table: "Memberships");

            // Kept, under its new name: "Bio" is the closest thing a member has already written to
            // "Describe yourself or your business". The form capped it at 1000 characters, but the
            // column was nvarchar(max), so trim defensively before narrowing it.
            migrationBuilder.Sql("UPDATE [Profiles] SET [Bio] = LEFT([Bio], 2000) WHERE LEN([Bio]) > 2000;");
            migrationBuilder.RenameColumn(
                name: "Bio",
                table: "Profiles",
                newName: "About");
            migrationBuilder.AlterColumn<string>(
                name: "About",
                table: "Profiles",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "CanHelpWith",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Industry",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "InstagramHandle",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Interests",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "LookingFor",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "Profiles");

            // The questions changed wording, not meaning: "What are you building?" is the old form
            // of "Describe yourself or your business", "Why do you want to join?" the old form of
            // "What are your expectations?", and the free-text revenue band the old form of the
            // earnings range. Renamed so nobody's answers vanish from the admin review screen.
            migrationBuilder.Sql("UPDATE [Applications] SET [AnnualRevenueBand] = LEFT([AnnualRevenueBand], 20) WHERE LEN([AnnualRevenueBand]) > 20;");
            migrationBuilder.Sql("UPDATE [Applications] SET [BuildingStatement] = LEFT([BuildingStatement], 2000) WHERE LEN([BuildingStatement]) > 2000;");
            migrationBuilder.Sql("UPDATE [Applications] SET [MotivationStatement] = LEFT([MotivationStatement], 2000) WHERE LEN([MotivationStatement]) > 2000;");
            migrationBuilder.Sql("UPDATE [Applications] SET [JobTitle] = LEFT([JobTitle], 200) WHERE LEN([JobTitle]) > 200;");
            migrationBuilder.Sql("UPDATE [Applications] SET [City] = LEFT([City], 100) WHERE LEN([City]) > 100;");
            migrationBuilder.Sql("UPDATE [Applications] SET [Phone] = LEFT([Phone], 32) WHERE LEN([Phone]) > 32;");
            migrationBuilder.Sql("UPDATE [Applications] SET [ReferralCode] = LEFT([ReferralCode], 40) WHERE LEN([ReferralCode]) > 40;");

            migrationBuilder.RenameColumn(
                name: "AnnualRevenueBand",
                table: "Applications",
                newName: "EarningsBand");
            migrationBuilder.AlterColumn<string>(
                name: "EarningsBand",
                table: "Applications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "BuildingStatement",
                table: "Applications",
                newName: "AboutStatement");
            migrationBuilder.AlterColumn<string>(
                name: "AboutStatement",
                table: "Applications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "MotivationStatement",
                table: "Applications",
                newName: "ExpectationsStatement");
            migrationBuilder.AlterColumn<string>(
                name: "ExpectationsStatement",
                table: "Applications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CompanyStage",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ContributionStatement",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "FundingStage",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "HowDidYouHear",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "Industry",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "WebsiteUrl",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CompanyName",
                table: "Profiles");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "Profiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeetingUrl",
                table: "Seminars",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql("UPDATE [Profiles] SET [PhotoUrl] = LEFT([PhotoUrl], 500) WHERE LEN([PhotoUrl]) > 500;");
            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "Profiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "Profiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EarningsBand",
                table: "Profiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Expectations",
                table: "Profiles",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Profiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancelledAt",
                table: "Memberships",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCustomerId",
                table: "Memberships",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderSubscriptionId",
                table: "Memberships",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderPriceId",
                table: "MembershipPlans",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderProductId",
                table: "MembershipPlans",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderSyncError",
                table: "MembershipPlans",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProviderSyncedAt",
                table: "MembershipPlans",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReferralCode",
                table: "Applications",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Applications",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "JobTitle",
                table: "Applications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Applications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "Applications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "Applications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                table: "Applications",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_ProviderSubscriptionId",
                table: "Memberships",
                column: "ProviderSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_UserId_Status",
                table: "Memberships",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MembershipPlans_ProviderProductId",
                table: "MembershipPlans",
                column: "ProviderProductId",
                unique: true,
                filter: "[ProviderProductId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Memberships_ProviderSubscriptionId",
                table: "Memberships");

            migrationBuilder.DropIndex(
                name: "IX_Memberships_UserId_Status",
                table: "Memberships");

            migrationBuilder.DropIndex(
                name: "IX_MembershipPlans_ProviderProductId",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "MeetingUrl",
                table: "Seminars");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "EarningsBand",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "Expectations",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "ProviderCustomerId",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "ProviderSubscriptionId",
                table: "Memberships");

            migrationBuilder.DropColumn(
                name: "ProviderPriceId",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "ProviderProductId",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "ProviderSyncError",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "ProviderSyncedAt",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                table: "Applications");

            migrationBuilder.RenameColumn(
                name: "About",
                table: "Profiles",
                newName: "Bio");
            migrationBuilder.AlterColumn<string>(
                name: "Bio",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Profiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "EarningsBand",
                table: "Applications",
                newName: "AnnualRevenueBand");
            migrationBuilder.AlterColumn<string>(
                name: "AnnualRevenueBand",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "AboutStatement",
                table: "Applications",
                newName: "BuildingStatement");
            migrationBuilder.AlterColumn<string>(
                name: "BuildingStatement",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "ExpectationsStatement",
                table: "Applications",
                newName: "MotivationStatement");
            migrationBuilder.AlterColumn<string>(
                name: "MotivationStatement",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoUrl",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanHelpWith",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Industry",
                table: "Profiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InstagramHandle",
                table: "Profiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Interests",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "Profiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LookingFor",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "Profiles",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReferralCode",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "JobTitle",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "City",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyName",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompanyStage",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContributionStatement",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundingStage",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HowDidYouHear",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Industry",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebsiteUrl",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "Applications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memberships_UserId",
                table: "Memberships",
                column: "UserId");
        }
    }
}
