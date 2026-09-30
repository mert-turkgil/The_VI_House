using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalMediaGallery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Captions",
                table: "JournalPostMedia",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "JournalPostMedia",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInGallery",
                table: "JournalPostMedia",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_JournalPostMedia_JournalPostId_ContentHash",
                table: "JournalPostMedia",
                columns: new[] { "JournalPostId", "ContentHash" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JournalPostMedia_JournalPostId_ContentHash",
                table: "JournalPostMedia");

            migrationBuilder.DropColumn(
                name: "Captions",
                table: "JournalPostMedia");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "JournalPostMedia");

            migrationBuilder.DropColumn(
                name: "ShowInGallery",
                table: "JournalPostMedia");
        }
    }
}
