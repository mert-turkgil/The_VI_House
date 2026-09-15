using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanSeatLimitsAndDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MemberDiscountPercent",
                table: "Seminars",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MembershipDuration",
                table: "PromoCodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "MembershipPlanId",
                table: "PromoCodes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderCouponId",
                table: "PromoCodes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictedToEmail",
                table: "PromoCodes",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "PromoCodes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PromoCodeId",
                table: "PendingJoins",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxMembers",
                table: "MembershipPlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PromoCodeId",
                table: "MembershipPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemberDiscountPercent",
                table: "Experiences",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MemberDiscountPercent",
                table: "Seminars");

            migrationBuilder.DropColumn(
                name: "MembershipDuration",
                table: "PromoCodes");

            migrationBuilder.DropColumn(
                name: "MembershipPlanId",
                table: "PromoCodes");

            migrationBuilder.DropColumn(
                name: "ProviderCouponId",
                table: "PromoCodes");

            migrationBuilder.DropColumn(
                name: "RestrictedToEmail",
                table: "PromoCodes");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "PromoCodes");

            migrationBuilder.DropColumn(
                name: "PromoCodeId",
                table: "PendingJoins");

            migrationBuilder.DropColumn(
                name: "MaxMembers",
                table: "MembershipPlans");

            migrationBuilder.DropColumn(
                name: "PromoCodeId",
                table: "MembershipPayments");

            migrationBuilder.DropColumn(
                name: "MemberDiscountPercent",
                table: "Experiences");
        }
    }
}
