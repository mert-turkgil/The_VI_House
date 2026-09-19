using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VIHouse.DataAccess.Concrete.EntityFramework.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookEventLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebhookEvents",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ObjectId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LiveMode = table.Column<bool>(type: "bit", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PayloadHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookEvents", x => x.EventId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_ObjectId",
                table: "WebhookEvents",
                column: "ObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_Status_ReceivedAt",
                table: "WebhookEvents",
                columns: new[] { "Status", "ReceivedAt" });

            // Every event the old ledger had already seen counts as processed — it was. Nothing
            // else about it was recorded, so the type is a placeholder and the hash is empty.
            migrationBuilder.Sql("""
                INSERT INTO [WebhookEvents] ([EventId], [Type], [ObjectId], [LiveMode], [ReceivedAt], [LastAttemptAt], [ProcessedAt], [Status], [Attempts], [LastError], [PayloadHash])
                SELECT [EventId], N'legacy', NULL, 0, [ProcessedAt], [ProcessedAt], [ProcessedAt], N'Processed', 1, NULL, N''
                FROM [ProcessedWebhookEvents];
                """);

            migrationBuilder.DropTable(
                name: "ProcessedWebhookEvents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebhookEvents");

            migrationBuilder.CreateTable(
                name: "ProcessedWebhookEvents",
                columns: table => new
                {
                    EventId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedWebhookEvents", x => x.EventId);
                });
        }
    }
}
