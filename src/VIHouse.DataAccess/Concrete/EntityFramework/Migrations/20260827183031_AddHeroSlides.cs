using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddHeroSlides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeroSlides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ImageStorageKey = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PrimaryCtaUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SecondaryCtaUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VisibleFromUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    VisibleUntilUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeroSlides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HeroSlideTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HeroSlideId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Culture = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Eyebrow = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Heading = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subheading = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    PrimaryCtaLabel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    SecondaryCtaLabel = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ImageAlt = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeroSlideTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeroSlideTranslations_HeroSlides_HeroSlideId",
                        column: x => x.HeroSlideId,
                        principalTable: "HeroSlides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeroSlides_IsActive_SortOrder",
                table: "HeroSlides",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_HeroSlideTranslations_HeroSlideId_Culture",
                table: "HeroSlideTranslations",
                columns: new[] { "HeroSlideId", "Culture" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeroSlideTranslations");

            migrationBuilder.DropTable(
                name: "HeroSlides");
        }
    }
}
