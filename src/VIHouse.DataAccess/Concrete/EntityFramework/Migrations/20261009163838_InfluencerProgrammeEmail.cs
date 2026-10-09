using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class InfluencerProgrammeEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InfluencerEmail",
                table: "SiteSettings",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InfluencerEmail",
                table: "SiteSettings");
        }
    }
}
