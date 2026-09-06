using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddExperienceTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Culture",
                table: "ExperienceProgramDays",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "en-GB");

            migrationBuilder.AddColumn<string>(
                name: "Culture",
                table: "ExperienceInclusions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "en-GB");

            migrationBuilder.AddColumn<string>(
                name: "Culture",
                table: "ExperienceFaqs",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "en-GB");

            // The default is en-GB, not "": every existing inclusion, FAQ and programme day was
            // written in English, and the public page shows the set matching the reader's culture,
            // falling back to the default culture's. Left as an empty string, those rows would match
            // no culture at all and every experience would silently lose its inclusions, its FAQ and
            // its programme in all four languages.
            //
            // Applied as the column default rather than a follow-up UPDATE so the value lands as the
            // column is created, with no window in which the rows are untagged.

            migrationBuilder.CreateTable(
                name: "ExperienceTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExperienceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Culture = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShortSummary = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Venue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AudienceTags = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SeoTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SeoDescription = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CoverImageAlt = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExperienceTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExperienceTranslations_Experiences_ExperienceId",
                        column: x => x.ExperienceId,
                        principalTable: "Experiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExperienceTranslations_ExperienceId_Culture",
                table: "ExperienceTranslations",
                columns: new[] { "ExperienceId", "Culture" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExperienceTranslations");

            migrationBuilder.DropColumn(
                name: "Culture",
                table: "ExperienceProgramDays");

            migrationBuilder.DropColumn(
                name: "Culture",
                table: "ExperienceInclusions");

            migrationBuilder.DropColumn(
                name: "Culture",
                table: "ExperienceFaqs");
        }
    }
}
