﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalTranslationsAndMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CoverImageUrl",
                table: "JournalPosts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoverMediaId",
                table: "JournalPosts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JournalPostMedia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalPostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsInline = table.Column<bool>(type: "bit", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalPostMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalPostMedia_JournalPosts_JournalPostId",
                        column: x => x.JournalPostId,
                        principalTable: "JournalPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JournalPostTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JournalPostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Culture = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Excerpt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JournalPostTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JournalPostTranslations_JournalPosts_JournalPostId",
                        column: x => x.JournalPostId,
                        principalTable: "JournalPosts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JournalPostMedia_JournalPostId_SortOrder",
                table: "JournalPostMedia",
                columns: new[] { "JournalPostId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_JournalPostTranslations_JournalPostId_Culture",
                table: "JournalPostTranslations",
                columns: new[] { "JournalPostId", "Culture" },
                unique: true);

            // Move the existing copy into a default-culture row, then drop the columns it came from.
            //
            // The order is the whole point: EF scaffolded the three DropColumn calls first, which
            // would have thrown every published article away. Written by hand, before the drops, and
            // as one statement rather than a loop so it is atomic with the rest of the migration.
            //
            // NEWID() rather than a generated key: BaseEntity assigns ids in C#, and this runs in
            // SQL where nothing is tracked. SYSDATETIMEOFFSET() matches the CreatedAt default that
            // every other row gets.
            migrationBuilder.Sql("""
                INSERT INTO JournalPostTranslations (Id, JournalPostId, Culture, Title, Excerpt, Body, CreatedAt)
                SELECT NEWID(), p.Id, N'en-GB', p.Title, p.Excerpt, p.Body, SYSDATETIMEOFFSET()
                FROM JournalPosts AS p;
                """);

            migrationBuilder.DropColumn(
                name: "Body",
                table: "JournalPosts");

            migrationBuilder.DropColumn(
                name: "Excerpt",
                table: "JournalPosts");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "JournalPosts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JournalPostMedia");

            migrationBuilder.DropColumn(
                name: "CoverMediaId",
                table: "JournalPosts");

            migrationBuilder.AlterColumn<string>(
                name: "CoverImageUrl",
                table: "JournalPosts",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Body",
                table: "JournalPosts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Excerpt",
                table: "JournalPosts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "JournalPosts",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // The mirror image of Up: put the default-culture copy back into the columns before the
            // table holding it goes. A Down that loses every article is not a rollback.
            migrationBuilder.Sql("""
                UPDATE p
                SET p.Title = t.Title, p.Excerpt = t.Excerpt, p.Body = t.Body
                FROM JournalPosts AS p
                INNER JOIN JournalPostTranslations AS t
                    ON t.JournalPostId = p.Id AND t.Culture = N'en-GB';
                """);

            migrationBuilder.DropTable(
                name: "JournalPostTranslations");
        }
    }
}
