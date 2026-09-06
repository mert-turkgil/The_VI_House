using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddExperienceMemberAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_ExperienceId",
                table: "Bookings");

            migrationBuilder.AddColumn<int>(
                name: "AttendanceMode",
                table: "Experiences",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "TicketTypeId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<int>(
                name: "Attendance",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GrantedVia",
                table: "Bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ExperienceMembershipAccess",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExperienceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MembershipPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperienceMembershipAccess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExperienceMembershipAccess_Experiences_ExperienceId",
                        column: x => x.ExperienceId,
                        principalTable: "Experiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExperienceMembershipAccess_MembershipPlans_MembershipPlanId",
                        column: x => x.MembershipPlanId,
                        principalTable: "MembershipPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ExperienceId_UserId",
                table: "Bookings",
                columns: new[] { "ExperienceId", "UserId" },
                unique: true,
                filter: "[Status] <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceMembershipAccess_ExperienceId_MembershipPlanId",
                table: "ExperienceMembershipAccess",
                columns: new[] { "ExperienceId", "MembershipPlanId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceMembershipAccess_MembershipPlanId",
                table: "ExperienceMembershipAccess",
                column: "MembershipPlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExperienceMembershipAccess");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ExperienceId_UserId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "AttendanceMode",
                table: "Experiences");

            migrationBuilder.DropColumn(
                name: "Attendance",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "GrantedVia",
                table: "Bookings");

            migrationBuilder.AlterColumn<Guid>(
                name: "TicketTypeId",
                table: "Bookings",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ExperienceId",
                table: "Bookings",
                column: "ExperienceId");
        }
    }
}
