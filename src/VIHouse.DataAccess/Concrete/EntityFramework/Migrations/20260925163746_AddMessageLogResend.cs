using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageLogResend : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "SmsLogs",
                type: "nvarchar(1600)",
                maxLength: 1600,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResentAt",
                table: "SmsLogs",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "EmailLogs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ResentAt",
                table: "EmailLogs",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Body",
                table: "SmsLogs");

            migrationBuilder.DropColumn(
                name: "ResentAt",
                table: "SmsLogs");

            migrationBuilder.DropColumn(
                name: "Body",
                table: "EmailLogs");

            migrationBuilder.DropColumn(
                name: "ResentAt",
                table: "EmailLogs");
        }
    }
}
